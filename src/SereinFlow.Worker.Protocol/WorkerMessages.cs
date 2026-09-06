using System.Text;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Worker.Protocol;

public static class WorkerProtocolConstants
{
    public const int Version = SereinFlow.Contracts.WorkerProtocol.Version;
    public const int MaxMessageBytes = 1_048_576;
    public const int MaxPayloadBytes = 896_000;
    public const string ReadyKind = WorkerErrorCodes.Ready;
    public const string HandshakeKind = WorkerErrorCodes.Handshake;
    public const string HandshakeAcceptedKind = WorkerErrorCodes.HandshakeAccepted;
    public const string RunKind = WorkerErrorCodes.Run;
    public const string CancelKind = WorkerErrorCodes.Cancel;
    public const string CancelAcknowledgedKind = WorkerErrorCodes.CancelAck;
    public const string DebugContinueKind = WorkerErrorCodes.DebugContinue;
    public const string DebugStepKind = WorkerErrorCodes.DebugStep;
    public const string DebugStopKind = WorkerErrorCodes.DebugStop;
    public const string HeartbeatKind = WorkerErrorCodes.Heartbeat;
    public const string HeartbeatAcknowledgedKind = WorkerErrorCodes.HeartbeatAck;
    public const string EventKind = WorkerErrorCodes.Event;
    public const string ResultKind = WorkerErrorCodes.Result;
    public const string ErrorKind = WorkerErrorCodes.Error;
    public const string MessageDeliverKind = MessageErrorCodes.Deliver;
    public const string MessageAcceptedKind = MessageErrorCodes.Accepted;
    public const string MessageRejectedKind = MessageErrorCodes.Rejected;
    public const string MessageRegisterKind = MessageErrorCodes.Register;
    public const string MessageUnregisterKind = MessageErrorCodes.Unregister;
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
    public static WorkerMessage Create(
        string kind,
        string? payloadJson = null,
        Guid? runId = null,
        DateTimeOffset? deadline = null,
        long? sequence = null,
        string? requestId = null)
        => new(WorkerProtocolConstants.Version, kind, requestId ?? Guid.NewGuid().ToString("N"), runId, sequence, deadline, payloadJson);
}

public sealed record WorkerErrorDto(string Code, string Message);

public static class WorkerProtocolCodec
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.WriteIndented = false;
    });

    public static string Serialize(WorkerMessage message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message), "The worker message cannot be null. Worker 消息不能为空。");
        if (message.ProtocolVersion != WorkerProtocolConstants.Version)
            throw new WorkerProtocolException(WorkerErrorCodes.ProtocolMismatch, $"Unsupported protocol version '{message.ProtocolVersion}'. 不支持的协议版本“{message.ProtocolVersion}”。");

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > WorkerProtocolConstants.MaxMessageBytes)
            throw new WorkerProtocolException(WorkerErrorCodes.MessageTooLarge, $"Worker messages are limited to {WorkerProtocolConstants.MaxMessageBytes} bytes. Worker 消息大小不能超过 {WorkerProtocolConstants.MaxMessageBytes} 字节。");
        return json;
    }

    public static WorkerMessage Deserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new WorkerProtocolException(WorkerErrorCodes.InvalidMessage, "Worker message cannot be empty. Worker 消息不能为空。");
        if (Encoding.UTF8.GetByteCount(line) > WorkerProtocolConstants.MaxMessageBytes)
            throw new WorkerProtocolException(WorkerErrorCodes.MessageTooLarge, $"Worker messages are limited to {WorkerProtocolConstants.MaxMessageBytes} bytes. Worker 消息大小不能超过 {WorkerProtocolConstants.MaxMessageBytes} 字节。");

        try
        {
            var message = JsonSerializer.Deserialize<WorkerMessage>(line, JsonOptions)
                ?? throw new WorkerProtocolException(WorkerErrorCodes.InvalidMessage, "Worker message payload is null. Worker 消息载荷为空。");
            if (message.ProtocolVersion != WorkerProtocolConstants.Version)
                throw new WorkerProtocolException(WorkerErrorCodes.ProtocolMismatch, $"Unsupported protocol version '{message.ProtocolVersion}'. 不支持的协议版本“{message.ProtocolVersion}”。");
            if (string.IsNullOrWhiteSpace(message.Kind) || string.IsNullOrWhiteSpace(message.RequestId))
                throw new WorkerProtocolException(WorkerErrorCodes.InvalidMessage, "Worker message kind and requestId are required. Worker 消息类型和 requestId 不能为空。");
            if (message.PayloadJson is { Length: > WorkerProtocolConstants.MaxPayloadBytes })
                throw new WorkerProtocolException(WorkerErrorCodes.PayloadTooLarge, $"Worker payloads are limited to {WorkerProtocolConstants.MaxPayloadBytes} characters. Worker 载荷不能超过 {WorkerProtocolConstants.MaxPayloadBytes} 个字符。");
            return message;
        }
        catch (JsonException exception)
        {
            throw new WorkerProtocolException(
                WorkerErrorCodes.InvalidMessage,
                "Worker message is not valid JSON. Worker 消息不是有效的 JSON。",
                exception,
                line,
                exception.LineNumber,
                exception.BytePositionInLine);
        }
    }

    public static string SerializePayload<T>(T payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static T DeserializePayload<T>(WorkerMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.PayloadJson))
            throw new WorkerProtocolException(WorkerErrorCodes.InvalidPayload, $"Message '{message.Kind}' requires a payload. 消息“{message.Kind}”需要载荷。");
        try
        {
            return JsonSerializer.Deserialize<T>(message.PayloadJson, JsonOptions)
                ?? throw new WorkerProtocolException(WorkerErrorCodes.InvalidPayload, $"Message '{message.Kind}' payload is null. 消息“{message.Kind}”的载荷为空。");
        }
        catch (JsonException exception)
        {
            throw new WorkerProtocolException(WorkerErrorCodes.InvalidPayload, $"Message '{message.Kind}' payload is invalid. 消息“{message.Kind}”的载荷无效。", exception);
        }
    }
}

public sealed class WorkerProtocolException : Exception
{
    public WorkerProtocolException(
        string code,
        string message,
        Exception? innerException = null,
        string? rawMessage = null,
        long? jsonLineNumber = null,
        long? jsonBytePositionInLine = null)
        : base(message, innerException)
    {
        Code = code;
        RawMessage = rawMessage;
        JsonLineNumber = jsonLineNumber;
        JsonBytePositionInLine = jsonBytePositionInLine;
    }

    public string Code { get; }

    public string? RawMessage { get; }

    public long? JsonLineNumber { get; }

    public long? JsonBytePositionInLine { get; }
}
