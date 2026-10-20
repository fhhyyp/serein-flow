using SereinFlow.Contracts;

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
    public const string Closed = WorkerErrorCodes.TransportClosed;
    public const string ReceiveConcurrent = WorkerErrorCodes.TransportReceiveConcurrent;
    public const string SendFailed = WorkerErrorCodes.TransportSendFailed;
    public const string ReceiveFailed = WorkerErrorCodes.TransportReceiveFailed;
    public const string InvalidFrame = WorkerErrorCodes.TransportInvalidFrame;
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
public sealed record WorkerTransportCloseResult(WorkerTransportCloseReason Reason, bool IsRemote, Exception? Exception = null)
{
    public static WorkerTransportCloseResult Open { get; } = new(WorkerTransportCloseReason.None, false);
}

public sealed class WorkerTransportException : Exception
{
    public WorkerTransportException(string code, string message, Exception? innerException = null, WorkerTransportCloseResult? closeResult = null) : base(message, innerException)
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
    private static bool StartsWithJsonObject(string line)
    {
        var trimmedLine = line.TrimStart();
        var startsWithObject = trimmedLine.StartsWith('{');
        return startsWithObject;
    }

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
    public StdioWorkerTransport(Stream input, Stream output, Action<string>? diagnostic = null, bool leaveOpen = false)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input), "The worker input stream cannot be null. Worker 输入流不能为空。");
        _output = output ?? throw new ArgumentNullException(nameof(output), "The worker output stream cannot be null. Worker 输出流不能为空。");
        _diagnostic = diagnostic;
        _leaveOpen = leaveOpen;
        var uTF8Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        _reader = new StreamReader(_input, uTF8Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
    }

    public WorkerTransportCloseResult CloseResult
    {
        get
        {
            var readResult = Volatile.Read(ref _closeResult);
            return readResult;
        }
    }

    public async ValueTask SendAsync(WorkerMessage message, CancellationToken cancellationToken = default)
    {
        EnsureCanSend();
        var waitAsyncResult = _sendGate.WaitAsync(cancellationToken);
        await waitAsyncResult.ConfigureAwait(false);
        try
        {
            EnsureCanSend();
            var evaluatedValue = WorkerProtocolCodec.Serialize(message) + "\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(evaluatedValue);
            try
            {
                var writeAsyncResult = _output.WriteAsync(bytes, cancellationToken);
                await writeAsyncResult.ConfigureAwait(false);
                var flushAsyncResult = _output.FlushAsync(cancellationToken);
                await flushAsyncResult.ConfigureAwait(false);
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
                var workerTransportCloseResult = new WorkerTransportCloseResult(WorkerTransportCloseReason.Faulted, true, exception);
                SetCloseResult(workerTransportCloseResult);
                throw new WorkerTransportException(WorkerTransportErrorCodes.SendFailed, "The Worker transport could not send a message. Worker 传输无法发送消息。", exception, CloseResult);
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
        {
            global::SereinFlow.Worker.Protocol.WorkerMessage? evaluatedValue1 = CloseResult.IsRemote ? null : throw CreateClosedException();
            return evaluatedValue1;
        }

        if (Interlocked.CompareExchange(ref _receiveActive, 1, 0) != 0)
        {
            throw new WorkerTransportException(WorkerTransportErrorCodes.ReceiveConcurrent, "Only one Worker transport receive loop may be active. Worker 传输只能有一个接收循环。");
        }

        try
        {
            while (true)
            {
                string? line;
                try
                {
                    var readLineAsyncResult = _reader.ReadLineAsync(cancellationToken);
                    line = await readLineAsyncResult.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ObjectDisposedException)when (Volatile.Read(ref _disposed) == 1)
                {
                    return null;
                }
                catch (System.Text.DecoderFallbackException exception)
                {
                    var workerTransportCloseResult2 = new WorkerTransportCloseResult(WorkerTransportCloseReason.Faulted, true, exception);
                    SetCloseResult(workerTransportCloseResult2);
                    throw new WorkerTransportException(WorkerTransportErrorCodes.InvalidFrame, "The Worker transport received invalid UTF-8 data. Worker 传输收到无效的 UTF-8 数据。", exception, CloseResult);
                }
                catch (IOException exception)
                {
                    var workerTransportCloseResult3 = new WorkerTransportCloseResult(WorkerTransportCloseReason.Faulted, true, exception);
                    SetCloseResult(workerTransportCloseResult3);
                    throw new WorkerTransportException(WorkerTransportErrorCodes.ReceiveFailed, "The Worker transport could not receive a message. Worker 传输无法接收消息。", exception, CloseResult);
                }

                if (line is null)
                {
                    var workerTransportCloseResult4 = new WorkerTransportCloseResult(WorkerTransportCloseReason.RemoteEof, true);
                    SetCloseResult(workerTransportCloseResult4);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var deserializeResult = WorkerProtocolCodec.Deserialize(line);
                    return deserializeResult;
                }
                catch (WorkerProtocolException exception)when (exception.Code == WorkerErrorCodes.InvalidMessage && !StartsWithJsonObject(line))
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
        {
            var workerTransportCloseResult5 = new WorkerTransportCloseResult(WorkerTransportCloseReason.Local, false);
            SetCloseResult(workerTransportCloseResult5);
        }

        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

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
        {
            return;
        }

        if (!ReferenceEquals(_input, _output))
        {
            try
            {
                var disposeAsyncResult = _input.DisposeAsync();
                await disposeAsyncResult.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        try
        {
            var disposeAsyncResult6 = _output.DisposeAsync();
            await disposeAsyncResult6.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        var closeAsyncResult = CloseAsync();
        return closeAsyncResult;
    }

    private void EnsureCanSend()
    {
        if (Volatile.Read(ref _closed) == 1)
        {
            throw CreateClosedException();
        }
    }

    private WorkerTransportException CreateClosedException(Exception? innerException = null)
    {
        global::SereinFlow.Worker.Protocol.WorkerTransportException evaluatedValue7 = new(WorkerTransportErrorCodes.Closed, "The Worker transport is closed. Worker 传输已关闭。", innerException, CloseResult);
        return evaluatedValue7;
    }

    private void SetCloseResult(WorkerTransportCloseResult result)
    {
        Interlocked.CompareExchange(ref _closeResult, result, WorkerTransportCloseResult.Open);
    }
}
