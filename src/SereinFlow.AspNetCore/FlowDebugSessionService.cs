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
                return !string.IsNullOrWhiteSpace(nodeId);
            })
            .Select(static nodeId =>
            {
                return nodeId.Trim();
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        if (request.MaxQueuedFlipflopTriggers is < 0 or > 1_024)
        {
            return new FlowDebugSessionStartResult(null, StatusCodes.Status422UnprocessableEntity, "The debug trigger queue limit must be between 0 and 1024. 调试触发队列上限必须在 0 到 1024 之间。", new { code = DebugErrorCodes.InvalidTriggerQueueLimit }, DebugErrorCodes.InvalidTriggerQueueLimit);
        }

        var debugSessionId = Guid.NewGuid();
        using var scope = _scopeFactory.CreateScope();
        var runApplicationService = scope.ServiceProvider.GetRequiredService<RunApplicationService>();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var listActive = (await flowDebugSessionStore.ListActiveAsync(cancellationToken));
        var activeForFlow = listActive.FirstOrDefault(item => item.ProjectId == projectId && item.FlowId == flowId);
        if (activeForFlow is not null)
        {
            return new FlowDebugSessionStartResult(null, StatusCodes.Status409Conflict, "This flow already has an active debug session. 当前流程已有活动调试会话。", new { code = DebugErrorCodes.SessionAlreadyActive, sessionId = activeForFlow.Id }, DebugErrorCodes.SessionAlreadyActive);
        }

        var runFlowRequestDto = new RunFlowRequestDto(request.ExpectedFlowVersion, request.ProjectInputs, request.TimeoutSeconds, request.MaxSteps, request.MaxNodeVisits);
        var preparation = await runApplicationService.PrepareAsync(projectId, flowId, runFlowRequestDto, FlowRunExecutionKind.Debug, debugSessionId, breakpointNodeIds, definitionOverride: request.Definition, cancellationToken: cancellationToken);
        if (!preparation.IsSuccess)
        {
            return new FlowDebugSessionStartResult(null, preparation.StatusCode, preparation.ErrorTitle, preparation.ErrorBody);
        }

        var prepared = preparation.Preparation!;
        var session = FlowDebugSession.Create(prepared.Run.Id, projectId, flowId, breakpointNodeIds, DateTimeOffset.UtcNow, debugSessionId);
        await flowDebugSessionStore.CreateAsync(session, cancellationToken);
        var item = new RunWorkItem(prepared.Run.Id, projectId, prepared.Definition, prepared.ProjectInputs, prepared.Run.QueuedAt, prepared.TimeoutSeconds, prepared.MaxSteps, prepared.MaxNodeVisits);
        if (!_queue.TryStartDirectExecution(item, out var lease))
        {
            var failure = "No Worker execution slot is currently available for debugging. 当前没有可用于调试的 Worker 执行槽。";
            prepared.Run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, failure);
            await flowRunStore.SaveAsync(prepared.Run, CancellationToken.None);
            session.Fail(failure, DateTimeOffset.UtcNow);
            await flowDebugSessionStore.SaveAsync(session, CancellationToken.None);
            return new FlowDebugSessionStartResult(null, StatusCodes.Status429TooManyRequests, failure, new { code = DebugErrorCodes.ExecutionCapacityFull }, DebugErrorCodes.ExecutionCapacityFull);
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var addSeconds = now.AddSeconds(prepared.TimeoutSeconds);
            prepared.Run.MarkRunning(now, addSeconds);
            await flowRunStore.SaveAsync(prepared.Run, cancellationToken);
            session.MarkRunning(now);
            await flowDebugSessionStore.SaveAsync(session, cancellationToken);
            var webOptions = SereinJsonSerialization.CreateWebOptions();
            var definitionJson = JsonSerializer.Serialize(prepared.Definition, webOptions);
            var projectIdText = projectId.ToString("D");
            var allowedLibraryIds = ProjectLibraryService.GetLibraryIds(prepared.Definition);
            var workerDebugOptionsDto = new WorkerDebugOptionsDto(session.Id, session.BreakpointNodeIds, request.MaxQueuedFlipflopTriggers ?? 64);
            var workerRequest = new WorkerRunRequestDto(WorkerProtocol.Version, prepared.Run.Id, prepared.Definition.Id, prepared.Definition.Version, definitionJson, prepared.Run.Deadline!.Value, projectIdText, prepared.ProjectInputs, prepared.MaxSteps, _scriptRoot, _libraryRoot, prepared.MaxNodeVisits, allowedLibraryIds, workerDebugOptionsDto, _workpieceRoot);
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
            var mappedCancellationToken = _queue.GetCancellationToken(prepared.Run.Id);
            var handle = await _workerClient.StartDebugAsync(workerRequest, delegateWorkerRunEventSink, mappedCancellationToken);
            var active = new ActiveDebugSession(handle, lease!, stateGate);
            if (!_active.TryAdd(session.Id, active))
            {
                await handle.DisposeAsync();
                throw new InvalidOperationException("The debug session is already active. 调试会话已处于活动状态。");
            }

            _ = ObserveCompletionAsync(session.Id, active);
            return new FlowDebugSessionStartResult(session, StatusCodes.Status202Accepted);
        }
        catch (Exception exception)
        {
            lease!.Dispose();
            var failedRun = await flowRunStore.FindAsync(prepared.Run.Id, CancellationToken.None);
            if (failedRun is not null && !failedRun.IsTerminal)
            {
                failedRun.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, $"Debug Worker startup failed. 调试 Worker 启动失败。 {exception.Message}");
                await flowRunStore.SaveAsync(failedRun, CancellationToken.None);
            }

            var failedSession = await flowDebugSessionStore.FindAsync(session.Id, CancellationToken.None);
            if (failedSession is not null)
            {
                failedSession.Fail($"Debug Worker startup failed. 调试 Worker 启动失败。 {exception.Message}", DateTimeOffset.UtcNow);
                await flowDebugSessionStore.SaveAsync(failedSession, CancellationToken.None);
            }

            DebugWorkerStartupFailedLog(_logger, session.Id, exception);
            return new FlowDebugSessionStartResult(null, StatusCodes.Status503ServiceUnavailable, "The debug Worker could not start. 调试 Worker 无法启动。", new { code = DebugErrorCodes.WorkerStartFailed }, DebugErrorCodes.WorkerStartFailed);
        }
    }

    /// <summary>
    /// Finds a debug session by its identifier.
    /// 按标识查找调试会话。
    /// </summary>
    public async Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        return await flowDebugSessionStore.FindAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Waits for the debug session state to advance beyond a revision.
    /// 等待调试会话状态推进到指定修订号之后。
    /// </summary>
    public async Task<FlowDebugStateWaitResult> WaitForChangeAsync(Guid sessionId, long afterRevision, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        return await FlowDebugSessionWaiter.WaitAsync(flowDebugSessionStore, sessionId, afterRevision, timeout, cancellationToken);
    }

    /// <summary>
    /// Continues a paused debug session.
    /// 继续执行已暂停的调试会话。
    /// </summary>
    public Task<FlowDebugSessionCommandResult> ContinueAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(sessionId, commandSequence, static (handle, sequence, token) =>
        {
            return handle.ContinueAsync(sequence, token);
        }, requirePaused: true, cancelRun: false, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Advances a paused debug session by one step.
    /// 将已暂停的调试会话推进一步。
    /// </summary>
    public Task<FlowDebugSessionCommandResult> StepAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(sessionId, commandSequence, static (handle, sequence, token) =>
        {
            return handle.StepAsync(sequence, token);
        }, requirePaused: true, cancelRun: false, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stops a debug session.
    /// 停止调试会话。
    /// </summary>
    public Task<FlowDebugSessionCommandResult> StopAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(sessionId, commandSequence, static (handle, sequence, token) =>
        {
            return handle.StopAsync(sequence, token);
        }, requirePaused: false, cancelRun: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Starts background maintenance for persisted debug sessions.
    /// 启动持久化调试会话的后台维护。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var sessions = await flowDebugSessionStore.ListActiveAsync(cancellationToken);
        foreach (var session in sessions)
        {
            var now = DateTimeOffset.UtcNow;
            const string message = "The API process restarted before the debug Worker could be reattached. API 进程重启，无法重新接管调试 Worker。";
            session.Fail(message, now);
            await flowDebugSessionStore.SaveAsync(session, cancellationToken);
            var run = await flowRunStore.FindAsync(session.RunId, cancellationToken);
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

            await flowRunStore.SaveAsync(run, cancellationToken);
            var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Failed, DebugErrorCodes.ApiRestart, message);
            await PublishTerminalAsync(run, workerRunResultDto, flowRunEventStore, cancellationToken);
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
            return new FlowDebugSessionCommandResult(StatusCodes.Status400BadRequest, "The debug command sequence must be positive. 调试命令序号必须为正数。", DebugErrorCodes.InvalidCommandSequence);
        }

        if (!_active.TryGetValue(sessionId, out var active))
        {
            return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is not active. 调试会话当前未处于活动状态。", DebugErrorCodes.SessionNotActive);
        }

        await active.CommandGate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
            var session = await flowDebugSessionStore.FindAsync(sessionId, cancellationToken);
            if (session is null)
            {
                return new FlowDebugSessionCommandResult(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。", DebugErrorCodes.SessionNotFound);
            }

            if (session.IsTerminal)
            {
                return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is already complete. 调试会话已经完成。", DebugErrorCodes.SessionTerminal);
            }

            if (commandSequence <= session.LastCommandSequence)
            {
                return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。", DebugErrorCodes.CommandSequenceConflict);
            }

            if (requirePaused && session.Status != FlowDebugSessionStatus.Paused)
            {
                return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is not paused. 调试会话当前未暂停。", DebugErrorCodes.SessionNotPaused);
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
                await flowDebugSessionStore.SaveAsync(session, cancellationToken);
                try
                {
                    await send(active.Handle, commandSequence, cancellationToken);
                }
                finally
                {
                    _queue.Cancel(session.RunId, DebugErrorCodes.Stop);
                }
                return new FlowDebugSessionCommandResult(StatusCodes.Status202Accepted, Session: session);
            }

            await send(active.Handle, commandSequence, cancellationToken);
            session.AcceptCommand(commandSequence, DateTimeOffset.UtcNow);
            if (requirePaused && session.Status == FlowDebugSessionStatus.Paused)
            {
                session.Resume(DateTimeOffset.UtcNow);
            }

            await flowDebugSessionStore.SaveAsync(session, cancellationToken);
            return new FlowDebugSessionCommandResult(StatusCodes.Status202Accepted, Session: session);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("strictly increasing", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。", DebugErrorCodes.CommandSequenceConflict);
        }
        catch (InvalidOperationException exception)
        {
            return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, exception.Message, DebugErrorCodes.CommandRejected);
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
            var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
            var run = await flowRunStore.FindAsync(result.RunId, CancellationToken.None);
            if (run is not null && !run.IsTerminal)
            {
                if (result.Status == FlowRunStatusDto.Cancelled)
                {
                    run.Cancel(DebugErrorCodes.Stop, DateTimeOffset.UtcNow);
                }
                else
                {
                    var domainStatus = ToDomainStatus(result.Status);
                    run.Complete(domainStatus, DateTimeOffset.UtcNow, result.ErrorMessage);
                }

                await PublishTerminalAsync(run, result, flowRunEventStore, CancellationToken.None);
                await flowRunStore.SaveAsync(run, CancellationToken.None);
            }

            var session = await flowDebugSessionStore.FindAsync(sessionId, CancellationToken.None);
            if (session is not null)
            {
                var flowRunStatus = ToDomainStatus(result.Status);
                session.Complete(flowRunStatus, result.ErrorMessage, DateTimeOffset.UtcNow);
                await flowDebugSessionStore.SaveAsync(session, CancellationToken.None);
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
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var flowRunOutputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
        var flowDebugSessionStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var session = await flowDebugSessionStore.FindAsync(sessionId, cancellationToken);
        if (session is null || session.RunId != workerEvent.RunId)
        {
            return;
        }

        if (await flowRunStore.FindAsync(workerEvent.RunId, cancellationToken) is null)
        {
            return;
        }

        var lastSequence = await flowRunEventStore.GetLastSequenceAsync(workerEvent.RunId, cancellationToken);
        if (workerEvent.Sequence <= lastSequence)
        {
            return;
        }

        if (workerEvent.EventType == WorkerEventType.DebugPaused && !session.IsTerminal)
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            var invocationId = ReadPayloadGuid(payload.RootElement, "triggerInvocationId");
            var executionId = ReadPayloadGuid(payload.RootElement, "executionId");
            var nodeType = ReadPayloadString(payload.RootElement, "nodeType") ?? string.Empty;
            var step = ReadPayloadInt(payload.RootElement, "step");
            var frameDepth = ReadPayloadInt(payload.RootElement, "frameDepth");
            var inputsJson = ReadPayloadJson(payload.RootElement, "inputs");
            var flowDebugPauseState = new FlowDebugPauseState(workerEvent.NodeId ?? string.Empty, nodeType, step, frameDepth, invocationId, workerEvent.Sequence, inputsJson, workerEvent.Timestamp, executionId);
            session.Pause(flowDebugPauseState, workerEvent.Timestamp, invocationId, invocationId is null ? null : session.ActiveFlipflopNodeId ?? workerEvent.NodeId);
            await flowDebugSessionStore.SaveAsync(session, cancellationToken);
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
                    var max = Math.Max(0, session.QueuedTriggerCount - 1);
                    session.SetActiveInvocation(invocationId, flipflopNodeId, max, DateTimeOffset.UtcNow);
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
                await flowDebugSessionStore.SaveAsync(session, cancellationToken);
            }
        }

        var type = ToEventType(workerEvent.EventType);
        var item = new FlowRunEvent(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, type, workerEvent.NodeId, workerEvent.PayloadJson);
        await flowRunEventStore.AppendAsync([item], cancellationToken);
        var output = CreateNodeOutput(workerEvent);
        if (output is not null)
        {
            await flowRunOutputStore.AppendAsync([output], cancellationToken);
            var flowDebugNodeResult = new FlowDebugNodeResult(output.NodeId, output.Sequence, output.Timestamp, output.Outcome, output.Branch, output.InputsJson, output.OutputsJson, output.ErrorCode, output.ErrorMessage);
            session.RecordNodeResult(flowDebugNodeResult, output.Timestamp);
            await flowDebugSessionStore.SaveAsync(session, cancellationToken);
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

        var sequence = await eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1;
        var webOptions = SereinJsonSerialization.CreateWebOptions();
        var payloadJson = JsonSerializer.Serialize(new { result.Status, result.ErrorCode, result.ErrorMessage, cancellationSource = run.CancellationReason, variables = result.FinalVariables }, webOptions);
        var item = new FlowRunEvent(run.Id, sequence, DateTimeOffset.UtcNow, type, null, payloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var flowRunEventDto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        await PublishLiveAsync(flowRunEventDto);
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
            var group = _hub.Clients.Group($"run:{item.RunId:D}");
            await group.SendAsync("runEvent", item, CancellationToken.None);
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

        var branch = ReadPayloadString(root, "branch");
        string outputsJson = root.TryGetProperty("outputs", out var outputs) ? outputs.GetRawText() : "{}";
        var errorCode = ReadPayloadString(root, "errorCode");
        var errorMessage = ReadPayloadString(root, "errorMessage");
        string inputsJson = root.TryGetProperty("inputs", out var inputs) ? inputs.GetRawText() : "{}";
        return new FlowRunOutput(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, workerEvent.NodeId, outcome, branch, outputsJson, errorCode, errorMessage, inputsJson);
    }

    private static string? ReadPayloadString(JsonElement payload, string name)
    {
        return payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
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
        return payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? Math.Max(0, parsed) : 0;
    }

    private static string ReadPayloadJson(JsonElement payload, string name)
    {
        return payload.TryGetProperty(name, out var value) ? value.GetRawText() : "{}";
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
        return LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Debug Worker startup failed for session {DebugSessionId}. 调试 Worker 启动失败。");
    }

    private static Action<ILogger, Exception?> CreateDebugWorkerShutdownFailedLogInitialValue()
    {
        var eventId = new EventId(4102, nameof(DebugWorkerShutdownFailedLog));
        return LoggerMessage.Define(LogLevel.Warning, eventId, "Debug Worker shutdown failed. 调试 Worker 关闭失败。");
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugWorkerMonitoringFailedLogInitialValue()
    {
        var eventId = new EventId(4103, nameof(DebugWorkerMonitoringFailedLog));
        return LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Debug Worker monitoring failed for session {DebugSessionId}. 调试 Worker 监控失败。");
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugCompletionPersistenceFailedLogInitialValue()
    {
        var eventId = new EventId(4104, nameof(DebugCompletionPersistenceFailedLog));
        return LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Debug completion persistence failed for session {DebugSessionId}. 调试完成状态持久化失败。");
    }

    private static Action<ILogger, Guid, Exception?> CreateLiveDebugBroadcastFailedLogInitialValue()
    {
        var eventId = new EventId(4105, nameof(LiveDebugBroadcastFailedLog));
        return LoggerMessage.Define<Guid>(LogLevel.Warning, eventId, "Live debug event broadcast failed for {RunId}. 实时调试事件广播失败。");
    }

    private static Action<ILogger, Guid, Exception?> CreateDebugSignalRPushFailedLogInitialValue()
    {
        var eventId = new EventId(4106, nameof(DebugSignalRPushFailedLog));
        return LoggerMessage.Define<Guid>(LogLevel.Warning, eventId, "Debug SignalR event push failed for {RunId}. 调试 SignalR 事件推送失败。");
    }
}
