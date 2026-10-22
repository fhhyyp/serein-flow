using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;
/// <summary>
/// Owns API-side debug handles while keeping all executable code inside the
/// Worker process. The durable session record is intentionally smaller than a
/// Worker session so it can be reconciled safely after an API restart.
/// 在 API 侧持有调试句柄，同时将所有可执行代码保留在 Worker 进程内。持久化会话记录
/// 被刻意设计得比 Worker 会话更小，因此 API 重启后可安全对账。
/// </summary>
public sealed class FlowDebugSessionService : IHostedService, IFlowDebugSessionService
{
    private const string DebugStoppedMessage = "The debug session was stopped. 调试会话已停止。";
    private static readonly Action<ILogger, Guid, Exception?> DebugWorkerStartupFailedLog = CreateDebugWorkerStartupFailedLogInitialValue();
    private static readonly Action<ILogger, Exception?> DebugWorkerShutdownFailedLog = CreateDebugWorkerShutdownFailedLogInitialValue();
    private static readonly Action<ILogger, Guid, Exception?> DebugWorkerMonitoringFailedLog = CreateDebugWorkerMonitoringFailedLogInitialValue();
    private static readonly Action<ILogger, Guid, Exception?> DebugCompletionPersistenceFailedLog = CreateDebugCompletionPersistenceFailedLogInitialValue();
    private static readonly Action<ILogger, Guid, Exception?> LiveDebugBroadcastFailedLog = CreateLiveDebugBroadcastFailedLogInitialValue();
    private static readonly Action<ILogger, Guid, Exception?> DebugSignalRPushFailedLog = CreateDebugSignalRPushFailedLogInitialValue();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerDebugRunClient _workerClient;
    private readonly RunExecutionQueue _queue;
    private readonly RunEventBroadcaster _broadcaster;
    private readonly IHubContext<RunEventsHub> _hub;
    private readonly ILogger<FlowDebugSessionService> _logger;
    private readonly string _scriptRoot;
    private readonly string _libraryRoot;
    private readonly string _workpieceRoot;
    private readonly ConcurrentDictionary<Guid, ActiveDebugSession> _active = new();
    public FlowDebugSessionService(IServiceScopeFactory scopeFactory, IWorkerDebugRunClient workerClient, RunExecutionQueue queue, RunEventBroadcaster broadcaster, IHubContext<RunEventsHub> hub, ILogger<FlowDebugSessionService> logger, SereinFlow.Infrastructure.Configuration.SereinFlowStorageOptions storage)
    {
        _scopeFactory = scopeFactory;
        _workerClient = workerClient;
        _queue = queue;
        _broadcaster = broadcaster;
        _hub = hub;
        _logger = logger;
        _scriptRoot = storage.ScriptArtifactRoot;
        _libraryRoot = storage.LibraryDirectory;
        _workpieceRoot = storage.WorkpieceDirectory;
    }

    /// <summary>
    /// Creates a debug session and starts its worker run.
    /// 创建调试会话并启动对应的 Worker 运行。
    /// </summary>
    public async Task<FlowDebugSessionStartResult> CreateAsync(Guid projectId, Guid flowId, StartFlowDebugSessionRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var breakpointNodeIds = (request.BreakpointNodeIds ?? [])
            .Where(static nodeId =>
            {
                var evaluatedValue = !string.IsNullOrWhiteSpace(nodeId);
                return evaluatedValue;
            })
            .Select(static nodeId =>
            {
                var trimResult = nodeId.Trim();
                return trimResult;
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        if (request.MaxQueuedFlipflopTriggers is < 0 or > 1_024)
        {
            var flowDebugSessionStartResult = new FlowDebugSessionStartResult(null, StatusCodes.Status422UnprocessableEntity, "The debug trigger queue limit must be between 0 and 1024. 调试触发队列上限必须在 0 到 1024 之间。", new { code = DebugErrorCodes.InvalidTriggerQueueLimit }, DebugErrorCodes.InvalidTriggerQueueLimit);
            return flowDebugSessionStartResult;
        }

        var debugSessionId = Guid.NewGuid();
        using var scope = _scopeFactory.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<RunApplicationService>();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var debugStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var evaluatedValue11 = (await debugStore.ListActiveAsync(cancellationToken));
        var activeForFlow = evaluatedValue11.FirstOrDefault(item => item.ProjectId == projectId && item.FlowId == flowId);
        if (activeForFlow is not null)
        {
            var flowDebugSessionStartResult6 = new FlowDebugSessionStartResult(null, StatusCodes.Status409Conflict, "This flow already has an active debug session. 当前流程已有活动调试会话。", new { code = DebugErrorCodes.SessionAlreadyActive, sessionId = activeForFlow.Id }, DebugErrorCodes.SessionAlreadyActive);
            return flowDebugSessionStartResult6;
        }

        var runFlowRequestDto = new RunFlowRequestDto(request.ExpectedFlowVersion, request.ProjectInputs, request.TimeoutSeconds, request.MaxSteps, request.MaxNodeVisits);
        var preparation = await runService.PrepareAsync(projectId, flowId, runFlowRequestDto, FlowRunExecutionKind.Debug, debugSessionId, breakpointNodeIds, definitionOverride: request.Definition, cancellationToken: cancellationToken);
        if (!preparation.IsSuccess)
        {
            var flowDebugSessionStartResult7 = new FlowDebugSessionStartResult(null, preparation.StatusCode, preparation.ErrorTitle, preparation.ErrorBody);
            return flowDebugSessionStartResult7;
        }

        var prepared = preparation.Preparation!;
        var session = FlowDebugSession.Create(prepared.Run.Id, projectId, flowId, breakpointNodeIds, DateTimeOffset.UtcNow, debugSessionId);
        await debugStore.CreateAsync(session, cancellationToken);
        var item = new RunWorkItem(prepared.Run.Id, projectId, prepared.Definition, prepared.ProjectInputs, prepared.Run.QueuedAt, prepared.TimeoutSeconds, prepared.MaxSteps, prepared.MaxNodeVisits);
        if (!_queue.TryStartDirectExecution(item, out var lease))
        {
            var failure = "No Worker execution slot is currently available for debugging. 当前没有可用于调试的 Worker 执行槽。";
            prepared.Run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, failure);
            await runStore.SaveAsync(prepared.Run, CancellationToken.None);
            session.Fail(failure, DateTimeOffset.UtcNow);
            await debugStore.SaveAsync(session, CancellationToken.None);
            var flowDebugSessionStartResult8 = new FlowDebugSessionStartResult(null, StatusCodes.Status429TooManyRequests, failure, new { code = DebugErrorCodes.ExecutionCapacityFull }, DebugErrorCodes.ExecutionCapacityFull);
            return flowDebugSessionStartResult8;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var addSecondsResult = now.AddSeconds(prepared.TimeoutSeconds);
            prepared.Run.MarkRunning(now, addSecondsResult);
            await runStore.SaveAsync(prepared.Run, cancellationToken);
            session.MarkRunning(now);
            await debugStore.SaveAsync(session, cancellationToken);
            var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
            var serializeResult = JsonSerializer.Serialize(prepared.Definition, createWebOptionsResult);
            var toStringResult = projectId.ToString("D");
            var getLibraryIdsResult = ProjectLibraryService.GetLibraryIds(prepared.Definition);
            var workerDebugOptionsDto = new WorkerDebugOptionsDto(session.Id, session.BreakpointNodeIds, request.MaxQueuedFlipflopTriggers ?? 64);
            var workerRequest = new WorkerRunRequestDto(WorkerProtocol.Version, prepared.Run.Id, prepared.Definition.Id, prepared.Definition.Version, serializeResult, prepared.Run.Deadline!.Value, toStringResult, prepared.ProjectInputs, prepared.MaxSteps, _scriptRoot, _libraryRoot, prepared.MaxNodeVisits, getLibraryIdsResult, workerDebugOptionsDto, _workpieceRoot);
            var stateGate = new SemaphoreSlim(1, 1);
            var delegateWorkerRunEventSink = new DelegateWorkerRunEventSink(async (workerEvent, token) =>
            {
                await stateGate.WaitAsync(token);
                try
                {
                    await HandleWorkerEventAsync(session.Id, workerEvent, token);
                }
                finally
                {
                    stateGate.Release();
                }
            });
            var getCancellationTokenResult = _queue.GetCancellationToken(prepared.Run.Id);
            var handle = await _workerClient.StartDebugAsync(workerRequest, delegateWorkerRunEventSink, getCancellationTokenResult);
            var active = new ActiveDebugSession(handle, lease!, stateGate);
            if (!_active.TryAdd(session.Id, active))
            {
                await handle.DisposeAsync();
                throw new InvalidOperationException("The debug session is already active. 调试会话已处于活动状态。");
            }

            _ = ObserveCompletionAsync(session.Id, active);
            var flowDebugSessionStartResult9 = new FlowDebugSessionStartResult(session, StatusCodes.Status202Accepted);
            return flowDebugSessionStartResult9;
        }
        catch (Exception exception)
        {
            lease!.Dispose();
            var failedRun = await runStore.FindAsync(prepared.Run.Id, CancellationToken.None);
            if (failedRun is not null && !failedRun.IsTerminal)
            {
                failedRun.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, $"Debug Worker startup failed. 调试 Worker 启动失败。 {exception.Message}");
                await runStore.SaveAsync(failedRun, CancellationToken.None);
            }

            var failedSession = await debugStore.FindAsync(session.Id, CancellationToken.None);
            if (failedSession is not null)
            {
                failedSession.Fail($"Debug Worker startup failed. 调试 Worker 启动失败。 {exception.Message}", DateTimeOffset.UtcNow);
                await debugStore.SaveAsync(failedSession, CancellationToken.None);
            }

            DebugWorkerStartupFailedLog(_logger, session.Id, exception);
            var flowDebugSessionStartResult10 = new FlowDebugSessionStartResult(null, StatusCodes.Status503ServiceUnavailable, "The debug Worker could not start. 调试 Worker 无法启动。", new { code = DebugErrorCodes.WorkerStartFailed }, DebugErrorCodes.WorkerStartFailed);
            return flowDebugSessionStartResult10;
        }
    }

    /// <summary>
    /// Finds a debug session by its identifier.
    /// 按标识查找调试会话。
    /// </summary>
    public async Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var findAsyncResult = await store.FindAsync(sessionId, cancellationToken);
        return findAsyncResult;
    }

    /// <summary>
    /// Waits for the debug session state to advance beyond a revision.
    /// 等待调试会话状态推进到指定修订号之后。
    /// </summary>
    public async Task<FlowDebugStateWaitResult> WaitForChangeAsync(Guid sessionId, long afterRevision, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var waitAsyncResult = await FlowDebugSessionWaiter.WaitAsync(store, sessionId, afterRevision, timeout, cancellationToken);
        return waitAsyncResult;
    }

    /// <summary>
    /// Continues a paused debug session.
    /// 继续执行已暂停的调试会话。
    /// </summary>
    public Task<FlowDebugSessionCommandResult> ContinueAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
    {
        var sendCommandAsyncResult = SendCommandAsync(sessionId, commandSequence, static (handle, sequence, token) =>
        {
            var continueAsyncResult = handle.ContinueAsync(sequence, token);
            return continueAsyncResult;
        }, requirePaused: true, cancelRun: false, cancellationToken: cancellationToken);
        return sendCommandAsyncResult;
    }

    /// <summary>
    /// Advances a paused debug session by one step.
    /// 将已暂停的调试会话推进一步。
    /// </summary>
    public Task<FlowDebugSessionCommandResult> StepAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
    {
        var sendCommandAsyncResult12 = SendCommandAsync(sessionId, commandSequence, static (handle, sequence, token) =>
        {
            var stepAsyncResult = handle.StepAsync(sequence, token);
            return stepAsyncResult;
        }, requirePaused: true, cancelRun: false, cancellationToken: cancellationToken);
        return sendCommandAsyncResult12;
    }

    /// <summary>
    /// Stops a debug session.
    /// 停止调试会话。
    /// </summary>
    public Task<FlowDebugSessionCommandResult> StopAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
    {
        var sendCommandAsyncResult13 = SendCommandAsync(sessionId, commandSequence, static (handle, sequence, token) =>
        {
            var stopAsyncResult = handle.StopAsync(sequence, token);
            return stopAsyncResult;
        }, requirePaused: false, cancelRun: true, cancellationToken: cancellationToken);
        return sendCommandAsyncResult13;
    }

    /// <summary>
    /// Starts background maintenance for persisted debug sessions.
    /// 启动持久化调试会话的后台维护。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var debugStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var sessions = await debugStore.ListActiveAsync(cancellationToken);
        foreach (var session in sessions)
        {
            var now = DateTimeOffset.UtcNow;
            const string message = "The API process restarted before the debug Worker could be reattached. API 进程重启，无法重新接管调试 Worker。";
            session.Fail(message, now);
            await debugStore.SaveAsync(session, cancellationToken);
            var run = await runStore.FindAsync(session.RunId, cancellationToken);
            if (run is null || run.IsTerminal)
            {
                continue;
            }

            if (run.Status == FlowRunStatus.Running)
            {
                run.Interrupt(DebugErrorCodes.ApiRestart, message, now);
            }
            else
            {
                run.Complete(FlowRunStatus.Failed, now, message);
            }

            await runStore.SaveAsync(run, cancellationToken);
            var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Failed, DebugErrorCodes.ApiRestart, message);
            await PublishTerminalAsync(run, workerRunResultDto, eventStore, cancellationToken);
        }
    }

    /// <summary>
    /// Stops background debug session maintenance.
    /// 停止调试会话后台维护。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var active = _active.Values.ToArray();
        foreach (var item in active)
        {
            try
            {
                await item.Handle.DisposeAsync();
            }
            catch (Exception exception)
            {
                DebugWorkerShutdownFailedLog(_logger, exception);
            }
        }
    }

    private async Task<FlowDebugSessionCommandResult> SendCommandAsync(Guid sessionId, long commandSequence, Func<IWorkerDebugRunHandle, long, CancellationToken, Task> send, bool requirePaused, bool cancelRun, CancellationToken cancellationToken)
    {
        if (commandSequence < 1)
        {
            var flowDebugSessionCommandResult = new FlowDebugSessionCommandResult(StatusCodes.Status400BadRequest, "The debug command sequence must be positive. 调试命令序号必须为正数。", DebugErrorCodes.InvalidCommandSequence);
            return flowDebugSessionCommandResult;
        }

        if (!_active.TryGetValue(sessionId, out var active))
        {
            var flowDebugSessionCommandResult14 = new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is not active. 调试会话当前未处于活动状态。", DebugErrorCodes.SessionNotActive);
            return flowDebugSessionCommandResult14;
        }

        await active.CommandGate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
            var session = await store.FindAsync(sessionId, cancellationToken);
            if (session is null)
            {
                var flowDebugSessionCommandResult15 = new FlowDebugSessionCommandResult(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。", DebugErrorCodes.SessionNotFound);
                return flowDebugSessionCommandResult15;
            }

            if (session.IsTerminal)
            {
                var flowDebugSessionCommandResult16 = new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is already complete. 调试会话已经完成。", DebugErrorCodes.SessionTerminal);
                return flowDebugSessionCommandResult16;
            }

            if (commandSequence <= session.LastCommandSequence)
            {
                var flowDebugSessionCommandResult17 = new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。", DebugErrorCodes.CommandSequenceConflict);
                return flowDebugSessionCommandResult17;
            }

            if (requirePaused && session.Status != FlowDebugSessionStatus.Paused)
            {
                var flowDebugSessionCommandResult18 = new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is not paused. 调试会话当前未暂停。", DebugErrorCodes.SessionNotPaused);
                return flowDebugSessionCommandResult18;
            }

            if (cancelRun)
            {
                // Persist the accepted stop before triggering Worker shutdown.
                // Completion can race the command path; persisting first keeps
                // the terminal transition from being overwritten by a stale
                // Paused/Running session instance.
                // 先持久化已接受的停止命令，再触发 Worker 关闭。完成事件可能与命令路径
                // 并发，先写入可避免旧的 Paused/Running 会话对象覆盖终态。
                session.AcceptCommand(commandSequence, DateTimeOffset.UtcNow);
                await store.SaveAsync(session, cancellationToken);
                try
                {
                    await send(active.Handle, commandSequence, cancellationToken);
                }
                finally
                {
                    _queue.Cancel(session.RunId, DebugErrorCodes.Stop);
                }

                var flowDebugSessionCommandResult19 = new FlowDebugSessionCommandResult(StatusCodes.Status202Accepted, Session: session);
                return flowDebugSessionCommandResult19;
            }

            await send(active.Handle, commandSequence, cancellationToken);
            session.AcceptCommand(commandSequence, DateTimeOffset.UtcNow);
            if (requirePaused && session.Status == FlowDebugSessionStatus.Paused)
            {
                session.Resume(DateTimeOffset.UtcNow);
            }

            await store.SaveAsync(session, cancellationToken);
            var flowDebugSessionCommandResult20 = new FlowDebugSessionCommandResult(StatusCodes.Status202Accepted, Session: session);
            return flowDebugSessionCommandResult20;
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("strictly increasing", StringComparison.OrdinalIgnoreCase))
        {
            var flowDebugSessionCommandResult21 = new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。", DebugErrorCodes.CommandSequenceConflict);
            return flowDebugSessionCommandResult21;
        }
        catch (InvalidOperationException exception)
        {
            var flowDebugSessionCommandResult22 = new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, exception.Message, DebugErrorCodes.CommandRejected);
            return flowDebugSessionCommandResult22;
        }
        finally
        {
            active.CommandGate.Release();
        }
    }

    private async Task ObserveCompletionAsync(Guid sessionId, ActiveDebugSession active)
    {
        WorkerRunResultDto result;
        try
        {
            result = await active.Handle.Completion;
        }
        catch (Exception exception)
        {
            DebugWorkerMonitoringFailedLog(_logger, sessionId, exception);
            result = new WorkerRunResultDto(WorkerProtocol.Version, active.Handle.RunId, FlowRunStatusDto.Failed, DebugErrorCodes.WorkerMonitorFailed, exception.Message);
        }

        await active.CommandGate.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var debugStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
            var run = await runStore.FindAsync(result.RunId, CancellationToken.None);
            if (run is not null && !run.IsTerminal)
            {
                if (result.Status == FlowRunStatusDto.Cancelled)
                {
                    run.Cancel(DebugErrorCodes.Stop, DateTimeOffset.UtcNow);
                }
                else
                {
                    var toDomainStatusResult = ToDomainStatus(result.Status);
                    run.Complete(toDomainStatusResult, DateTimeOffset.UtcNow, result.ErrorMessage);
                }

                await PublishTerminalAsync(run, result, eventStore, CancellationToken.None);
                await runStore.SaveAsync(run, CancellationToken.None);
            }

            var session = await debugStore.FindAsync(sessionId, CancellationToken.None);
            if (session is not null)
            {
                var toDomainStatusResult23 = ToDomainStatus(result.Status);
                session.Complete(toDomainStatusResult23, result.ErrorMessage, DateTimeOffset.UtcNow);
                await debugStore.SaveAsync(session, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            DebugCompletionPersistenceFailedLog(_logger, sessionId, exception);
        }
        finally
        {
            active.CommandGate.Release();
            _active.TryRemove(sessionId, out _);
            active.Lease.Dispose();
            await active.Handle.DisposeAsync();
        }
    }

    private async ValueTask HandleWorkerEventAsync(Guid sessionId, WorkerEventEnvelopeDto workerEvent, CancellationToken cancellationToken)
    {
        if (workerEvent.ProtocolVersion != WorkerProtocol.Version || workerEvent.RunId == Guid.Empty)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var outputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
        var debugStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var session = await debugStore.FindAsync(sessionId, cancellationToken);
        if (session is null || session.RunId != workerEvent.RunId)
        {
            return;
        }

        if (await runStore.FindAsync(workerEvent.RunId, cancellationToken) is null)
        {
            return;
        }

        var lastSequence = await eventStore.GetLastSequenceAsync(workerEvent.RunId, cancellationToken);
        if (workerEvent.Sequence <= lastSequence)
        {
            return;
        }

        if (workerEvent.EventType == WorkerEventType.DebugPaused && !session.IsTerminal)
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            var invocationId = ReadPayloadGuid(payload.RootElement, "triggerInvocationId");
            var executionId = ReadPayloadGuid(payload.RootElement, "executionId");
            var evaluatedValue24 = ReadPayloadString(payload.RootElement, "nodeType") ?? string.Empty;
            var readPayloadIntResult = ReadPayloadInt(payload.RootElement, "step");
            var readPayloadIntResult25 = ReadPayloadInt(payload.RootElement, "frameDepth");
            var readPayloadJsonResult = ReadPayloadJson(payload.RootElement, "inputs");
            var flowDebugPauseState = new FlowDebugPauseState(workerEvent.NodeId ?? string.Empty, evaluatedValue24, readPayloadIntResult, readPayloadIntResult25, invocationId, workerEvent.Sequence, readPayloadJsonResult, workerEvent.Timestamp, executionId);
            session.Pause(flowDebugPauseState, workerEvent.Timestamp, invocationId, invocationId is null ? null : session.ActiveFlipflopNodeId ?? workerEvent.NodeId);
            await debugStore.SaveAsync(session, cancellationToken);
        }
        else if (workerEvent.EventType is WorkerEventType.DebugTriggerReceived or WorkerEventType.DebugTriggerQueued or WorkerEventType.DebugTriggerAdmitted or WorkerEventType.DebugTriggerCompleted or WorkerEventType.DebugTriggerRejected or WorkerEventType.DebugTriggerFailed)
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            var invocationId = ReadPayloadGuid(payload.RootElement, "triggerInvocationId");
            var flipflopNodeId = ReadPayloadString(payload.RootElement, "flipflopNodeId") ?? workerEvent.NodeId;
            switch (workerEvent.EventType)
            {
                case WorkerEventType.DebugTriggerQueued:
                    session.SetActiveInvocation(session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount + 1, DateTimeOffset.UtcNow);
                    break;
                case WorkerEventType.DebugTriggerAdmitted:
                    var maxResult = Math.Max(0, session.QueuedTriggerCount - 1);
                    session.SetActiveInvocation(invocationId, flipflopNodeId, maxResult, DateTimeOffset.UtcNow);
                    break;
                case WorkerEventType.DebugTriggerCompleted or WorkerEventType.DebugTriggerFailed:
                    if (session.ActiveInvocationId == invocationId)
                    {
                        session.SetActiveInvocation(null, null, session.QueuedTriggerCount, DateTimeOffset.UtcNow);
                    }

                    break;
            }

            if (workerEvent.EventType != WorkerEventType.DebugTriggerReceived && workerEvent.EventType != WorkerEventType.DebugTriggerRejected)
            {
                await debugStore.SaveAsync(session, cancellationToken);
            }
        }

        var toEventTypeResult = ToEventType(workerEvent.EventType);
        var item = new FlowRunEvent(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, toEventTypeResult, workerEvent.NodeId, workerEvent.PayloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var output = CreateNodeOutput(workerEvent);
        if (output is not null)
        {
            await outputStore.AppendAsync([output], cancellationToken);
            var flowDebugNodeResult = new FlowDebugNodeResult(output.NodeId, output.Sequence, output.Timestamp, output.Outcome, output.Branch, output.InputsJson, output.OutputsJson, output.ErrorCode, output.ErrorMessage);
            session.RecordNodeResult(flowDebugNodeResult, output.Timestamp);
            await debugStore.SaveAsync(session, cancellationToken);
        }

        var flowRunEventDto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        await PublishLiveAsync(flowRunEventDto);
    }

    private async Task PublishTerminalAsync(FlowRun run, WorkerRunResultDto result, IFlowRunEventStore eventStore, CancellationToken cancellationToken)
    {
        var type = result.Status switch
        {
            FlowRunStatusDto.Cancelled => RunErrorCodes.Cancelled,
            FlowRunStatusDto.TimedOut => RunErrorCodes.TimedOut,
            FlowRunStatusDto.Failed => RunErrorCodes.Failed,
            _ => RunErrorCodes.Completed
        };
        if (run.Status == FlowRunStatus.Interrupted)
        {
            type = RunErrorCodes.Interrupted;
        }

        var evaluatedValue26 = await eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1;
        var createWebOptionsResult27 = SereinJsonSerialization.CreateWebOptions();
        var serializeResult28 = JsonSerializer.Serialize(new { result.Status, result.ErrorCode, result.ErrorMessage, cancellationSource = run.CancellationReason, variables = result.FinalVariables }, createWebOptionsResult27);
        var item = new FlowRunEvent(run.Id, evaluatedValue26, DateTimeOffset.UtcNow, type, null, serializeResult28);
        await eventStore.AppendAsync([item], cancellationToken);
        var flowRunEventDto29 = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        await PublishLiveAsync(flowRunEventDto29);
    }

    private async Task PublishLiveAsync(FlowRunEventDto item)
    {
        try
        {
            await _broadcaster.PublishAsync(item);
        }
        catch (Exception exception)
        {
            LiveDebugBroadcastFailedLog(_logger, item.RunId, exception);
        }

        try
        {
            var groupResult = _hub.Clients.Group($"run:{item.RunId:D}");
            await groupResult.SendAsync("runEvent", item, CancellationToken.None);
        }
        catch (Exception exception)
        {
            DebugSignalRPushFailedLog(_logger, item.RunId, exception);
        }
    }

    private static string ToEventType(WorkerEventType eventType) => eventType switch
    {
        WorkerEventType.RunStarted => RunErrorCodes.Started,
        WorkerEventType.NodeStarted => NodeErrorCodes.Started,
        WorkerEventType.NodeCompleted => NodeErrorCodes.Completed,
        WorkerEventType.NodeFailed => NodeErrorCodes.Failed,
        WorkerEventType.NodeErrored => NodeErrorCodes.Error,
        WorkerEventType.RunCompleted => RunErrorCodes.Completed,
        WorkerEventType.RunCancelled => RunErrorCodes.Cancelled,
        WorkerEventType.DebugPaused => DebugErrorCodes.Paused,
        WorkerEventType.DebugTriggerReceived => DebugErrorCodes.TriggerReceived,
        WorkerEventType.DebugTriggerQueued => DebugErrorCodes.TriggerQueued,
        WorkerEventType.DebugTriggerAdmitted => DebugErrorCodes.TriggerAdmitted,
        WorkerEventType.DebugTriggerRejected => DebugErrorCodes.TriggerRejected,
        WorkerEventType.DebugTriggerCompleted => DebugErrorCodes.TriggerCompleted,
        WorkerEventType.DebugTriggerFailed => DebugErrorCodes.TriggerFailed,
        _ => "log"
    };
    private static FlowRunStatus ToDomainStatus(FlowRunStatusDto status) => status switch
    {
        FlowRunStatusDto.Succeeded => FlowRunStatus.Succeeded,
        FlowRunStatusDto.Cancelled => FlowRunStatus.Cancelled,
        FlowRunStatusDto.TimedOut => FlowRunStatus.TimedOut,
        _ => FlowRunStatus.Failed
    };
    private static FlowRunOutput? CreateNodeOutput(WorkerEventEnvelopeDto workerEvent)
    {
        if (string.IsNullOrWhiteSpace(workerEvent.NodeId))
        {
            return null;
        }

        var outcome = workerEvent.EventType switch
        {
            WorkerEventType.NodeCompleted => "completed",
            WorkerEventType.NodeFailed => "failed",
            WorkerEventType.NodeErrored => "error",
            _ => null
        };
        if (outcome is null)
        {
            return null;
        }

        using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
        var root = payload.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Worker node output payload is invalid. Worker 节点输出载荷无效。");
        }

        var readPayloadStringResult = ReadPayloadString(root, "branch");
        string evaluatedValue30 = root.TryGetProperty("outputs", out var outputs) ? outputs.GetRawText() : "{}";
        var readPayloadStringResult31 = ReadPayloadString(root, "errorCode");
        var readPayloadStringResult32 = ReadPayloadString(root, "errorMessage");
        string evaluatedValue33 = root.TryGetProperty("inputs", out var inputs) ? inputs.GetRawText() : "{}";
        var flowRunOutput = new FlowRunOutput(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, workerEvent.NodeId, outcome, readPayloadStringResult, evaluatedValue30, readPayloadStringResult31, readPayloadStringResult32, evaluatedValue33);
        return flowRunOutput;
    }

    private static string? ReadPayloadString(JsonElement payload, string name)
    {
        string? evaluatedValue34 = payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return evaluatedValue34;
    }

    private static Guid? ReadPayloadGuid(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var identifierText = value.GetString();
            if (Guid.TryParse(identifierText, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int ReadPayloadInt(JsonElement payload, string name)
    {
        int evaluatedValue36 = payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? Math.Max(0, parsed) : 0;
        return evaluatedValue36;
    }

    private static string ReadPayloadJson(JsonElement payload, string name)
    {
        string evaluatedValue37 = payload.TryGetProperty(name, out var value) ? value.GetRawText() : "{}";
        return evaluatedValue37;
    }

    private sealed class ActiveDebugSession(IWorkerDebugRunHandle handle, RunExecutionQueue.RunExecutionLease lease, SemaphoreSlim stateGate)
    {
        public IWorkerDebugRunHandle Handle { get; } = handle;
        public RunExecutionQueue.RunExecutionLease Lease { get; } = lease;
        // Commands, Worker events and completion all replace the durable state.
        // Serialize their read/modify/write cycles to preserve pauses and revisions.
        // 命令、Worker 事件和完成处理都会替换持久化状态；串行执行读改写过程，保留暂停状态和修订号。
        public SemaphoreSlim CommandGate { get; } = stateGate;
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugWorkerStartupFailedLogInitialValue()
    {
        var eventId = new EventId(4101, nameof(DebugWorkerStartupFailedLog));
        Action<ILogger, Guid, Exception?> debugWorkerStartupFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Debug Worker startup failed for session {DebugSessionId}. 调试 Worker 启动失败。");
        return debugWorkerStartupFailedLogInitialValue;
    }

    private static Action<ILogger, Exception?> CreateDebugWorkerShutdownFailedLogInitialValue()
    {
        var eventId1 = new EventId(4102, nameof(DebugWorkerShutdownFailedLog));
        Action<ILogger, Exception?> debugWorkerShutdownFailedLogInitialValue = LoggerMessage.Define(LogLevel.Warning, eventId1, "Debug Worker shutdown failed. 调试 Worker 关闭失败。");
        return debugWorkerShutdownFailedLogInitialValue;
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugWorkerMonitoringFailedLogInitialValue()
    {
        var eventId2 = new EventId(4103, nameof(DebugWorkerMonitoringFailedLog));
        Action<ILogger, Guid, Exception?> debugWorkerMonitoringFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Error, eventId2, "Debug Worker monitoring failed for session {DebugSessionId}. 调试 Worker 监控失败。");
        return debugWorkerMonitoringFailedLogInitialValue;
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugCompletionPersistenceFailedLogInitialValue()
    {
        var eventId3 = new EventId(4104, nameof(DebugCompletionPersistenceFailedLog));
        Action<ILogger, Guid, Exception?> debugCompletionPersistenceFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Error, eventId3, "Debug completion persistence failed for session {DebugSessionId}. 调试完成状态持久化失败。");
        return debugCompletionPersistenceFailedLogInitialValue;
    }

    private static Action<ILogger, Guid, Exception?> CreateLiveDebugBroadcastFailedLogInitialValue()
    {
        var eventId4 = new EventId(4105, nameof(LiveDebugBroadcastFailedLog));
        Action<ILogger, Guid, Exception?> liveDebugBroadcastFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Warning, eventId4, "Live debug event broadcast failed for {RunId}. 实时调试事件广播失败。");
        return liveDebugBroadcastFailedLogInitialValue;
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugSignalRPushFailedLogInitialValue()
    {
        var eventId5 = new EventId(4106, nameof(DebugSignalRPushFailedLog));
        Action<ILogger, Guid, Exception?> debugSignalRPushFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Warning, eventId5, "Debug SignalR event push failed for {RunId}. 调试 SignalR 事件推送失败。");
        return debugSignalRPushFailedLogInitialValue;
    }
}
