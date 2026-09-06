using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Api;

/// <summary>
/// Shared REST/MCP use case for delivering JSON messages to active Worker runs.
/// REST 与 MCP 共用的活动 Worker 运行 JSON 消息投递用例。
/// </summary>
public sealed class RunMessageDeliveryService : IRunMessageDeliveryService
{
    private readonly IFlowRunStore _runs;
    private readonly IWorkerMessageRunClient _messageClient;

    public RunMessageDeliveryService(
        IFlowRunStore runs,
        IWorkerMessageRunClient messageClient)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _messageClient = messageClient ?? throw new ArgumentNullException(nameof(messageClient));
    }

    public async Task<RunMessageDeliveryResult> DeliverAsync(
        RunMessageDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        var run = await _runs.FindAsync(command.RunId, cancellationToken);
        if (run is null)
            return Failure(RunMessageDeliveryDisposition.RunNotFound, RunErrorCodes.NotFound, "Run not found. 未找到运行实例。", 404);

        if (run.IsTerminal || run.Status != FlowRunStatus.Running)
        {
            return Failure(
                RunMessageDeliveryDisposition.WorkerNotActive,
                WorkerErrorCodes.NotActive,
                "The Worker run is not active. Worker 运行当前不活动。",
                409);
        }

        var topic = command.Topic?.Trim();
        if (string.IsNullOrWhiteSpace(topic) || topic.Length > 256 || topic.Contains('\r') || topic.Contains('\n'))
        {
            return Failure(
                RunMessageDeliveryDisposition.InvalidRequest,
                MessageErrorCodes.TopicInvalid,
                "The message topic is invalid. 消息主题无效。",
                400);
        }

        if (command.Payload.ValueKind == JsonValueKind.Undefined)
        {
            return Failure(
                RunMessageDeliveryDisposition.InvalidRequest,
                MessageErrorCodes.PayloadRequired,
                "A JSON message payload is required. 必须提供 JSON 消息载荷。",
                400);
        }

        if (!Enum.IsDefined(command.ChannelKind))
        {
            return Failure(
                RunMessageDeliveryDisposition.InvalidRequest,
                MessageErrorCodes.ChannelInvalid,
                "The message channel kind is invalid. 消息通道类型无效。",
                400);
        }

        var messageIdResult = ResolveMessageId(
            command.RunId,
            topic,
            command.MessageId,
            command.IdempotencyKey);
        if (!messageIdResult.IsSuccess)
        {
            return Failure(
                RunMessageDeliveryDisposition.InvalidRequest,
                MessageErrorCodes.IdInvalid,
                "MessageId must be a valid GUID. MessageId 必须是有效 GUID。",
                400);
        }

        WorkerMessageDeliveryDto delivery;
        try
        {
            delivery = new WorkerMessageDeliveryDto(
                WorkerProtocol.Version,
                command.RunId,
                messageIdResult.MessageId,
                topic,
                command.ChannelKind,
                WorkerMessageSerializationModeDto.Json,
                command.ContractId,
                JsonSerializer.Serialize(command.Payload, SereinJsonSerialization.CreateWebOptions()),
                DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Failure(
                RunMessageDeliveryDisposition.InvalidRequest,
                MessageErrorCodes.PayloadInvalid,
                "The message payload is not valid JSON. 消息载荷不是有效 JSON。",
                400);
        }

        var response = await _messageClient.DeliverMessageAsync(delivery, cancellationToken);
        return MapWorkerResponse(response);
    }

    internal static MessageIdResolution ResolveMessageId(
        Guid runId,
        string topic,
        string? requestedMessageId,
        string? idempotencyKey)
    {
        if (requestedMessageId is not null)
        {
            return Guid.TryParse(requestedMessageId, out var parsed) && parsed != Guid.Empty
                ? new MessageIdResolution(true, parsed)
                : new MessageIdResolution(false, Guid.Empty);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return new MessageIdResolution(true, Guid.NewGuid());
        if (Guid.TryParse(idempotencyKey, out var idempotencyGuid) && idempotencyGuid != Guid.Empty)
            return new MessageIdResolution(true, idempotencyGuid);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{runId:D}:{topic}:{idempotencyKey}"));
        return new MessageIdResolution(true, new Guid(bytes.AsSpan(0, 16)));
    }

    private static RunMessageDeliveryResult MapWorkerResponse(WorkerMessageDeliveryResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var code = response.Code;
        var message = GetSafeMessage(code, response.Status);
        var normalizedResponse = response.Status == WorkerMessageDeliveryStatusDto.Accepted
            ? response
            : response with { Code = code ?? MessageErrorCodes.Rejected, Message = message };

        return response.Status switch
        {
            WorkerMessageDeliveryStatusDto.Accepted => new(
                RunMessageDeliveryDisposition.Accepted,
                normalizedResponse,
                null,
                response.Message ?? "The message was accepted. 消息已接受。",
                202),
            WorkerMessageDeliveryStatusDto.NotFound when string.Equals(code, WorkerErrorCodes.NotActive, StringComparison.Ordinal)
                => new(RunMessageDeliveryDisposition.WorkerNotFound, normalizedResponse, code, message, 404),
            WorkerMessageDeliveryStatusDto.NotFound
                => new(RunMessageDeliveryDisposition.WorkerNotFound, normalizedResponse, code ?? WorkerErrorCodes.NotFound, message, 404),
            WorkerMessageDeliveryStatusDto.NotReady
                => new(RunMessageDeliveryDisposition.EndpointNotReady, normalizedResponse, code ?? MessageErrorCodes.EndpointNotReady, message, 409),
            WorkerMessageDeliveryStatusDto.TimedOut
                => new(RunMessageDeliveryDisposition.TimedOut, normalizedResponse, code ?? MessageErrorCodes.DeliveryTimeout, message, 504),
            _ when string.Equals(code, MessageErrorCodes.EndpointForbidden, StringComparison.Ordinal)
                => new(RunMessageDeliveryDisposition.EndpointForbidden, normalizedResponse, code, message, 403),
            _ when string.Equals(code, MessageErrorCodes.ChannelFull, StringComparison.Ordinal)
                => new(RunMessageDeliveryDisposition.ChannelFull, normalizedResponse, code, message, 429),
            _ => new(RunMessageDeliveryDisposition.Rejected, normalizedResponse, code ?? MessageErrorCodes.Rejected, message, 400),
        };
    }

    private static string GetSafeMessage(string? code, WorkerMessageDeliveryStatusDto status)
        => code switch
        {
            MessageErrorCodes.EndpointNotReady => "The message endpoint is not ready. 消息入口尚未就绪。",
            MessageErrorCodes.EndpointForbidden => "The message endpoint is not open to external ingress. 消息入口未开放外部投递。",
            MessageErrorCodes.ContractMismatch => "The message contract does not match the registered endpoint. 消息合同与已注册入口不匹配。",
            MessageErrorCodes.ChannelFull => "The message channel is full. 消息通道已满。",
            MessageErrorCodes.DeliveryTimeout => "The Worker did not acknowledge the message before the delivery timeout. Worker 未在投递超时前确认消息。",
            MessageErrorCodes.ChannelInvalid => "The message channel kind is invalid. 消息通道类型无效。",
            MessageErrorCodes.ExternalJsonRequired or MessageErrorCodes.EndpointJsonRequired => "External ingress only accepts JSON messages. 外部入口只接受 JSON 消息。",
            MessageErrorCodes.PayloadInvalid => "The message payload is not valid JSON. 消息载荷不是有效 JSON。",
            MessageErrorCodes.PayloadTooLarge => "The message payload is too large. 消息载荷过大。",
            MessageErrorCodes.Expired => "The message has expired. 消息已过期。",
            WorkerErrorCodes.NotFound or WorkerErrorCodes.TransportClosed => "The Worker run is not active in the Supervisor. Supervisor 中不存在活动 Worker 会话。",
            _ when status == WorkerMessageDeliveryStatusDto.NotFound => "The Worker run is not active in the Supervisor. Supervisor 中不存在活动 Worker 会话。",
            _ => "The message was rejected. 消息被拒绝。",
        };

    private static RunMessageDeliveryResult Failure(
        RunMessageDeliveryDisposition disposition,
        string code,
        string message,
        int statusCode)
        => new(disposition, null, code, message, statusCode);

    internal readonly record struct MessageIdResolution(bool IsSuccess, Guid MessageId);
}
