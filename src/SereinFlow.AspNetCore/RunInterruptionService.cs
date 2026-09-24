using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Api;

public enum RunInterruptionDisposition
{
    Interrupted,
    NotFound,
    AlreadyTerminal,
    NotRunning,
    SaveFailed
}

public sealed record RunInterruptionResult(
    RunInterruptionDisposition Disposition,
    FlowRun? Run = null)
{
    public bool IsInterrupted => Disposition == RunInterruptionDisposition.Interrupted;
}

/// <summary>
/// Turns an orphaned running instance into an auditable terminal state. Worker
/// processes have no durable execution checkpoint, so they cannot be attached
/// again after the API process has restarted.
/// 将孤儿运行实例转换为可审计的终态。Worker 进程没有持久化执行检查点，API 进程重启后不能重新接管它。
/// </summary>
public sealed class RunInterruptionService
{
    // Reconciliation and manual interruption are exceptional control-plane
    // operations. A short global critical section prevents duplicate audit
    // events without retaining one lock object for every historical run.
    // 对账和人工中断都属于低频控制面操作。短暂的全局临界区可避免重复审计事件，且无需为每个历史运行实例长期保留锁对象。
    private static readonly SemaphoreSlim InterruptionGate = new(1, 1);
    private static readonly Action<ILogger, Guid, Exception?> SaveFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4201, "InterruptedRunSaveFailed"),
        "Interrupted flow run {RunId} could not be saved because it no longer exists. 已中断的流程实例无法保存，实例可能已不存在。");
    private static readonly Action<ILogger, Guid, Exception?> AuditAppendFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(4202, "InterruptedRunAuditAppendFailed"),
        "Interrupted flow run {RunId} could not append its audit event. 已中断流程实例无法追加审计事件。");
    private static readonly Action<ILogger, Guid, Exception?> BroadcastFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4203, "InterruptedRunBroadcastFailed"),
        "Interrupted run event broadcast failed for {RunId}. 中断运行事件广播失败。");
    private static readonly Action<ILogger, Guid, Exception?> SignalRPushFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4204, "InterruptedRunSignalRPushFailed"),
        "Interrupted SignalR event push failed for {RunId}. 中断 SignalR 事件推送失败。");

    private readonly IFlowRunStore _runStore;
    private readonly IFlowRunEventStore _eventStore;
    private readonly RunEventBroadcaster _broadcaster;
    private readonly IHubContext<RunEventsHub> _hub;
    private readonly ILogger<RunInterruptionService> _logger;

    public RunInterruptionService(
        IFlowRunStore runStore,
        IFlowRunEventStore eventStore,
        RunEventBroadcaster broadcaster,
        IHubContext<RunEventsHub> hub,
        ILogger<RunInterruptionService> logger)
    {
        _runStore = runStore;
        _eventStore = eventStore;
        _broadcaster = broadcaster;
        _hub = hub;
        _logger = logger;
    }

    public async Task<RunInterruptionResult> InterruptAsync(
        Guid runId,
        string reason,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "The run ID cannot be empty. 运行实例 ID 不能为空。",
                nameof(runId));
        }

        await InterruptionGate.WaitAsync(cancellationToken);
        try
        {
            var run = await _runStore.FindAsync(runId, cancellationToken);
            if (run is null)
                return new RunInterruptionResult(RunInterruptionDisposition.NotFound);
            if (run.IsTerminal)
                return new RunInterruptionResult(RunInterruptionDisposition.AlreadyTerminal, run);
            if (run.Status != FlowRunStatus.Running)
                return new RunInterruptionResult(RunInterruptionDisposition.NotRunning, run);

            var interruptedAt = DateTimeOffset.UtcNow;
            run.Interrupt(reason, errorMessage, interruptedAt);
            if (!await _runStore.SaveAsync(run, cancellationToken))
            {
                SaveFailedLog(_logger, runId, null);
                return new RunInterruptionResult(RunInterruptionDisposition.SaveFailed);
            }

            var sequence = await _eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1;
            var payload = JsonSerializer.Serialize(
                new
                {
                    status = FlowRunStatusDto.Interrupted,
                    errorCode,
                    errorMessage,
                    reason = run.CancellationReason,
                    lastKnownSequence = sequence - 1,
                    interruptedAt
                },
                SereinJsonSerialization.CreateWebOptions());
            var item = new FlowRunEvent(
                run.Id,
                sequence,
                interruptedAt,
                RunErrorCodes.Interrupted,
                null,
                payload);

            try
            {
                await _eventStore.AppendAsync([item], cancellationToken);
            }
            catch (Exception exception)
            {
                // The state must remain terminal even when an audit storage
                // outage occurs. It must never appear runnable again.
                // 审计写入故障时状态仍必须保持终态，绝不能再次表现为可运行。
                AuditAppendFailedLog(_logger, run.Id, exception);
                throw;
            }

            var dto = new FlowRunEventDto(
                item.RunId,
                item.Sequence,
                item.Timestamp,
                item.Type,
                item.NodeId,
                item.PayloadJson);
            await PublishLiveAsync(dto);
            return new RunInterruptionResult(RunInterruptionDisposition.Interrupted, run);
        }
        finally
        {
            InterruptionGate.Release();
        }
    }

    private async Task PublishLiveAsync(FlowRunEventDto item)
    {
        try
        {
            await _broadcaster.PublishAsync(item);
        }
        catch (Exception exception)
        {
            BroadcastFailedLog(_logger, item.RunId, exception);
        }

        try
        {
            await _hub.Clients.Group($"run:{item.RunId:D}").SendAsync("runEvent", item, CancellationToken.None);
        }
        catch (Exception exception)
        {
            SignalRPushFailedLog(_logger, item.RunId, exception);
        }
    }
}
