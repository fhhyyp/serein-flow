using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Api;

public sealed record RunWorkItem(
    Guid RunId,
    Guid ProjectId,
    FlowDefinitionDto Definition,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs,
    DateTimeOffset QueuedAt,
    int TimeoutSeconds,
    int MaxSteps,
    int MaxNodeVisits = 1_000);

public sealed class RunExecutionOptions
{
    public int QueueCapacity { get; init; } = 100;
    public int MaxConcurrentRuns { get; init; } = 4;
    public int MaxConcurrentListenerRuns { get; init; } = 1;
    public int MaxConcurrentRunsPerProject { get; init; } = 2;
    public int QueueWaitTimeoutSeconds { get; init; } = 60;
    public int ShutdownGracePeriodSeconds { get; init; } = 10;

    public int SynchronousInvocationTimeoutSeconds { get; init; } = 30;

    internal RunExecutionOptions Normalize()
        => new()
        {
            QueueCapacity = Math.Clamp(QueueCapacity, 1, 10_000),
            MaxConcurrentRuns = Math.Clamp(MaxConcurrentRuns, 1, 1_024),
            MaxConcurrentListenerRuns = Math.Clamp(MaxConcurrentListenerRuns, 0, 1_024),
            MaxConcurrentRunsPerProject = Math.Clamp(MaxConcurrentRunsPerProject, 1, 1_024),
            QueueWaitTimeoutSeconds = Math.Clamp(QueueWaitTimeoutSeconds, 1, 86_400),
            ShutdownGracePeriodSeconds = Math.Clamp(ShutdownGracePeriodSeconds, 1, 300),
            SynchronousInvocationTimeoutSeconds = Math.Clamp(SynchronousInvocationTimeoutSeconds, 1, 300)
        };

    public RunExecutionSettingsDto ToDto()
        => new(
            QueueCapacity,
            MaxConcurrentRuns,
            MaxConcurrentListenerRuns,
            MaxConcurrentRunsPerProject,
            QueueWaitTimeoutSeconds,
            ShutdownGracePeriodSeconds,
            SynchronousInvocationTimeoutSeconds);

    public static RunExecutionOptions FromDto(RunExecutionSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new RunExecutionOptions
        {
            QueueCapacity = settings.QueueCapacity,
            MaxConcurrentRuns = settings.MaxConcurrentRuns,
            MaxConcurrentListenerRuns = settings.MaxConcurrentListenerRuns,
            MaxConcurrentRunsPerProject = settings.MaxConcurrentRunsPerProject,
            QueueWaitTimeoutSeconds = settings.QueueWaitTimeoutSeconds,
            ShutdownGracePeriodSeconds = settings.ShutdownGracePeriodSeconds,
            SynchronousInvocationTimeoutSeconds = settings.SynchronousInvocationTimeoutSeconds,
        }.Normalize();
    }
}

public sealed record RunExecutionSnapshot(
    int QueueCapacity,
    int QueuedCount,
    int ActiveRunCount,
    int ActiveListenerRunCount,
    int MaxConcurrentRuns,
    int MaxConcurrentListenerRuns,
    int MaxConcurrentRunsPerProject);

public sealed record RunSubmissionResult(
    FlowRun? Run,
    int StatusCode,
    string? ErrorTitle = null,
    object? ErrorBody = null,
    long? CurrentVersion = null)
{
    public bool IsAccepted => Run is not null;
}

public sealed class RunSubmissionService
{
    private readonly RunApplicationService _runService;
    private readonly RunExecutionQueue _queue;
    private readonly IFlowRunStore _runStore;

    public RunSubmissionService(
        RunApplicationService runService,
        RunExecutionQueue queue,
        IFlowRunStore runStore)
    {
        _runService = runService;
        _queue = queue;
        _runStore = runStore;
    }

    public async Task<RunSubmissionResult> SubmitAsync(
        Guid projectId,
        Guid flowId,
        RunFlowRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var reservation = _queue.TryReserve();
        if (reservation is null)
        {
            return new RunSubmissionResult(
                null,
                StatusCodes.Status429TooManyRequests,
                "The run queue is full. 运行队列已满。",
                new { code = "run.queue_full" });
        }

        var preparation = await _runService.PrepareAsync(projectId, flowId, request, cancellationToken);
        if (!preparation.IsSuccess)
        {
            return new RunSubmissionResult(
                null,
                preparation.StatusCode,
                preparation.ErrorTitle,
                preparation.ErrorBody,
                preparation.CurrentVersion);
        }

        var prepared = preparation.Preparation!;
        var item = new RunWorkItem(
            prepared.Run.Id,
            projectId,
            prepared.Definition,
            prepared.ProjectInputs,
            prepared.Run.QueuedAt,
            prepared.TimeoutSeconds,
            prepared.MaxSteps,
            prepared.MaxNodeVisits);
        if (_queue.TryEnqueueReserved(item, reservation))
            return new RunSubmissionResult(prepared.Run, StatusCodes.Status202Accepted);

        prepared.Run.Cancel("run.scheduler_unavailable", DateTimeOffset.UtcNow);
        await _runStore.SaveAsync(prepared.Run, CancellationToken.None);
        return new RunSubmissionResult(
            null,
            StatusCodes.Status503ServiceUnavailable,
            "The run scheduler is unavailable. 运行调度器不可用。",
            new { code = "run.scheduler_unavailable" });
    }
}

public sealed class RunExecutionQueue
{
    private readonly Channel<RunWorkItem> _queue;
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private readonly ConcurrentDictionary<Guid, ActiveRunInfo> _activeRuns = new();
    // A single pending wake-up is enough: scheduling always re-evaluates all
    // work and limits. This prevents signal counts accumulating under load.
    // 一个待处理唤醒信号已经足够：调度会重新检查所有任务和限制，避免高负载下信号计数累积。
    private readonly SemaphoreSlim _schedulerSignal = new(0, 1);
    private readonly object _gate = new();
    private RunExecutionOptions _options;
    private int _queuedCount;
    private bool _accepting = true;

    public RunExecutionQueue(IOptions<RunExecutionOptions> options)
    {
        _options = (options?.Value ?? new RunExecutionOptions()).Normalize();
        // Capacity is enforced by a reservation counter so operators can
        // safely change it without replacing a channel containing work.
        // 容量通过预留计数器限制，使运维人员调整上限时无需替换仍有任务的通道。
        _queue = Channel.CreateUnbounded<RunWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public RunExecutionOptions Options
    {
        get
        {
            lock (_gate)
                return _options;
        }
    }

    public RunExecutionSettingsDto Configure(RunExecutionSettingsDto settings)
    {
        lock (_gate)
        {
            _options = RunExecutionOptions.FromDto(settings);
        }
        SignalScheduler();
        return Options.ToDto();
    }

    public RunQueueReservation? TryReserve()
    {
        lock (_gate)
        {
            if (!_accepting || _queuedCount >= _options.QueueCapacity)
                return null;
            Interlocked.Increment(ref _queuedCount);
            return new RunQueueReservation(this);
        }
    }

    public bool TryEnqueueReserved(RunWorkItem item, RunQueueReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!reservation.TryClaim(this))
            throw new InvalidOperationException("The queue reservation is not valid. 队列预留无效。");

        var source = new CancellationTokenSource();
        lock (_gate)
        {
            if (!_accepting || !_queue.Writer.TryWrite(item))
            {
                source.Dispose();
                ReleaseQueueSlot();
                return false;
            }
            _cancellations[item.RunId] = source;
        }
        SignalScheduler();
        return true;
    }

    public bool TryRead(out RunWorkItem item)
    {
        if (_queue.Reader.TryRead(out var next))
        {
            item = next;
            return true;
        }

        item = default!;
        return false;
    }

    public CancellationToken GetCancellationToken(Guid runId)
    {
        lock (_gate)
        {
            return _cancellations.TryGetValue(runId, out var source)
                ? source.Token
                : new CancellationToken(canceled: true);
        }
    }

    public bool Cancel(Guid runId)
    {
        lock (_gate)
        {
            if (!_cancellations.TryGetValue(runId, out var source))
                return false;
            source.Cancel();
        }
        SignalScheduler();
        return true;
    }

    public bool IsCancellationRequested(Guid runId)
    {
        lock (_gate)
        {
            return _cancellations.TryGetValue(runId, out var source) && source.IsCancellationRequested;
        }
    }

    public bool IsTracked(Guid runId)
    {
        lock (_gate)
        {
            return _cancellations.ContainsKey(runId) || _activeRuns.ContainsKey(runId);
        }
    }

    public void RemoveQueued(Guid runId)
    {
        RemoveCancellation(runId);
        ReleaseQueueSlot();
    }

    public void PromoteToActive(RunWorkItem item)
    {
        ReleaseQueueSlot();
        SignalScheduler();
    }

    public bool TryAcquireExecutionSlots(RunWorkItem item, out RunExecutionLease? lease)
    {
        lease = null;
        lock (_gate)
        {
            if (_activeRuns.Count >= _options.MaxConcurrentRuns)
                return false;

            var listenerRun = IsListenerRun(item);
            if (listenerRun && _activeRuns.Values.Count(static run => run.IsListenerRun) >= _options.MaxConcurrentListenerRuns)
                return false;

            if (_activeRuns.Values.Count(run => run.ProjectId == item.ProjectId) >= _options.MaxConcurrentRunsPerProject)
                return false;

            if (!_activeRuns.TryAdd(item.RunId, new ActiveRunInfo(item.ProjectId, listenerRun)))
                return false;

            lease = new RunExecutionLease(this, item.RunId);
            return true;
        }
    }

    public RunExecutionSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new(
                _options.QueueCapacity,
                Math.Max(0, Volatile.Read(ref _queuedCount)),
                _activeRuns.Count,
                _activeRuns.Values.Count(static item => item.IsListenerRun),
                _options.MaxConcurrentRuns,
                _options.MaxConcurrentListenerRuns,
                _options.MaxConcurrentRunsPerProject);
        }
    }

    public async Task<bool> WaitForSchedulerSignalAsync(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (timeout is { } value && value > TimeSpan.Zero)
            return await _schedulerSignal.WaitAsync(value, cancellationToken);

        await _schedulerSignal.WaitAsync(cancellationToken);
        return true;
    }

    public void StopAcceptingAndCancelAll()
    {
        CancellationTokenSource[] sources;
        lock (_gate)
        {
            _accepting = false;
            _queue.Writer.TryComplete();
            sources = _cancellations.Values.ToArray();
        }
        foreach (var source in sources)
            source.Cancel();
        SignalScheduler();
    }

    private void CompleteActive(Guid runId)
    {
        _activeRuns.TryRemove(runId, out _);
        RemoveCancellation(runId);
        SignalScheduler();
    }

    private void ReleaseQueueSlot()
    {
        while (true)
        {
            var current = Volatile.Read(ref _queuedCount);
            if (current <= 0)
                return;
            if (Interlocked.CompareExchange(ref _queuedCount, current - 1, current) == current)
                return;
        }
    }

    private void RemoveCancellation(Guid runId)
    {
        CancellationTokenSource? source = null;
        lock (_gate)
        {
            if (_cancellations.Remove(runId, out var existing))
                source = existing;
        }
        source?.Dispose();
    }

    private void ReleaseExecutionSlots(Guid runId)
    {
        CompleteActive(runId);
    }

    private void SignalScheduler()
    {
        try
        {
            _schedulerSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // The scheduler can coalesce redundant wake-ups.
            // 调度器可以合并重复唤醒信号。
        }
    }

    private static bool IsListenerRun(RunWorkItem item)
    {
        var incoming = item.Definition.Canvases
            .SelectMany(static canvas => canvas.Connections)
            .Where(static connection => connection.Kind == ConnectionKindDto.Execution)
            .Select(static connection => connection.ToNodeId)
            .ToHashSet(StringComparer.Ordinal);
        return item.Definition.Canvases
            .SelectMany(static canvas => canvas.Nodes)
            .Any(node => node.Type == NodeTypeDto.Flipflop && !incoming.Contains(node.Id));
    }

    private sealed record ActiveRunInfo(Guid ProjectId, bool IsListenerRun);

    public sealed class RunQueueReservation : IDisposable
    {
        private RunExecutionQueue? _owner;

        internal RunQueueReservation(RunExecutionQueue owner) => _owner = owner;

        internal bool TryClaim(RunExecutionQueue owner)
            => ReferenceEquals(Interlocked.Exchange(ref _owner, null), owner);

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseQueueSlot();
        }
    }

    public sealed class RunExecutionLease : IDisposable
    {
        private RunExecutionQueue? _owner;
        private readonly Guid _runId;

        internal RunExecutionLease(RunExecutionQueue owner, Guid runId)
        {
            _owner = owner;
            _runId = runId;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseExecutionSlots(_runId);
        }
    }
}

public sealed class RunEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>> _channels = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _completedRuns = new();
    private static readonly TimeSpan CompletedRunRetention = TimeSpan.FromMinutes(10);

    public ChannelReader<FlowRunEventDto> Subscribe(Guid runId, CancellationToken cancellationToken = default)
    {
        CleanupCompletedRuns();
        var subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<FlowRunEventDto>();
        if (_completedRuns.ContainsKey(runId))
        {
            channel.Writer.TryComplete();
            return channel.Reader;
        }

        var subscribers = _channels.GetOrAdd(runId, static _ => new ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>());
        subscribers[subscriptionId] = channel;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => RemoveSubscription(runId, subscriptionId));
        }

        // The run may finish between the completed check and subscription.
        // Complete the new reader instead of retaining it after terminal state.
        // 运行可能在完成状态检查和订阅之间结束；终态时完成新读者，避免保留订阅。
        if (_completedRuns.ContainsKey(runId))
            RemoveSubscription(runId, subscriptionId, complete: true);
        return channel.Reader;
    }

    public ValueTask PublishAsync(FlowRunEventDto item, CancellationToken cancellationToken = default)
    {
        CleanupCompletedRuns();
        if (_channels.TryGetValue(item.RunId, out var subscribers))
        {
            foreach (var channel in subscribers.Values)
                channel.Writer.TryWrite(item);
        }

        if (item.Type is "run.completed" or "run.failed" or "run.cancelled" or "run.timed_out")
        {
            _completedRuns[item.RunId] = DateTimeOffset.UtcNow;
            if (_channels.TryRemove(item.RunId, out var completed))
            {
                foreach (var channel in completed.Values)
                    channel.Writer.TryComplete();
            }
        }

        return ValueTask.CompletedTask;
    }

    private void CleanupCompletedRuns()
    {
        var expiresAt = DateTimeOffset.UtcNow - CompletedRunRetention;
        foreach (var completed in _completedRuns)
        {
            if (completed.Value <= expiresAt)
                _completedRuns.TryRemove(completed);
        }
    }

    private void RemoveSubscription(Guid runId, Guid subscriptionId, bool complete = true)
    {
        if (!_channels.TryGetValue(runId, out var subscribers)
            || !subscribers.TryRemove(subscriptionId, out var channel))
            return;

        if (complete)
            channel.Writer.TryComplete();
        if (subscribers.IsEmpty)
            _channels.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>>(runId, subscribers));
    }
}

public sealed class RunEventsHub : Hub
{
    public Task Subscribe(Guid runId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"run:{runId:D}");

    public Task Unsubscribe(Guid runId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run:{runId:D}");
}

public sealed class RunExecutionHostedService : BackgroundService
{
    private static readonly Action<ILogger, Guid, Exception?> WorkerRunFailedLog =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4101, "WorkerRunFailed"),
            "Worker run {RunId} failed before completion. Worker 运行在完成前失败。");

    private readonly RunExecutionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerRunClient _workerClient;
    private readonly RunEventBroadcaster _broadcaster;
    private readonly IHubContext<RunEventsHub> _hub;
    private readonly ILogger<RunExecutionHostedService> _logger;
    private readonly string _scriptRoot;
    private readonly string _libraryRoot;
    private readonly ConcurrentDictionary<Guid, Task> _activeTasks = new();

    public RunExecutionHostedService(
        RunExecutionQueue queue,
        IServiceScopeFactory scopeFactory,
        IWorkerRunClient workerClient,
        RunEventBroadcaster broadcaster,
        IHubContext<RunEventsHub> hub,
        ILogger<RunExecutionHostedService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _workerClient = workerClient;
        _broadcaster = broadcaster;
        _hub = hub;
        _logger = logger;
        _scriptRoot = ResolvePath(configuration["SereinFlow:ScriptArtifactRoot"] ?? "data/script-artifacts", environment.ContentRootPath);
        _libraryRoot = ResolvePath(configuration["SereinFlow:LibraryDirectory"] ?? "data/libraries", environment.ContentRootPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pending = new List<RunWorkItem>();
        try
        {
            await ApplyPersistedEnvironmentSettingsAsync(stoppingToken);
            await RecoverPendingRunsAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_queue.TryRead(out var item))
                    pending.Add(item);

                await FinalizeExpiredOrCancelledPendingAsync(pending, stoppingToken);
                for (var index = 0; index < pending.Count;)
                {
                    var item = pending[index];
                    if (!_queue.TryAcquireExecutionSlots(item, out var lease))
                    {
                        index++;
                        continue;
                    }

                    pending.RemoveAt(index);
                    _queue.PromoteToActive(item);
                    var executionTask = Task.Run(
                        () => ExecuteItemAsync(item, lease!, stoppingToken),
                        CancellationToken.None);
                    _activeTasks[item.RunId] = executionTask;
                    _ = executionTask.ContinueWith(
                        _ => _activeTasks.TryRemove(item.RunId, out Task? _),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }

                await RecoverPendingRunsAsync(stoppingToken);
                var wait = GetNextPendingExpiry(pending);
                await _queue.WaitForSchedulerSignalAsync(wait, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected. Pending runs remain recoverable and
            // active Workers receive cancellation through the shared queue.
            // 主机关闭属于预期行为；排队实例保持可恢复，活动 Worker 通过共享队列收到取消。
        }
        finally
        {
            _queue.StopAcceptingAndCancelAll();
            var active = _activeTasks.Values.ToArray();
            if (active.Length > 0)
            {
                await Task.WhenAny(
                    Task.WhenAll(active),
                    Task.Delay(TimeSpan.FromSeconds(_queue.Options.ShutdownGracePeriodSeconds), CancellationToken.None));
            }
        }
    }

    private async Task ApplyPersistedEnvironmentSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRunEnvironmentSettingsStore>();
        var settings = await store.GetAsync(cancellationToken);
        if (settings is not null)
            _queue.Configure(settings);
    }

    private async Task RecoverPendingRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        foreach (var pending in await runStore.ListPendingAsync(cancellationToken))
        {
            if (_queue.IsTracked(pending.Run.Id))
                continue;

            using var reservation = _queue.TryReserve();
            if (reservation is null)
                break;

            var item = new RunWorkItem(
                pending.Run.Id,
                pending.Run.ProjectId,
                pending.Definition,
                pending.Options.ProjectInputs,
                pending.Run.QueuedAt,
                pending.Options.TimeoutSeconds,
                pending.Options.MaxSteps,
                pending.Options.MaxNodeVisits);
            if (!_queue.TryEnqueueReserved(item, reservation))
            {
                _logger.LogWarning(
                    "Recovered run {RunId} could not be returned to the scheduler. 恢复的运行实例无法重新加入调度器。",
                    item.RunId);
                break;
            }
        }
    }

    private async Task FinalizeExpiredOrCancelledPendingAsync(List<RunWorkItem> pending, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        for (var index = pending.Count - 1; index >= 0; index--)
        {
            var item = pending[index];
            if (_queue.IsCancellationRequested(item.RunId))
            {
                pending.RemoveAt(index);
                await CompletePendingRunAsync(
                    item,
                    FlowRunStatusDto.Cancelled,
                    "run.cancelled",
                    "The queued run was cancelled. 排队中的运行实例已取消。",
                    cancellationToken);
                continue;
            }

            if (now - item.QueuedAt < TimeSpan.FromSeconds(_queue.Options.QueueWaitTimeoutSeconds))
                continue;

            pending.RemoveAt(index);
            await CompletePendingRunAsync(
                item,
                FlowRunStatusDto.TimedOut,
                "run.queue_timeout",
                "The run exceeded the queue wait timeout. 运行实例超过了队列等待超时。",
                cancellationToken);
        }
    }

    private TimeSpan? GetNextPendingExpiry(IReadOnlyCollection<RunWorkItem> pending)
    {
        if (pending.Count == 0)
            return null;

        var now = DateTimeOffset.UtcNow;
        var deadline = pending.Min(item => item.QueuedAt.AddSeconds(_queue.Options.QueueWaitTimeoutSeconds));
        return deadline <= now ? TimeSpan.FromMilliseconds(1) : deadline - now;
    }

    private async Task CompletePendingRunAsync(
        RunWorkItem item,
        FlowRunStatusDto terminalStatus,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var run = await runStore.FindAsync(item.RunId, cancellationToken);
            if (run is not null && !run.IsTerminal)
            {
                if (terminalStatus == FlowRunStatusDto.Cancelled)
                    run.Cancel(code, DateTimeOffset.UtcNow);
                else
                    run.Complete(FlowRunStatus.TimedOut, DateTimeOffset.UtcNow, message);
                await runStore.SaveAsync(run, CancellationToken.None);
                await PublishTerminalAsync(
                    run,
                    new WorkerRunResultDto(WorkerProtocol.Version, run.Id, terminalStatus, code, message),
                    eventStore,
                    CancellationToken.None);
            }
        }
        finally
        {
            _queue.RemoveQueued(item.RunId);
        }
    }

    private async Task ExecuteItemAsync(
        RunWorkItem item,
        RunExecutionQueue.RunExecutionLease lease,
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var outputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                _queue.GetCancellationToken(item.RunId));
            var runToken = cancellation.Token;
            var run = await runStore.FindAsync(item.RunId, runToken);
            if (run is null || run.IsTerminal)
                return;
            if (runToken.IsCancellationRequested)
            {
                run.Cancel("run.cancelled", DateTimeOffset.UtcNow);
                await runStore.SaveAsync(run, CancellationToken.None);
                await PublishTerminalAsync(
                    run,
                    new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, "run.cancelled", "The queued run was cancelled. 排队中的运行实例已取消。"),
                    eventStore,
                    CancellationToken.None);
                return;
            }

            _logger.LogInformation(
                "Starting flow run {RunId} for project {ProjectId}, flow {FlowId}. 开始运行流程实例。",
                item.RunId,
                item.ProjectId,
                item.Definition.Id);
            var startedAt = DateTimeOffset.UtcNow;
            run.MarkRunning(startedAt, startedAt.AddSeconds(item.TimeoutSeconds));
            await runStore.SaveAsync(run, runToken);

            var request = new WorkerRunRequestDto(
                WorkerProtocol.Version,
                item.RunId,
                item.Definition.Id,
                item.Definition.Version,
                JsonSerializer.Serialize(item.Definition, SereinJsonSerialization.CreateWebOptions()),
                run.Deadline!.Value,
                item.ProjectId.ToString("D"),
                item.ProjectInputs,
                item.MaxSteps,
                _scriptRoot,
                _libraryRoot,
                item.MaxNodeVisits,
                ProjectLibraryService.GetLibraryIds(item.Definition));

            var result = await _workerClient.RunAsync(
                request,
                new DelegateWorkerRunEventSink((workerEvent, token) => HandleEventAsync(workerEvent, runStore, eventStore, outputStore, token)),
                runToken);

            if (result.Status == FlowRunStatusDto.Cancelled)
                run.Cancel(result.ErrorCode ?? "cancelled", DateTimeOffset.UtcNow);
            else
                run.Complete(ToDomainStatus(result.Status), DateTimeOffset.UtcNow, result.ErrorMessage);
            await runStore.SaveAsync(run, CancellationToken.None);
            await PublishTerminalAsync(run, result, eventStore, CancellationToken.None);
            _logger.LogInformation(
                "Flow run {RunId} finished with status {Status}. 流程实例已结束。",
                item.RunId,
                run.Status);
        }
        catch (OperationCanceledException)
        {
            await MarkCancelledAfterStartAsync(item.RunId);
        }
        catch (Exception exception)
        {
            WorkerRunFailedLog(_logger, item.RunId, exception);
            await MarkFailedAfterStartAsync(item.RunId, exception);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private async Task MarkCancelledAfterStartAsync(Guid runId)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var run = await runStore.FindAsync(runId, CancellationToken.None);
        if (run is null || run.IsTerminal)
            return;
        run.Cancel("worker.cancelled", DateTimeOffset.UtcNow);
        await runStore.SaveAsync(run, CancellationToken.None);
        await PublishTerminalAsync(
            run,
            new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, "worker.cancelled", "The worker run was cancelled. Worker 运行已取消。"),
            eventStore,
            CancellationToken.None);
    }

    private async Task MarkFailedAfterStartAsync(Guid runId, Exception exception)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var run = await runStore.FindAsync(runId, CancellationToken.None);
        if (run is null || run.IsTerminal)
            return;
        var errorMessage = $"Worker run failed. Worker 运行失败。 {exception.Message}";
        run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, errorMessage);
        await runStore.SaveAsync(run, CancellationToken.None);
        await PublishTerminalAsync(
            run,
            new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Failed, "worker.run_failed", errorMessage),
            eventStore,
            CancellationToken.None);
    }

    private async ValueTask HandleEventAsync(
        WorkerEventEnvelopeDto workerEvent,
        IFlowRunStore runStore,
        IFlowRunEventStore eventStore,
        IFlowRunOutputStore outputStore,
        CancellationToken cancellationToken)
    {
        if (workerEvent.ProtocolVersion != WorkerProtocol.Version
            || workerEvent.RunId == Guid.Empty
            || await runStore.FindAsync(workerEvent.RunId, cancellationToken) is null)
        {
            return;
        }

        // WorkerSupervisor already enforces monotonic sequences. Keep the
        // store boundary defensive so a custom client cannot duplicate events.
        // WorkerSupervisor 已校验序列；在持久化边界再次防御，避免自定义客户端写入重复事件。
        var lastSequence = await eventStore.GetLastSequenceAsync(workerEvent.RunId, cancellationToken);
        if (workerEvent.Sequence <= lastSequence)
            return;

        var type = workerEvent.EventType switch
        {
            WorkerEventType.RunStarted => "run.started",
            WorkerEventType.NodeStarted => "node.started",
            WorkerEventType.NodeCompleted => "node.completed",
            WorkerEventType.NodeFailed => "node.failed",
            WorkerEventType.NodeErrored => "node.error",
            WorkerEventType.RunCompleted => "run.completed",
            WorkerEventType.RunCancelled => "run.cancelled",
            _ => "log"
        };
        var item = new FlowRunEvent(
            workerEvent.RunId,
            workerEvent.Sequence,
            workerEvent.Timestamp,
            type,
            workerEvent.NodeId,
            workerEvent.PayloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var output = CreateNodeOutput(workerEvent);
        if (output is not null)
            await outputStore.AppendAsync([output], cancellationToken);
        var dto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        try
        {
            await _broadcaster.PublishAsync(dto, cancellationToken);
        }
        catch (Exception exception)
        {
            // A disconnected SSE reader must never abort the Worker process.
            // The durable event has already been stored and can be replayed.
            // SSE 客户端断开不能终止 Worker；事件已持久化，可通过回放补齐。
            _logger.LogWarning(exception, "Live run event broadcast failed for {RunId}. 实时事件广播失败。", item.RunId);
        }
        try
        {
            await _hub.Clients.Group($"run:{item.RunId:D}").SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "SignalR run event push failed for {RunId}. SignalR 运行事件推送失败。", item.RunId);
        }
    }

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

        try
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    "Node output payload must be a JSON object. 节点输出载荷必须是 JSON 对象。");
            }

            var root = payload.RootElement;
            var inputsJson = root.TryGetProperty("inputs", out var inputs)
                ? inputs.GetRawText()
                : "{}";
            var outputsJson = root.TryGetProperty("outputs", out var outputs)
                ? outputs.GetRawText()
                : "{}";
            return new FlowRunOutput(
                workerEvent.RunId,
                workerEvent.Sequence,
                workerEvent.Timestamp,
                workerEvent.NodeId,
                outcome,
                ReadPayloadString(root, "branch"),
                outputsJson,
                ReadPayloadString(root, "errorCode"),
                ReadPayloadString(root, "errorMessage"),
                inputsJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Worker node output payload is invalid. Worker 节点输出载荷无效。",
                exception);
        }
    }

    private static string? ReadPayloadString(JsonElement payload, string propertyName)
        => payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
        var sequence = await eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1;
        var item = new FlowRunEvent(
            run.Id,
            sequence,
            DateTimeOffset.UtcNow,
            type,
            null,
            JsonSerializer.Serialize(
                new { result.Status, result.ErrorCode, result.ErrorMessage },
                SereinJsonSerialization.CreateWebOptions()));
        await eventStore.AppendAsync([item], cancellationToken);
        var dto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        try
        {
            await _broadcaster.PublishAsync(dto, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Terminal run event broadcast failed for {RunId}. 终止事件广播失败。", run.Id);
        }
        try
        {
            await _hub.Clients.Group($"run:{run.Id:D}").SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Terminal SignalR event push failed for {RunId}. 终止 SignalR 事件推送失败。", run.Id);
        }
    }

    private static FlowRunStatus ToDomainStatus(FlowRunStatusDto status)
        => status switch
        {
            FlowRunStatusDto.Succeeded => FlowRunStatus.Succeeded,
            FlowRunStatusDto.Cancelled => FlowRunStatus.Cancelled,
            FlowRunStatusDto.TimedOut => FlowRunStatus.TimedOut,
            _ => FlowRunStatus.Failed
        };

    private static string ResolvePath(string value, string root)
        => Path.IsPathRooted(value) ? value : Path.Combine(root, value);
}
