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
    public RunMessageDeliveryService(IFlowRunStore runs, IWorkerMessageRunClient messageClient)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _messageClient = messageClient ?? throw new ArgumentNullException(nameof(messageClient));
    }

    public async Task<RunMessageDeliveryResult> DeliverAsync(RunMessageDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        var run = await _runs.FindAsync(command.RunId, cancellationToken);
        if (run is null)
        {
            var failureResult = Failure(RunMessageDeliveryDisposition.RunNotFound, RunErrorCodes.NotFound, "Run not found. 未找到运行实例。", 404);
            return failureResult;
        }

        if (run.IsTerminal || run.Status != FlowRunStatus.Running)
        {
            var failureResult1 = Failure(RunMessageDeliveryDisposition.WorkerNotActive, WorkerErrorCodes.NotActive, "The Worker run is not active. Worker 运行当前不活动。", 409);
            return failureResult1;
        }

        var topic = command.Topic?.Trim();
        if (string.IsNullOrWhiteSpace(topic) || topic.Length > 256 || topic.Contains('\r') || topic.Contains('\n'))
        {
            var failureResult2 = Failure(RunMessageDeliveryDisposition.InvalidRequest, MessageErrorCodes.TopicInvalid, "The message topic is invalid. 消息主题无效。", 400);
            return failureResult2;
        }

        if (command.Payload.ValueKind == JsonValueKind.Undefined)
        {
            var failureResult3 = Failure(RunMessageDeliveryDisposition.InvalidRequest, MessageErrorCodes.PayloadRequired, "A JSON message payload is required. 必须提供 JSON 消息载荷。", 400);
            return failureResult3;
        }

        if (!Enum.IsDefined(command.ChannelKind))
        {
            var failureResult4 = Failure(RunMessageDeliveryDisposition.InvalidRequest, MessageErrorCodes.ChannelInvalid, "The message channel kind is invalid. 消息通道类型无效。", 400);
            return failureResult4;
        }

        var messageIdResult = ResolveMessageId(command.RunId, topic, command.MessageId, command.IdempotencyKey);
        if (!messageIdResult.IsSuccess)
        {
            var failureResult5 = Failure(RunMessageDeliveryDisposition.InvalidRequest, MessageErrorCodes.IdInvalid, "MessageId must be a valid GUID. MessageId 必须是有效 GUID。", 400);
            return failureResult5;
        }

        WorkerMessageDeliveryDto delivery;
        try
        {
            var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
            var serializeResult = JsonSerializer.Serialize(command.Payload, createWebOptionsResult);
            delivery = new WorkerMessageDeliveryDto(WorkerProtocol.Version, command.RunId, messageIdResult.MessageId, topic, command.ChannelKind, WorkerMessageSerializationModeDto.Json, command.ContractId, serializeResult, DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            var failureResult6 = Failure(RunMessageDeliveryDisposition.InvalidRequest, MessageErrorCodes.PayloadInvalid, "The message payload is not valid JSON. 消息载荷不是有效 JSON。", 400);
            return failureResult6;
        }

        var response = await _messageClient.DeliverMessageAsync(delivery, cancellationToken);
        var mapWorkerResponseResult = MapWorkerResponse(response);
        return mapWorkerResponseResult;
    }

    internal static MessageIdResolution ResolveMessageId(Guid runId, string topic, string? requestedMessageId, string? idempotencyKey)
    {
        if (requestedMessageId is not null)
        {
            RunMessageDeliveryService.MessageIdResolution evaluatedValue = Guid.TryParse(requestedMessageId, out var parsed) && parsed != Guid.Empty ? new MessageIdResolution(true, parsed) : new MessageIdResolution(false, Guid.Empty);
            return evaluatedValue;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var newGuidResult = Guid.NewGuid();
            var messageIdResolution = new MessageIdResolution(true, newGuidResult);
            return messageIdResolution;
        }

        if (Guid.TryParse(idempotencyKey, out var idempotencyGuid) && idempotencyGuid != Guid.Empty)
        {
            var messageIdResolution7 = new MessageIdResolution(true, idempotencyGuid);
            return messageIdResolution7;
        }

        var getBytesResult = Encoding.UTF8.GetBytes($"{runId:D}:{topic}:{idempotencyKey}");
        var bytes = SHA256.HashData(getBytesResult);
        var asSpanResult = bytes.AsSpan(0, 16);
        var guid = new Guid(asSpanResult);
        var messageIdResolution8 = new MessageIdResolution(true, guid);
        return messageIdResolution8;
    }

    private static RunMessageDeliveryResult MapWorkerResponse(WorkerMessageDeliveryResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var code = response.Code;
        var message = GetSafeMessage(code, response.Status);
        var normalizedResponse = response.Status == WorkerMessageDeliveryStatusDto.Accepted ? response : response with
        {
            Code = code ?? MessageErrorCodes.Rejected,
            Message = message
        };
        RunMessageDeliveryResult evaluatedValue9 = response.Status switch
        {
            WorkerMessageDeliveryStatusDto.Accepted => new(RunMessageDeliveryDisposition.Accepted, normalizedResponse, null, response.Message ?? "The message was accepted. 消息已接受。", 202),
            WorkerMessageDeliveryStatusDto.NotFound when string.Equals(code, WorkerErrorCodes.NotActive, StringComparison.Ordinal) => new(RunMessageDeliveryDisposition.WorkerNotFound, normalizedResponse, code, message, 404),
            WorkerMessageDeliveryStatusDto.NotFound => new(RunMessageDeliveryDisposition.WorkerNotFound, normalizedResponse, code ?? WorkerErrorCodes.NotFound, message, 404),
            WorkerMessageDeliveryStatusDto.NotReady => new(RunMessageDeliveryDisposition.EndpointNotReady, normalizedResponse, code ?? MessageErrorCodes.EndpointNotReady, message, 409),
            WorkerMessageDeliveryStatusDto.TimedOut => new(RunMessageDeliveryDisposition.TimedOut, normalizedResponse, code ?? MessageErrorCodes.DeliveryTimeout, message, 504),
            _ when string.Equals(code, MessageErrorCodes.EndpointForbidden, StringComparison.Ordinal) => new(RunMessageDeliveryDisposition.EndpointForbidden, normalizedResponse, code, message, 403),
            _ when string.Equals(code, MessageErrorCodes.ChannelFull, StringComparison.Ordinal) => new(RunMessageDeliveryDisposition.ChannelFull, normalizedResponse, code, message, 429),
            _ => new(RunMessageDeliveryDisposition.Rejected, normalizedResponse, code ?? MessageErrorCodes.Rejected, message, 400),
        };
        return evaluatedValue9;
    }

    private static string GetSafeMessage(string? code, WorkerMessageDeliveryStatusDto status) => code switch
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
    private static RunMessageDeliveryResult Failure(RunMessageDeliveryDisposition disposition, string code, string message, int statusCode)
    {
        RunMessageDeliveryResult evaluatedValue10 = new(disposition, null, code, message, statusCode);
        return evaluatedValue10;
    }

    internal readonly record struct MessageIdResolution(bool IsSuccess, Guid MessageId);
}
