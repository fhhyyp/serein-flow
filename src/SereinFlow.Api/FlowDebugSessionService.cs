using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;

public sealed record FlowDebugSessionStartResult(
    FlowDebugSession? Session,
    int StatusCode,
    string? ErrorTitle = null,
    object? ErrorBody = null)
{
    public bool IsAccepted => Session is not null;
}

public sealed record FlowDebugSessionCommandResult(int StatusCode, string? ErrorTitle = null)
{
    public bool IsAccepted => StatusCode is >= 200 and < 300;
}

/// <summary>
/// Owns API-side debug handles while keeping all executable code inside the
/// Worker process. The durable session record is intentionally smaller than a
/// Worker session so it can be reconciled safely after an API restart.
/// 在 API 侧持有调试句柄，同时将所有可执行代码保留在 Worker 进程内。持久化会话记录
/// 被刻意设计得比 Worker 会话更小，因此 API 重启后可安全对账。
/// </summary>
public sealed class FlowDebugSessionService : IHostedService
{
    private const string DebugStoppedMessage = "The debug session was stopped. 调试会话已停止。";
    private static readonly Action<ILogger, Guid, Exception?> DebugWorkerStartupFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(4101, nameof(DebugWorkerStartupFailedLog)),
        "Debug Worker startup failed for session {DebugSessionId}. 调试 Worker 启动失败。");
    private static readonly Action<ILogger, Exception?> DebugWorkerShutdownFailedLog = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(4102, nameof(DebugWorkerShutdownFailedLog)),
        "Debug Worker shutdown failed. 调试 Worker 关闭失败。");
    private static readonly Action<ILogger, Guid, Exception?> DebugWorkerMonitoringFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(4103, nameof(DebugWorkerMonitoringFailedLog)),
        "Debug Worker monitoring failed for session {DebugSessionId}. 调试 Worker 监控失败。");
    private static readonly Action<ILogger, Guid, Exception?> DebugCompletionPersistenceFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(4104, nameof(DebugCompletionPersistenceFailedLog)),
        "Debug completion persistence failed for session {DebugSessionId}. 调试完成状态持久化失败。");
    private static readonly Action<ILogger, Guid, Exception?> LiveDebugBroadcastFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4105, nameof(LiveDebugBroadcastFailedLog)),
        "Live debug event broadcast failed for {RunId}. 实时调试事件广播失败。");
    private static readonly Action<ILogger, Guid, Exception?> DebugSignalRPushFailedLog = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4106, nameof(DebugSignalRPushFailedLog)),
        "Debug SignalR event push failed for {RunId}. 调试 SignalR 事件推送失败。");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerDebugRunClient _workerClient;
    private readonly RunExecutionQueue _queue;
    private readonly RunEventBroadcaster _broadcaster;
    private readonly IHubContext<RunEventsHub> _hub;
    private readonly ILogger<FlowDebugSessionService> _logger;
    private readonly string _scriptRoot;
    private readonly string _libraryRoot;
    private readonly ConcurrentDictionary<Guid, ActiveDebugSession> _active = new();

    public FlowDebugSessionService(
        IServiceScopeFactory scopeFactory,
        IWorkerDebugRunClient workerClient,
        RunExecutionQueue queue,
        RunEventBroadcaster broadcaster,
        IHubContext<RunEventsHub> hub,
        ILogger<FlowDebugSessionService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _workerClient = workerClient;
        _queue = queue;
        _broadcaster = broadcaster;
        _hub = hub;
        _logger = logger;
        _scriptRoot = ResolvePath(
            configuration["SereinFlow:ScriptArtifactRoot"] ?? "data/script-artifacts",
            environment.ContentRootPath);
        _libraryRoot = ResolvePath(
            configuration["SereinFlow:LibraryDirectory"] ?? "data/libraries",
            environment.ContentRootPath);
    }

    public async Task<FlowDebugSessionStartResult> CreateAsync(
        Guid projectId,
        Guid flowId,
        StartFlowDebugSessionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var breakpointNodeIds = (request.BreakpointNodeIds ?? [])
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        if (request.MaxQueuedFlipflopTriggers is < 0 or > 1_024)
        {
            return new FlowDebugSessionStartResult(
                null,
                StatusCodes.Status422UnprocessableEntity,
                "The debug trigger queue limit must be between 0 and 1024. 调试触发队列上限必须在 0 到 1024 之间。",
                new { code = "debug.invalid_trigger_queue_limit" });
        }
        var debugSessionId = Guid.NewGuid();

        using var scope = _scopeFactory.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<RunApplicationService>();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var debugStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var activeForFlow = (await debugStore.ListActiveAsync(cancellationToken))
            .FirstOrDefault(item => item.ProjectId == projectId && item.FlowId == flowId);
        if (activeForFlow is not null)
        {
            return new FlowDebugSessionStartResult(
                null,
                StatusCodes.Status409Conflict,
                "This flow already has an active debug session. 当前流程已有活动调试会话。",
                new { code = "debug.session_already_active", sessionId = activeForFlow.Id });
        }
        var preparation = await runService.PrepareAsync(
            projectId,
            flowId,
            new RunFlowRequestDto(
                request.ExpectedFlowVersion,
                request.ProjectInputs,
                request.TimeoutSeconds,
                request.MaxSteps,
                request.MaxNodeVisits),
            FlowRunExecutionKind.Debug,
            debugSessionId,
            breakpointNodeIds,
            cancellationToken: cancellationToken);
        if (!preparation.IsSuccess)
        {
            return new FlowDebugSessionStartResult(
                null,
                preparation.StatusCode,
                preparation.ErrorTitle,
                preparation.ErrorBody);
        }

        var prepared = preparation.Preparation!;
        var session = FlowDebugSession.Create(
            prepared.Run.Id,
            projectId,
            flowId,
            breakpointNodeIds,
            DateTimeOffset.UtcNow,
            debugSessionId);
        await debugStore.CreateAsync(session, cancellationToken);

        var item = new RunWorkItem(
            prepared.Run.Id,
            projectId,
            prepared.Definition,
            prepared.ProjectInputs,
            prepared.Run.QueuedAt,
            prepared.TimeoutSeconds,
            prepared.MaxSteps,
            prepared.MaxNodeVisits);
        if (!_queue.TryStartDirectExecution(item, out var lease))
        {
            var failure = "No Worker execution slot is currently available for debugging. 当前没有可用于调试的 Worker 执行槽。";
            prepared.Run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, failure);
            await runStore.SaveAsync(prepared.Run, CancellationToken.None);
            session.Fail(failure, DateTimeOffset.UtcNow);
            await debugStore.SaveAsync(session, CancellationToken.None);
            return new FlowDebugSessionStartResult(
                null,
                StatusCodes.Status429TooManyRequests,
                failure,
                new { code = "debug.execution_capacity_full" });
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            prepared.Run.MarkRunning(now, now.AddSeconds(prepared.TimeoutSeconds));
            await runStore.SaveAsync(prepared.Run, cancellationToken);
            session.MarkRunning(now);
            await debugStore.SaveAsync(session, cancellationToken);

            var workerRequest = new WorkerRunRequestDto(
                WorkerProtocol.Version,
                prepared.Run.Id,
                prepared.Definition.Id,
                prepared.Definition.Version,
                JsonSerializer.Serialize(prepared.Definition, SereinJsonSerialization.CreateWebOptions()),
                prepared.Run.Deadline!.Value,
                projectId.ToString("D"),
                prepared.ProjectInputs,
                prepared.MaxSteps,
                _scriptRoot,
                _libraryRoot,
                prepared.MaxNodeVisits,
                ProjectLibraryService.GetLibraryIds(prepared.Definition),
                new WorkerDebugOptionsDto(
                    session.Id,
                    session.BreakpointNodeIds,
                    request.MaxQueuedFlipflopTriggers ?? 64));
            var handle = await _workerClient.StartDebugAsync(
                workerRequest,
                new DelegateWorkerRunEventSink((workerEvent, token) => HandleWorkerEventAsync(session.Id, workerEvent, token)),
                _queue.GetCancellationToken(prepared.Run.Id));
            var active = new ActiveDebugSession(handle, lease!);
            if (!_active.TryAdd(session.Id, active))
            {
                await handle.DisposeAsync();
                throw new InvalidOperationException(
                    "The debug session is already active. 调试会话已处于活动状态。");
            }

            _ = ObserveCompletionAsync(session.Id, active);
            return new FlowDebugSessionStartResult(session, StatusCodes.Status202Accepted);
        }
        catch (Exception exception)
        {
            lease!.Dispose();
            var failedRun = await runStore.FindAsync(prepared.Run.Id, CancellationToken.None);
            if (failedRun is not null && !failedRun.IsTerminal)
            {
                failedRun.Complete(
                    FlowRunStatus.Failed,
                    DateTimeOffset.UtcNow,
                    $"Debug Worker startup failed. 调试 Worker 启动失败。 {exception.Message}");
                await runStore.SaveAsync(failedRun, CancellationToken.None);
            }
            var failedSession = await debugStore.FindAsync(session.Id, CancellationToken.None);
            if (failedSession is not null)
            {
                failedSession.Fail(
                    $"Debug Worker startup failed. 调试 Worker 启动失败。 {exception.Message}",
                    DateTimeOffset.UtcNow);
                await debugStore.SaveAsync(failedSession, CancellationToken.None);
            }
            DebugWorkerStartupFailedLog(_logger, session.Id, exception);
            return new FlowDebugSessionStartResult(
                null,
                StatusCodes.Status503ServiceUnavailable,
                "The debug Worker could not start. 调试 Worker 无法启动。",
                new { code = "debug.worker_start_failed" });
        }
    }

    public async Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        return await store.FindAsync(sessionId, cancellationToken);
    }

    public async Task<FlowDebugStateWaitResult> WaitForChangeAsync(
        Guid sessionId,
        long afterRevision,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        return await FlowDebugSessionWaiter.WaitAsync(
            store,
            sessionId,
            afterRevision,
            timeout,
            cancellationToken);
    }

    public Task<FlowDebugSessionCommandResult> ContinueAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
        => SendCommandAsync(
            sessionId,
            commandSequence,
            static (handle, sequence, token) => handle.ContinueAsync(sequence, token),
            requirePaused: true,
            cancelRun: false,
            cancellationToken: cancellationToken);

    public Task<FlowDebugSessionCommandResult> StepAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
        => SendCommandAsync(
            sessionId,
            commandSequence,
            static (handle, sequence, token) => handle.StepAsync(sequence, token),
            requirePaused: true,
            cancelRun: false,
            cancellationToken: cancellationToken);

    public Task<FlowDebugSessionCommandResult> StopAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
        => SendCommandAsync(
            sessionId,
            commandSequence,
            static (handle, sequence, token) => handle.StopAsync(sequence, token),
            requirePaused: false,
            cancelRun: true,
            cancellationToken: cancellationToken);

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
                continue;

            if (run.Status == FlowRunStatus.Running)
            {
                run.Interrupt("debug.api_restart", message, now);
            }
            else
            {
                run.Complete(FlowRunStatus.Failed, now, message);
            }
            await runStore.SaveAsync(run, cancellationToken);
            await PublishTerminalAsync(
                run,
                new WorkerRunResultDto(
                    WorkerProtocol.Version,
                    run.Id,
                    FlowRunStatusDto.Failed,
                    "debug.api_restart",
                    message),
                eventStore,
                cancellationToken);
        }
    }

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

    private async Task<FlowDebugSessionCommandResult> SendCommandAsync(
        Guid sessionId,
        long commandSequence,
        Func<IWorkerDebugRunHandle, long, CancellationToken, Task> send,
        bool requirePaused,
        bool cancelRun,
        CancellationToken cancellationToken)
    {
        if (commandSequence < 1)
        {
            return new FlowDebugSessionCommandResult(
                StatusCodes.Status400BadRequest,
                "The debug command sequence must be positive. 调试命令序号必须为正数。");
        }
        if (!_active.TryGetValue(sessionId, out var active))
        {
            return new FlowDebugSessionCommandResult(
                StatusCodes.Status409Conflict,
                "The debug session is not active. 调试会话当前未处于活动状态。");
        }

        await active.CommandGate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
            var session = await store.FindAsync(sessionId, cancellationToken);
            if (session is null)
                return new FlowDebugSessionCommandResult(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。");
            if (session.IsTerminal)
                return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, "The debug session is already complete. 调试会话已经完成。");
            if (requirePaused && session.Status != FlowDebugSessionStatus.Paused)
            {
                return new FlowDebugSessionCommandResult(
                    StatusCodes.Status409Conflict,
                    "The debug session is not paused. 调试会话当前未暂停。");
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
                    _queue.Cancel(session.RunId, "debug.stop");
                }
                return new FlowDebugSessionCommandResult(StatusCodes.Status202Accepted);
            }

            await send(active.Handle, commandSequence, cancellationToken);
            session.AcceptCommand(commandSequence, DateTimeOffset.UtcNow);
            if (requirePaused && session.Status == FlowDebugSessionStatus.Paused)
                session.Resume(DateTimeOffset.UtcNow);
            await store.SaveAsync(session, cancellationToken);
            return new FlowDebugSessionCommandResult(StatusCodes.Status202Accepted);
        }
        catch (InvalidOperationException exception)
        {
            return new FlowDebugSessionCommandResult(StatusCodes.Status409Conflict, exception.Message);
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
            result = new WorkerRunResultDto(
                WorkerProtocol.Version,
                active.Handle.RunId,
                FlowRunStatusDto.Failed,
                "debug.worker_monitor_failed",
                exception.Message);
        }

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
                    run.Cancel("debug.stop", DateTimeOffset.UtcNow);
                else
                    run.Complete(ToDomainStatus(result.Status), DateTimeOffset.UtcNow, result.ErrorMessage);
                await runStore.SaveAsync(run, CancellationToken.None);
                await PublishTerminalAsync(run, result, eventStore, CancellationToken.None);
            }

            var session = await debugStore.FindAsync(sessionId, CancellationToken.None);
            if (session is not null)
            {
                session.Complete(ToDomainStatus(result.Status), result.ErrorMessage, DateTimeOffset.UtcNow);
                await debugStore.SaveAsync(session, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            DebugCompletionPersistenceFailedLog(_logger, sessionId, exception);
        }
        finally
        {
            _active.TryRemove(sessionId, out _);
            active.Lease.Dispose();
            await active.Handle.DisposeAsync();
        }
    }

    private async ValueTask HandleWorkerEventAsync(
        Guid sessionId,
        WorkerEventEnvelopeDto workerEvent,
        CancellationToken cancellationToken)
    {
        if (workerEvent.ProtocolVersion != WorkerProtocol.Version || workerEvent.RunId == Guid.Empty)
            return;

        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var outputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
        var debugStore = scope.ServiceProvider.GetRequiredService<IFlowDebugSessionStore>();
        var session = await debugStore.FindAsync(sessionId, cancellationToken);
        if (session is null || session.RunId != workerEvent.RunId)
            return;
        if (await runStore.FindAsync(workerEvent.RunId, cancellationToken) is null)
            return;

        var lastSequence = await eventStore.GetLastSequenceAsync(workerEvent.RunId, cancellationToken);
        if (workerEvent.Sequence <= lastSequence)
            return;

        if (workerEvent.EventType == WorkerEventType.DebugPaused && !session.IsTerminal)
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            var invocationId = ReadPayloadGuid(payload.RootElement, "triggerInvocationId");
            session.Pause(
                new FlowDebugPauseState(
                    workerEvent.NodeId ?? string.Empty,
                    ReadPayloadString(payload.RootElement, "nodeType") ?? string.Empty,
                    ReadPayloadInt(payload.RootElement, "step"),
                    ReadPayloadInt(payload.RootElement, "frameDepth"),
                    invocationId,
                    workerEvent.Sequence,
                    ReadPayloadJson(payload.RootElement, "inputs"),
                    workerEvent.Timestamp),
                workerEvent.Timestamp,
                invocationId,
                invocationId is null ? null : session.ActiveFlipflopNodeId ?? workerEvent.NodeId);
            await debugStore.SaveAsync(session, cancellationToken);
        }
        else if (workerEvent.EventType is WorkerEventType.DebugTriggerReceived
            or WorkerEventType.DebugTriggerQueued
            or WorkerEventType.DebugTriggerAdmitted
            or WorkerEventType.DebugTriggerCompleted
            or WorkerEventType.DebugTriggerRejected
            or WorkerEventType.DebugTriggerFailed)
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            var invocationId = ReadPayloadGuid(payload.RootElement, "triggerInvocationId");
            var flipflopNodeId = ReadPayloadString(payload.RootElement, "flipflopNodeId") ?? workerEvent.NodeId;
            switch (workerEvent.EventType)
            {
                case WorkerEventType.DebugTriggerQueued:
                    session.SetActiveInvocation(
                        session.ActiveInvocationId,
                        session.ActiveFlipflopNodeId,
                        session.QueuedTriggerCount + 1,
                        DateTimeOffset.UtcNow);
                    break;
                case WorkerEventType.DebugTriggerAdmitted:
                    session.SetActiveInvocation(
                        invocationId,
                        flipflopNodeId,
                        Math.Max(0, session.QueuedTriggerCount - 1),
                        DateTimeOffset.UtcNow);
                    break;
                case WorkerEventType.DebugTriggerCompleted or WorkerEventType.DebugTriggerFailed:
                    if (session.ActiveInvocationId == invocationId)
                    {
                        session.SetActiveInvocation(
                            null,
                            null,
                            session.QueuedTriggerCount,
                            DateTimeOffset.UtcNow);
                    }
                    break;
            }
            if (workerEvent.EventType != WorkerEventType.DebugTriggerReceived
                && workerEvent.EventType != WorkerEventType.DebugTriggerRejected)
                await debugStore.SaveAsync(session, cancellationToken);
        }

        var item = new FlowRunEvent(
            workerEvent.RunId,
            workerEvent.Sequence,
            workerEvent.Timestamp,
            ToEventType(workerEvent.EventType),
            workerEvent.NodeId,
            workerEvent.PayloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var output = CreateNodeOutput(workerEvent);
        if (output is not null)
        {
            await outputStore.AppendAsync([output], cancellationToken);
            session.RecordNodeResult(
                new FlowDebugNodeResult(
                    output.NodeId,
                    output.Sequence,
                    output.Timestamp,
                    output.Outcome,
                    output.Branch,
                    output.InputsJson,
                    output.OutputsJson,
                    output.ErrorCode,
                    output.ErrorMessage),
                output.Timestamp);
            await debugStore.SaveAsync(session, cancellationToken);
        }
        await PublishLiveAsync(new FlowRunEventDto(
            item.RunId,
            item.Sequence,
            item.Timestamp,
            item.Type,
            item.NodeId,
            item.PayloadJson));
    }

    private async Task PublishTerminalAsync(
        FlowRun run,
        WorkerRunResultDto result,
        IFlowRunEventStore eventStore,
        CancellationToken cancellationToken)
    {
        var type = result.Status switch
        {
            FlowRunStatusDto.Cancelled => "run.cancelled",
            FlowRunStatusDto.TimedOut => "run.timed_out",
            FlowRunStatusDto.Failed => "run.failed",
            _ => "run.completed"
        };
        if (run.Status == FlowRunStatus.Interrupted)
            type = "run.interrupted";
        var item = new FlowRunEvent(
            run.Id,
            await eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1,
            DateTimeOffset.UtcNow,
            type,
            null,
            JsonSerializer.Serialize(
                new { result.Status, result.ErrorCode, result.ErrorMessage, cancellationSource = run.CancellationReason },
                SereinJsonSerialization.CreateWebOptions()));
        await eventStore.AppendAsync([item], cancellationToken);
        await PublishLiveAsync(new FlowRunEventDto(
            item.RunId,
            item.Sequence,
            item.Timestamp,
            item.Type,
            item.NodeId,
            item.PayloadJson));
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
            await _hub.Clients.Group($"run:{item.RunId:D}").SendAsync("runEvent", item, CancellationToken.None);
        }
        catch (Exception exception)
        {
            DebugSignalRPushFailedLog(_logger, item.RunId, exception);
        }
    }

    private static string ToEventType(WorkerEventType eventType)
        => eventType switch
        {
            WorkerEventType.RunStarted => "run.started",
            WorkerEventType.NodeStarted => "node.started",
            WorkerEventType.NodeCompleted => "node.completed",
            WorkerEventType.NodeFailed => "node.failed",
            WorkerEventType.NodeErrored => "node.error",
            WorkerEventType.RunCompleted => "run.completed",
            WorkerEventType.RunCancelled => "run.cancelled",
            WorkerEventType.DebugPaused => "debug.paused",
            WorkerEventType.DebugTriggerReceived => "debug.trigger.received",
            WorkerEventType.DebugTriggerQueued => "debug.trigger.queued",
            WorkerEventType.DebugTriggerAdmitted => "debug.trigger.admitted",
            WorkerEventType.DebugTriggerRejected => "debug.trigger.rejected",
            WorkerEventType.DebugTriggerCompleted => "debug.trigger.completed",
            WorkerEventType.DebugTriggerFailed => "debug.trigger.failed",
            _ => "log"
        };

    private static FlowRunStatus ToDomainStatus(FlowRunStatusDto status)
        => status switch
        {
            FlowRunStatusDto.Succeeded => FlowRunStatus.Succeeded,
            FlowRunStatusDto.Cancelled => FlowRunStatus.Cancelled,
            FlowRunStatusDto.TimedOut => FlowRunStatus.TimedOut,
            _ => FlowRunStatus.Failed
        };

    private static FlowRunOutput? CreateNodeOutput(WorkerEventEnvelopeDto workerEvent)
    {
        if (string.IsNullOrWhiteSpace(workerEvent.NodeId))
            return null;
        var outcome = workerEvent.EventType switch
        {
            WorkerEventType.NodeCompleted => "completed",
            WorkerEventType.NodeFailed => "failed",
            WorkerEventType.NodeErrored => "error",
            _ => null
        };
        if (outcome is null)
            return null;

        using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
        var root = payload.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Worker node output payload is invalid. Worker 节点输出载荷无效。");
        }
        return new FlowRunOutput(
            workerEvent.RunId,
            workerEvent.Sequence,
            workerEvent.Timestamp,
            workerEvent.NodeId,
            outcome,
            ReadPayloadString(root, "branch"),
            root.TryGetProperty("outputs", out var outputs) ? outputs.GetRawText() : "{}",
            ReadPayloadString(root, "errorCode"),
            ReadPayloadString(root, "errorMessage"),
            root.TryGetProperty("inputs", out var inputs) ? inputs.GetRawText() : "{}");
    }

    private static string? ReadPayloadString(JsonElement payload, string name)
        => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid? ReadPayloadGuid(JsonElement payload, string name)
        => payload.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static int ReadPayloadInt(JsonElement payload, string name)
        => payload.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var parsed)
            ? Math.Max(0, parsed)
            : 0;

    private static string ReadPayloadJson(JsonElement payload, string name)
        => payload.TryGetProperty(name, out var value)
            ? value.GetRawText()
            : "{}";


    private static string ResolvePath(string value, string root)
        => Path.IsPathRooted(value) ? value : Path.Combine(root, value);

    private sealed class ActiveDebugSession(IWorkerDebugRunHandle handle, RunExecutionQueue.RunExecutionLease lease)
    {
        public IWorkerDebugRunHandle Handle { get; } = handle;

        public RunExecutionQueue.RunExecutionLease Lease { get; } = lease;

        public SemaphoreSlim CommandGate { get; } = new(1, 1);
    }
}
