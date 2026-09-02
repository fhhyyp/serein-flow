using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Application-layer command for delivering a JSON message to an active run.
/// 应用层向活动运行投递 JSON 消息的命令。
/// </summary>
public sealed record RunMessageDeliveryCommand(
    Guid RunId,
    string Topic,
    JsonElement Payload,
    string? MessageId = null,
    string? IdempotencyKey = null,
    string? ContractId = null,
    WorkerMessageChannelKindDto ChannelKind = WorkerMessageChannelKindDto.Queue);

/// <summary>
/// Describes the stable application outcome of a run-message delivery.
/// 描述运行消息投递的稳定应用层结果。
/// </summary>
public enum RunMessageDeliveryDisposition
{
    Accepted,
    InvalidRequest,
    RunNotFound,
    WorkerNotActive,
    WorkerNotFound,
    EndpointNotReady,
    EndpointForbidden,
    ChannelFull,
    TimedOut,
    Rejected,
}

public sealed record RunMessageDeliveryResult(
    RunMessageDeliveryDisposition Disposition,
    WorkerMessageDeliveryResponseDto? Response,
    string? ErrorCode,
    string Message,
    int StatusCode)
{
    public bool IsAccepted => Disposition == RunMessageDeliveryDisposition.Accepted;
}

public interface IRunMessageDeliveryService
{
    Task<RunMessageDeliveryResult> DeliverAsync(
        RunMessageDeliveryCommand command,
        CancellationToken cancellationToken = default);
}
