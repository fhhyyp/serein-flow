namespace SereinFlow.Worker.Protocol;

/// <summary>
/// Message-oriented transport used by the Worker protocol boundary.
/// Implementations own message framing and must allow only one active receive
/// operation at a time.
/// Worker 协议边界使用的面向消息传输；实现负责消息分帧，并且同一时间只能存在一个接收操作。
/// </summary>
public interface IWorkerTransport : IAsyncDisposable
{
    ValueTask SendAsync(WorkerMessage message, CancellationToken cancellationToken = default);

    ValueTask<WorkerMessage?> ReceiveAsync(CancellationToken cancellationToken = default);

    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}

public static class WorkerTransportErrorCodes
{
    public const string Closed = "worker.transport_closed";
    public const string ReceiveConcurrent = "worker.transport_receive_concurrent";
    public const string SendFailed = "worker.transport_send_failed";
    public const string ReceiveFailed = "worker.transport_receive_failed";
    public const string InvalidFrame = "worker.transport_invalid_frame";
}

public enum WorkerTransportCloseReason
{
    None = 0,
    Local = 1,
    RemoteEof = 2,
    Faulted = 3
}

/// <summary>
/// Stable lifecycle snapshot for a transport endpoint.
/// 传输端点的稳定生命周期快照。
/// </summary>
public sealed record WorkerTransportCloseResult(
    WorkerTransportCloseReason Reason,
    bool IsRemote,
    Exception? Exception = null)
{
    public static WorkerTransportCloseResult Open { get; } = new(WorkerTransportCloseReason.None, false);
}

public sealed class WorkerTransportException : Exception
{
    public WorkerTransportException(
        string code,
        string message,
        Exception? innerException = null,
        WorkerTransportCloseResult? closeResult = null)
        : base(message, innerException)
    {
        Code = code;
        CloseResult = closeResult;
    }

    public string Code { get; }

    public WorkerTransportCloseResult? CloseResult { get; }
}

/// <summary>
/// Stdio compatibility binding: one UTF-8 JSON message per line.
/// stdio 兼容传输绑定：每行一个 UTF-8 JSON 消息。
/// </summary>
public sealed class StdioWorkerTransport : IWorkerTransport
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly StreamReader _reader;
    private readonly Action<string>? _diagnostic;
    private readonly bool _leaveOpen;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private WorkerTransportCloseResult _closeResult = WorkerTransportCloseResult.Open;
    private int _receiveActive;
    private int _closed;
    private int _disposed;

    public StdioWorkerTransport(
        Stream input,
        Stream output,
        Action<string>? diagnostic = null,
        bool leaveOpen = false)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input), "The worker input stream cannot be null. Worker 输入流不能为空。");
        _output = output ?? throw new ArgumentNullException(nameof(output), "The worker output stream cannot be null. Worker 输出流不能为空。");
        _diagnostic = diagnostic;
        _leaveOpen = leaveOpen;
        _reader = new StreamReader(
            _input,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
    }

    public WorkerTransportCloseResult CloseResult => Volatile.Read(ref _closeResult);

    public async ValueTask SendAsync(WorkerMessage message, CancellationToken cancellationToken = default)
    {
        EnsureCanSend();
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureCanSend();
            var bytes = System.Text.Encoding.UTF8.GetBytes(WorkerProtocolCodec.Serialize(message) + "\n");
            try
            {
                await _output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException exception)
            {
                throw CreateClosedException(exception);
            }
            catch (IOException exception)
            {
                SetCloseResult(new WorkerTransportCloseResult(WorkerTransportCloseReason.Faulted, true, exception));
                throw new WorkerTransportException(
                    WorkerTransportErrorCodes.SendFailed,
                    "The Worker transport could not send a message. Worker 传输无法发送消息。",
                    exception,
                    CloseResult);
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public async ValueTask<WorkerMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _closed) == 1)
            return CloseResult.IsRemote ? null : throw CreateClosedException();
        if (Interlocked.CompareExchange(ref _receiveActive, 1, 0) != 0)
        {
            throw new WorkerTransportException(
                WorkerTransportErrorCodes.ReceiveConcurrent,
                "Only one Worker transport receive loop may be active. Worker 传输只能有一个接收循环。");
        }

        try
        {
            while (true)
            {
                string? line;
                try
                {
                    line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) == 1)
                {
                    return null;
                }
                catch (System.Text.DecoderFallbackException exception)
                {
                    SetCloseResult(new WorkerTransportCloseResult(WorkerTransportCloseReason.Faulted, true, exception));
                    throw new WorkerTransportException(
                        WorkerTransportErrorCodes.InvalidFrame,
                        "The Worker transport received invalid UTF-8 data. Worker 传输收到无效的 UTF-8 数据。",
                        exception,
                        CloseResult);
                }
                catch (IOException exception)
                {
                    SetCloseResult(new WorkerTransportCloseResult(WorkerTransportCloseReason.Faulted, true, exception));
                    throw new WorkerTransportException(
                        WorkerTransportErrorCodes.ReceiveFailed,
                        "The Worker transport could not receive a message. Worker 传输无法接收消息。",
                        exception,
                        CloseResult);
                }

                if (line is null)
                {
                    SetCloseResult(new WorkerTransportCloseResult(WorkerTransportCloseReason.RemoteEof, true));
                    return null;
                }
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    return WorkerProtocolCodec.Deserialize(line);
                }
                catch (WorkerProtocolException exception)
                    when (exception.Code == "worker.invalid_message"
                        && !line.TrimStart().StartsWith('{'))
                {
                    // Keep the existing stdio compatibility behavior: ignore
                    // plain-text noise, but fail on malformed JSON-looking data.
                    // 保留 stdio 兼容行为：忽略纯文本噪声，但疑似 JSON 的坏数据必须失败。
                    try
                    {
                        _diagnostic?.Invoke(line);
                    }
                    catch
                    {
                        // Diagnostics must never break protocol consumption.
                    }
                }
            }
        }
        finally
        {
            Volatile.Write(ref _receiveActive, 0);
        }
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
            SetCloseResult(new WorkerTransportCloseResult(WorkerTransportCloseReason.Local, false));

        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Closing is cleanup, so it remains best-effort even if its caller is
        // already cancelled. The token is intentionally not used here.
        // 关闭属于清理操作，即使调用方已取消也必须尽力完成，因此这里不使用 cancellationToken。
        try
        {
            _reader.Dispose();
        }
        catch
        {
        }

        if (_leaveOpen)
            return;

        if (!ReferenceEquals(_input, _output))
        {
            try
            {
                await _input.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        try
        {
            await _output.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public ValueTask DisposeAsync() => CloseAsync();

    private void EnsureCanSend()
    {
        if (Volatile.Read(ref _closed) == 1)
            throw CreateClosedException();
    }

    private WorkerTransportException CreateClosedException(Exception? innerException = null)
        => new(
            WorkerTransportErrorCodes.Closed,
            "The Worker transport is closed. Worker 传输已关闭。",
            innerException,
            CloseResult);

    private void SetCloseResult(WorkerTransportCloseResult result)
        => Interlocked.CompareExchange(ref _closeResult, result, WorkerTransportCloseResult.Open);
}
