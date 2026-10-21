using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Application-layer command for delivering a JSON message to an active run.
/// 应用层向活动运行投递 JSON 消息的命令。
/// </summary>
public sealed record RunMessageDeliveryCommand(Guid RunId, string Topic, JsonElement Payload, string? MessageId = null, string? IdempotencyKey = null, string? ContractId = null, WorkerMessageChannelKindDto ChannelKind = WorkerMessageChannelKindDto.Queue);
/// <summary>
/// Describes the stable application outcome of a run-message delivery.
/// 描述运行消息投递的稳定应用层结果。
/// </summary>
public enum RunMessageDeliveryDisposition
{
    /// <summary>
    /// Indicates the accepted value.
    /// 表示“已接收”这一取值。
    /// </summary>
    Accepted,
    /// <summary>
    /// Indicates the invalid request value.
    /// 表示“无效请求”这一取值。
    /// </summary>
    InvalidRequest,
    /// <summary>
    /// Indicates the run not found value.
    /// 表示“运行NotFound”这一取值。
    /// </summary>
    RunNotFound,
    /// <summary>
    /// Indicates the worker not active value.
    /// 表示“WorkerNotActive”这一取值。
    /// </summary>
    WorkerNotActive,
    /// <summary>
    /// Indicates the worker not found value.
    /// 表示“WorkerNotFound”这一取值。
    /// </summary>
    WorkerNotFound,
    /// <summary>
    /// Indicates the endpoint not ready value.
    /// 表示“端点Not就绪”这一取值。
    /// </summary>
    EndpointNotReady,
    /// <summary>
    /// Indicates the endpoint forbidden value.
    /// 表示“端点无权限”这一取值。
    /// </summary>
    EndpointForbidden,
    /// <summary>
    /// Indicates the channel full value.
    /// 表示“通道已满”这一取值。
    /// </summary>
    ChannelFull,
    /// <summary>
    /// Indicates the timed out value.
    /// 表示“TimedOut”这一取值。
    /// </summary>
    TimedOut,
    /// <summary>
    /// Indicates the rejected value.
    /// 表示“已拒绝”这一取值。
    /// </summary>
    Rejected,
}

/// <summary>
/// Captures the outcome of run message delivery operations.
/// 记录运行消息投递操作的结果。
/// </summary>
public sealed record RunMessageDeliveryResult(RunMessageDeliveryDisposition Disposition, WorkerMessageDeliveryResponseDto? Response, string? ErrorCode, string Message, int StatusCode)
{
    public bool IsAccepted => Disposition == RunMessageDeliveryDisposition.Accepted;
}

public interface IRunMessageDeliveryService
{
    Task<RunMessageDeliveryResult> DeliverAsync(RunMessageDeliveryCommand command, CancellationToken cancellationToken = default);
}
