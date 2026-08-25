using System.Text;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Worker.Protocol;

public static class WorkerProtocolConstants
{
    public const int Version = SereinFlow.Contracts.WorkerProtocol.Version;
    public const int MaxMessageBytes = 1_048_576;
    public const int MaxPayloadBytes = 896_000;
    public const string ReadyKind = "worker.ready";
    public const string HandshakeKind = "worker.handshake";
    public const string HandshakeAcceptedKind = "worker.handshake.accepted";
    public const string RunKind = "worker.run";
    public const string CancelKind = "worker.cancel";
    public const string CancelAcknowledgedKind = "worker.cancel.ack";
    public const string HeartbeatKind = "worker.heartbeat";
    public const string HeartbeatAcknowledgedKind = "worker.heartbeat.ack";
    public const string EventKind = "worker.event";
    public const string ResultKind = "worker.result";
    public const string ErrorKind = "worker.error";
}

public sealed record WorkerMessage(
    int ProtocolVersion,
    string Kind,
    string RequestId,
    Guid? RunId = null,
    long? Sequence = null,
    DateTimeOffset? Deadline = null,
    string? PayloadJson = null)
{
    public static WorkerMessage Create(string kind, string? payloadJson = null, Guid? runId = null, DateTimeOffset? deadline = null, long? sequence = null)
        => new(WorkerProtocolConstants.Version, kind, Guid.NewGuid().ToString("N"), runId, sequence, deadline, payloadJson);
}

public sealed record WorkerErrorDto(string Code, string Message);

public static class WorkerProtocolCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(WorkerMessage message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message), "The worker message cannot be null. Worker 消息不能为空。");
        if (message.ProtocolVersion != WorkerProtocolConstants.Version)
            throw new WorkerProtocolException("worker.protocol_mismatch", $"Unsupported protocol version '{message.ProtocolVersion}'. 不支持的协议版本“{message.ProtocolVersion}”。");

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > WorkerProtocolConstants.MaxMessageBytes)
            throw new WorkerProtocolException("worker.message_too_large", $"Worker messages are limited to {WorkerProtocolConstants.MaxMessageBytes} bytes. Worker 消息大小不能超过 {WorkerProtocolConstants.MaxMessageBytes} 字节。");
        return json;
    }

    public static WorkerMessage Deserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new WorkerProtocolException("worker.invalid_message", "Worker message cannot be empty. Worker 消息不能为空。");
        if (Encoding.UTF8.GetByteCount(line) > WorkerProtocolConstants.MaxMessageBytes)
            throw new WorkerProtocolException("worker.message_too_large", $"Worker messages are limited to {WorkerProtocolConstants.MaxMessageBytes} bytes. Worker 消息大小不能超过 {WorkerProtocolConstants.MaxMessageBytes} 字节。");

        try
        {
            var message = JsonSerializer.Deserialize<WorkerMessage>(line, JsonOptions)
                ?? throw new WorkerProtocolException("worker.invalid_message", "Worker message payload is null. Worker 消息载荷为空。");
            if (message.ProtocolVersion != WorkerProtocolConstants.Version)
                throw new WorkerProtocolException("worker.protocol_mismatch", $"Unsupported protocol version '{message.ProtocolVersion}'. 不支持的协议版本“{message.ProtocolVersion}”。");
            if (string.IsNullOrWhiteSpace(message.Kind) || string.IsNullOrWhiteSpace(message.RequestId))
                throw new WorkerProtocolException("worker.invalid_message", "Worker message kind and requestId are required. Worker 消息类型和 requestId 不能为空。");
            if (message.PayloadJson is { Length: > WorkerProtocolConstants.MaxPayloadBytes })
                throw new WorkerProtocolException("worker.payload_too_large", $"Worker payloads are limited to {WorkerProtocolConstants.MaxPayloadBytes} characters. Worker 载荷不能超过 {WorkerProtocolConstants.MaxPayloadBytes} 个字符。");
            return message;
        }
        catch (JsonException exception)
        {
            throw new WorkerProtocolException("worker.invalid_message", "Worker message is not valid JSON. Worker 消息不是有效的 JSON。", exception);
        }
    }

    public static async ValueTask WriteAsync(Stream stream, WorkerMessage message, CancellationToken cancellationToken = default)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream), "The worker stream cannot be null. Worker 流不能为空。");
        var line = Serialize(message) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async ValueTask<WorkerMessage?> ReadAsync(StreamReader reader, CancellationToken cancellationToken = default)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader), "The worker reader cannot be null. Worker 读取器不能为空。");
        var line = await reader.ReadLineAsync(cancellationToken);
        return line is null ? null : Deserialize(line);
    }

    public static string SerializePayload<T>(T payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static T DeserializePayload<T>(WorkerMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.PayloadJson))
            throw new WorkerProtocolException("worker.invalid_payload", $"Message '{message.Kind}' requires a payload. 消息“{message.Kind}”需要载荷。");
        try
        {
            return JsonSerializer.Deserialize<T>(message.PayloadJson, JsonOptions)
                ?? throw new WorkerProtocolException("worker.invalid_payload", $"Message '{message.Kind}' payload is null. 消息“{message.Kind}”的载荷为空。");
        }
        catch (JsonException exception)
        {
            throw new WorkerProtocolException("worker.invalid_payload", $"Message '{message.Kind}' payload is invalid. 消息“{message.Kind}”的载荷无效。", exception);
        }
    }
}

public sealed class WorkerMessageWriter : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WorkerMessageWriter(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream), "The worker stream cannot be null. Worker 流不能为空。");
    }

    public async ValueTask WriteAsync(WorkerMessage message, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WorkerProtocolCodec.WriteAsync(_stream, message, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class WorkerProtocolException : Exception
{
    public WorkerProtocolException(string code, string message, Exception? innerException = null)
        : base(message, innerException) => Code = code;

    public string Code { get; }
}
