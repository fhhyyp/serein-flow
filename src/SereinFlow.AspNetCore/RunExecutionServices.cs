using System;
using System.Threading;
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
public static class RunCancellationSources
{
    public const string Api = "api";
    public const string HostShutdown = "host_shutdown";
    public const string Worker = "worker";
}

public sealed record RunWorkItem(Guid RunId, Guid ProjectId, FlowDefinitionDto Definition, IReadOnlyDictionary<string, JsonElement>? ProjectInputs, DateTimeOffset QueuedAt, int TimeoutSeconds, int MaxSteps, int MaxNodeVisits = 1_000);
public sealed class RunExecutionOptions
{
    private const int MinimumPositiveSetting = 1;
    private const int MinimumListenerCount = 0;
    private const int QueueCapacityLimit = 10_000;
    private const int ConcurrentRunCountLimit = 1_024;
    private const int ConcurrentListenerCountLimit = 1_024;
    private const int ConcurrentProjectRunCountLimit = 1_024;
    private const int QueueWaitTimeoutLimitSeconds = 86_400;
    private const int ShutdownGracePeriodLimitSeconds = 300;
    private const int SynchronousInvocationTimeoutLimitSeconds = 300;
    public int QueueCapacity { get; init; } = 100;
    public int MaxConcurrentRuns { get; init; } = 4;
    public int MaxConcurrentListenerRuns { get; init; } = 1;
    public int MaxConcurrentRunsPerProject { get; init; } = 2;
    public int QueueWaitTimeoutSeconds { get; init; } = 60;
    public int ShutdownGracePeriodSeconds { get; init; } = 10;
    public int SynchronousInvocationTimeoutSeconds { get; init; } = 30;
    public long MaxLibraryUploadBytes { get; init; } = FileUploadLimits.DefaultMaxFileSizeBytes;

    internal RunExecutionOptions Normalize()
    {
        RunExecutionOptions evaluatedValue = new()
        {
            QueueCapacity = Math.Clamp(QueueCapacity, MinimumPositiveSetting, QueueCapacityLimit),
            MaxConcurrentRuns = Math.Clamp(MaxConcurrentRuns, MinimumPositiveSetting, ConcurrentRunCountLimit),
            MaxConcurrentListenerRuns = Math.Clamp(MaxConcurrentListenerRuns, MinimumListenerCount, ConcurrentListenerCountLimit),
            MaxConcurrentRunsPerProject = Math.Clamp(MaxConcurrentRunsPerProject, MinimumPositiveSetting, ConcurrentProjectRunCountLimit),
            QueueWaitTimeoutSeconds = Math.Clamp(QueueWaitTimeoutSeconds, MinimumPositiveSetting, QueueWaitTimeoutLimitSeconds),
            ShutdownGracePeriodSeconds = Math.Clamp(ShutdownGracePeriodSeconds, MinimumPositiveSetting, ShutdownGracePeriodLimitSeconds),
            SynchronousInvocationTimeoutSeconds = Math.Clamp(SynchronousInvocationTimeoutSeconds, MinimumPositiveSetting, SynchronousInvocationTimeoutLimitSeconds),
            MaxLibraryUploadBytes = FileUploadLimits.Normalize(MaxLibraryUploadBytes)
        };
        return evaluatedValue;
    }

    public RunExecutionSettingsDto ToDto()
    {
        RunExecutionSettingsDto evaluatedValue1 = new(QueueCapacity, MaxConcurrentRuns, MaxConcurrentListenerRuns, MaxConcurrentRunsPerProject, QueueWaitTimeoutSeconds, ShutdownGracePeriodSeconds, SynchronousInvocationTimeoutSeconds, MaxLibraryUploadBytes);
        return evaluatedValue1;
    }

    public static RunExecutionOptions FromDto(RunExecutionSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var runExecutionOptions = new RunExecutionOptions
        {
            QueueCapacity = settings.QueueCapacity,
            MaxConcurrentRuns = settings.MaxConcurrentRuns,
            MaxConcurrentListenerRuns = settings.MaxConcurrentListenerRuns,
            MaxConcurrentRunsPerProject = settings.MaxConcurrentRunsPerProject,
            QueueWaitTimeoutSeconds = settings.QueueWaitTimeoutSeconds,
            ShutdownGracePeriodSeconds = settings.ShutdownGracePeriodSeconds,
            SynchronousInvocationTimeoutSeconds = settings.SynchronousInvocationTimeoutSeconds,
            MaxLibraryUploadBytes = settings.MaxLibraryUploadBytes,
        };
        var normalizeResult = runExecutionOptions.Normalize();
        return normalizeResult;
    }
}

public sealed record RunExecutionSnapshot(int QueueCapacity, int QueuedCount, int ActiveRunCount, int ActiveListenerRunCount, int MaxConcurrentRuns, int MaxConcurrentListenerRuns, int MaxConcurrentRunsPerProject);
public sealed record RunSubmissionResult(FlowRun? Run, int StatusCode, string? ErrorTitle = null, object? ErrorBody = null, long? CurrentVersion = null)
{
    public bool IsAccepted => Run is not null;
}

public sealed class RunSubmissionService
{
    private readonly RunApplicationService _runService;
    private readonly RunExecutionQueue _queue;
    private readonly IFlowRunStore _runStore;
    public RunSubmissionService(RunApplicationService runService, RunExecutionQueue queue, IFlowRunStore runStore)
    {
        _runService = runService;
        _queue = queue;
        _runStore = runStore;
    }

    public async Task<RunSubmissionResult> SubmitAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        var submitCoreAsyncResult = await SubmitCoreAsync(projectId, flowId, request, definitionOverride: null, cancellationToken);
        return submitCoreAsyncResult;
    }

    public async Task<RunSubmissionResult> SubmitDefinitionAsync(Guid projectId, FlowDefinitionDto definition, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var submitCoreAsyncResult2 = await SubmitCoreAsync(projectId, definition.Id, request, definition, cancellationToken);
        return submitCoreAsyncResult2;
    }

    public async Task<RunSubmissionResult> SubmitProductionAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        var submitCoreAsyncResult3 = await SubmitCoreAsync(projectId, flowId, request, definitionOverride: null, cancellationToken: cancellationToken, definitionTrack: FlowVersionTrackDto.Production);
        return submitCoreAsyncResult3;
    }

    private async Task<RunSubmissionResult> SubmitCoreAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, FlowDefinitionDto? definitionOverride, CancellationToken cancellationToken, FlowVersionTrackDto definitionTrack = FlowVersionTrackDto.Development)
    {
        using var reservation = _queue.TryReserve();
        if (reservation is null)
        {
            var runSubmissionResult = new RunSubmissionResult(null, StatusCodes.Status429TooManyRequests, "The run queue is full. 运行队列已满。", new { code = RunErrorCodes.QueueFull });
            return runSubmissionResult;
        }

        var preparation = await _runService.PrepareAsync(projectId, flowId, request, definitionOverride: definitionOverride, cancellationToken: cancellationToken, definitionTrack: definitionTrack);
        if (!preparation.IsSuccess)
        {
            var runSubmissionResult4 = new RunSubmissionResult(null, preparation.StatusCode, preparation.ErrorTitle, preparation.ErrorBody, preparation.CurrentVersion);
            return runSubmissionResult4;
        }

        var prepared = preparation.Preparation!;
        var item = new RunWorkItem(prepared.Run.Id, projectId, prepared.Definition, prepared.ProjectInputs, prepared.Run.QueuedAt, prepared.TimeoutSeconds, prepared.MaxSteps, prepared.MaxNodeVisits);
        if (_queue.TryEnqueueReserved(item, reservation))
        {
            var runSubmissionResult5 = new RunSubmissionResult(prepared.Run, StatusCodes.Status202Accepted);
            return runSubmissionResult5;
        }

        prepared.Run.Cancel(RunErrorCodes.SchedulerUnavailable, DateTimeOffset.UtcNow);
        await _runStore.SaveAsync(prepared.Run, CancellationToken.None);
        var runSubmissionResult6 = new RunSubmissionResult(null, StatusCodes.Status503ServiceUnavailable, "The run scheduler is unavailable. 运行调度器不可用。", new { code = RunErrorCodes.SchedulerUnavailable });
        return runSubmissionResult6;
    }
}

public sealed class RunExecutionQueue
{
    private readonly Channel<RunWorkItem> _queue;
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private readonly Dictionary<Guid, string> _cancellationSources = [];
    private readonly ConcurrentDictionary<Guid, ActiveRunInfo> _activeRuns = new();
    // A single pending wake-up is enough: scheduling always re-evaluates all
    // work and limits. This prevents signal counts accumulating under load.
    // 一个待处理唤醒信号已经足够：调度会重新检查所有任务和限制，避免高负载下信号计数累积。
    private readonly SemaphoreSlim _schedulerSignal = new(0, 1);
    private readonly object _gate = new();
    private RunExecutionOptions _options;
    private int _queuedCount;
    private bool _accepting = true;
    private readonly IFileUploadSettings _fileUploadSettings;
    public RunExecutionQueue(IOptions<RunExecutionOptions> options, IFileUploadSettings? fileUploadSettings = null)
    {
        var evaluatedValue7 = (options?.Value ?? new RunExecutionOptions());
        _options = evaluatedValue7.Normalize();
        _fileUploadSettings = fileUploadSettings ?? new FileUploadSettings();
        _fileUploadSettings.Configure(_options.MaxLibraryUploadBytes);
        var unboundedChannelOptions = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        // Capacity is enforced by a reservation counter so operators can
        // safely change it without replacing a channel containing work.
        // 容量通过预留计数器限制，使运维人员调整上限时无需替换仍有任务的通道。
        _queue = Channel.CreateUnbounded<RunWorkItem>(unboundedChannelOptions);
    }

    public RunExecutionOptions Options
    {
        get
        {
            lock (_gate)
            {
                return _options;
            }
        }
    }

    public RunExecutionSettingsDto Configure(RunExecutionSettingsDto settings)
    {
        lock (_gate)
        {
            _options = RunExecutionOptions.FromDto(settings);
            _fileUploadSettings.Configure(_options.MaxLibraryUploadBytes);
        }

        SignalScheduler();
        var toDtoResult = Options.ToDto();
        return toDtoResult;
    }

    public RunQueueReservation? TryReserve()
    {
        lock (_gate)
        {
            if (!_accepting || _queuedCount >= _options.QueueCapacity)
            {
                return null;
            }

            Interlocked.Increment(ref _queuedCount);
            var runQueueReservation = new RunQueueReservation(this);
            return runQueueReservation;
        }
    }

    public bool TryEnqueueReserved(RunWorkItem item, RunQueueReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!reservation.TryClaim(this))
        {
            throw new InvalidOperationException("The queue reservation is not valid. 队列预留无效。");
        }

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
            CancellationToken evaluatedValue8 = _cancellations.TryGetValue(runId, out var source) ? source.Token : new CancellationToken(canceled: true);
            return evaluatedValue8;
        }
    }

    public bool Cancel(Guid runId)
    {
        var cancelResult = Cancel(runId, RunCancellationSources.Api);
        return cancelResult;
    }

    public bool Cancel(Guid runId, string source)
    {
        CancellationTokenSource? cancellation = null;
        lock (_gate)
        {
            if (!_cancellations.TryGetValue(runId, out cancellation))
            {
                return false;
            }

            if (cancellation.IsCancellationRequested)
            {
                return false;
            }

            _cancellationSources[runId] = NormalizeCancellationSource(source);
        }

        cancellation!.Cancel();
        SignalScheduler();
        return true;
    }

    public string? GetCancellationSource(Guid runId)
    {
        lock (_gate)
        {
            string? evaluatedValue9 = _cancellationSources.TryGetValue(runId, out var source) ? source : null;
            return evaluatedValue9;
        }
    }

    public bool IsCancellationRequested(Guid runId)
    {
        lock (_gate)
        {
            var evaluatedValue10 = _cancellations.TryGetValue(runId, out var source) && source.IsCancellationRequested;
            return evaluatedValue10;
        }
    }

    public bool IsTracked(Guid runId)
    {
        lock (_gate)
        {
            var evaluatedValue11 = _cancellations.ContainsKey(runId) || _activeRuns.ContainsKey(runId);
            return evaluatedValue11;
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
            {
                return false;
            }

            var listenerRun = IsListenerRun(item);
            if (listenerRun && _activeRuns.Values.Count(static run => run.IsListenerRun) >= _options.MaxConcurrentListenerRuns)
            {
                return false;
            }

            if (_activeRuns.Values.Count(run => run.ProjectId == item.ProjectId) >= _options.MaxConcurrentRunsPerProject)
            {
                return false;
            }

            if (!resolveTryAddResult())
            {
                return false;
            }

            lease = new RunExecutionLease(this, item.RunId);
            return true;
            bool resolveTryAddResult()
            {
                var activeRunInfo = new ActiveRunInfo(item.ProjectId, listenerRun);
                var tryAddResult = _activeRuns.TryAdd(item.RunId, activeRunInfo);
                return tryAddResult;
            }
        }
    }

    /// <summary>
    /// Reserves an execution slot for an interactive session that must retain
    /// its Worker control handle. Unlike queued production runs, debug runs
    /// are rejected when no slot is available instead of being handed to the
    /// one-shot scheduler.
    /// 为必须保留 Worker 控制句柄的交互会话预留执行槽。与排队的生产运行不同，
    /// 调试运行没有可用槽时直接拒绝，不交给一次性调度器。
    /// </summary>
    public bool TryStartDirectExecution(RunWorkItem item, out RunExecutionLease? lease)
    {
        lease = null;
        CancellationTokenSource? source = null;
        lock (_gate)
        {
            if (!_accepting || _activeRuns.Count >= _options.MaxConcurrentRuns)
            {
                return false;
            }

            var listenerRun = IsListenerRun(item);
            if (listenerRun && _activeRuns.Values.Count(static run => run.IsListenerRun) >= _options.MaxConcurrentListenerRuns)
            {
                return false;
            }

            if (_activeRuns.Values.Count(run => run.ProjectId == item.ProjectId) >= _options.MaxConcurrentRunsPerProject)
            {
                return false;
            }

            if (_cancellations.ContainsKey(item.RunId))
            {
                return false;
            }

            if (!resolveTryAddResult12())
            {
                return false;
            }

            source = new CancellationTokenSource();
            _cancellations[item.RunId] = source;
            lease = new RunExecutionLease(this, item.RunId);
            bool resolveTryAddResult12()
            {
                var activeRunInfo13 = new ActiveRunInfo(item.ProjectId, listenerRun);
                var tryAddResult14 = _activeRuns.TryAdd(item.RunId, activeRunInfo13);
                return tryAddResult14;
            }
        }

        SignalScheduler();
        return true;
    }

    public RunExecutionSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var readResult = Volatile.Read(ref _queuedCount);
            var maxResult = Math.Max(0, readResult);
            var countResult = _activeRuns.Values.Count(static item => item.IsListenerRun);
            RunExecutionSnapshot evaluatedValue15 = new(_options.QueueCapacity, maxResult, _activeRuns.Count, countResult, _options.MaxConcurrentRuns, _options.MaxConcurrentListenerRuns, _options.MaxConcurrentRunsPerProject);
            return evaluatedValue15;
        }
    }

    public async Task<bool> WaitForSchedulerSignalAsync(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (timeout is { } value && value > TimeSpan.Zero)
        {
            var waitAsyncResult = await _schedulerSignal.WaitAsync(value, cancellationToken);
            return waitAsyncResult;
        }

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
            var whereResult = _cancellations.Where(static pair => !pair.Value.IsCancellationRequested);
            var selectResult = whereResult.Select(pair =>
            {
                _cancellationSources.TryAdd(pair.Key, RunCancellationSources.HostShutdown);
                return pair.Value;
            });
            sources = selectResult.ToArray();
        }

        foreach (var source in sources)
        {
            source.Cancel();
        }

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
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _queuedCount, current - 1, current) == current)
            {
                return;
            }
        }
    }

    private void RemoveCancellation(Guid runId)
    {
        CancellationTokenSource? source = null;
        lock (_gate)
        {
            if (_cancellations.Remove(runId, out var existing))
            {
                source = existing;
            }

            _cancellationSources.Remove(runId);
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
        var selectManyResult = item.Definition.Canvases.SelectMany(static canvas => canvas.Connections);
        var whereResult17 = selectManyResult.Where(static connection => connection.Kind == ConnectionKindDto.Execution);
        var selectResult18 = whereResult17.Select(static connection => connection.ToNodeId);
        var incoming = selectResult18.ToHashSet(StringComparer.Ordinal);
        var selectManyResult19 = item.Definition.Canvases.SelectMany(static canvas => canvas.Nodes);
        var anyResult = selectManyResult19.Any(node =>
        {
            var evaluatedValue16 = node.Type == NodeTypeDto.Flipflop && !incoming.Contains(node.Id);
            return evaluatedValue16;
        });
        return anyResult;
    }

    private static string NormalizeCancellationSource(string source)
    {
        string evaluatedValue20 = string.IsNullOrWhiteSpace(source) ? RunCancellationSources.Worker : source.Trim();
        return evaluatedValue20;
    }

    private sealed record ActiveRunInfo(Guid ProjectId, bool IsListenerRun);
    public sealed class RunQueueReservation : IDisposable
    {
        private RunExecutionQueue? _owner;
        internal RunQueueReservation(RunExecutionQueue owner)
        {
            _owner = owner;
        }

        internal bool TryClaim(RunExecutionQueue owner)
        {
            var exchangeResult = Interlocked.Exchange(ref _owner, null);
            var referenceEqualsResult = ReferenceEquals(exchangeResult, owner);
            return referenceEqualsResult;
        }

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

        var subscribers = _channels.GetOrAdd(runId, static _ =>
        {
            var concurrentDictionary = new ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>();
            return concurrentDictionary;
        });
        subscribers[subscriptionId] = channel;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                RemoveSubscription(runId, subscriptionId);
            });
        }

        // The run may finish between the completed check and subscription.
        // Complete the new reader instead of retaining it after terminal state.
        // 运行可能在完成状态检查和订阅之间结束；终态时完成新读者，避免保留订阅。
        if (_completedRuns.ContainsKey(runId))
        {
            RemoveSubscription(runId, subscriptionId, complete: true);
        }

        return channel.Reader;
    }

    public ValueTask PublishAsync(FlowRunEventDto item, CancellationToken cancellationToken = default)
    {
        CleanupCompletedRuns();
        if (_channels.TryGetValue(item.RunId, out var subscribers))
        {
            foreach (var channel in subscribers.Values)
            {
                channel.Writer.TryWrite(item);
            }
        }

        if (item.Type is RunErrorCodes.Completed or RunErrorCodes.Failed or RunErrorCodes.Cancelled or RunErrorCodes.TimedOut or RunErrorCodes.Interrupted)
        {
            _completedRuns[item.RunId] = DateTimeOffset.UtcNow;
            if (_channels.TryRemove(item.RunId, out var completed))
            {
                foreach (var channel in completed.Values)
                {
                    channel.Writer.TryComplete();
                }
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
            {
                _completedRuns.TryRemove(completed);
            }
        }
    }

    private void RemoveSubscription(Guid runId, Guid subscriptionId, bool complete = true)
    {
        if (!_channels.TryGetValue(runId, out var subscribers) || !subscribers.TryRemove(subscriptionId, out var channel))
        {
            return;
        }

        if (complete)
        {
            channel.Writer.TryComplete();
        }

        if (subscribers.IsEmpty)
        {
            var keyValuePair = new KeyValuePair<Guid, ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>>(runId, subscribers);
            _channels.TryRemove(keyValuePair);
        }
    }
}

public sealed class RunEventsHub : Hub
{
    public Task Subscribe(Guid runId)
    {
        var addToGroupAsyncResult = Groups.AddToGroupAsync(Context.ConnectionId, $"run:{runId:D}");
        return addToGroupAsyncResult;
    }

    public Task Unsubscribe(Guid runId)
    {
        var removeFromGroupAsyncResult = Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run:{runId:D}");
        return removeFromGroupAsyncResult;
    }
}

public sealed class RunExecutionHostedService : BackgroundService
{
    private static readonly Action<ILogger, Guid, Exception?> WorkerRunFailedLog = createWorkerRunFailedLogInitialValue();
    private static readonly Action<ILogger, Guid, Exception?> ReconciliationFailedLog = createReconciliationFailedLogInitialValue();
    private readonly RunExecutionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerRunClient _workerClient;
    private readonly RunEventBroadcaster _broadcaster;
    private readonly IHubContext<RunEventsHub> _hub;
    private readonly ILogger<RunExecutionHostedService> _logger;
    private readonly string _scriptRoot;
    private readonly string _libraryRoot;
    private readonly string _workpieceRoot;
    private readonly ConcurrentDictionary<Guid, Task> _activeTasks = new();
    public RunExecutionHostedService(RunExecutionQueue queue, IServiceScopeFactory scopeFactory, IWorkerRunClient workerClient, RunEventBroadcaster broadcaster, IHubContext<RunEventsHub> hub, ILogger<RunExecutionHostedService> logger, SereinFlow.Infrastructure.Configuration.SereinFlowStorageOptions storage)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _workerClient = workerClient;
        _broadcaster = broadcaster;
        _hub = hub;
        _logger = logger;
        _scriptRoot = storage.ScriptArtifactRoot;
        _libraryRoot = storage.LibraryDirectory;
        _workpieceRoot = storage.WorkpieceDirectory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pending = new List<RunWorkItem>();
        try
        {
            await ApplyPersistedEnvironmentSettingsAsync(stoppingToken);
            await ReconcileInterruptedRunsAsync(stoppingToken);
            await RecoverPendingRunsAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_queue.TryRead(out var item))
                {
                    pending.Add(item);
                }

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
                    var executionTask = Task.Run(() =>
                    {
                        var executeItemAsyncResult = ExecuteItemAsync(item, lease!, stoppingToken);
                        return executeItemAsyncResult;
                    }, CancellationToken.None);
                    _activeTasks[item.RunId] = executionTask;
                    _ = executionTask.ContinueWith(_ =>
                    {
                        var tryRemoveResult = _activeTasks.TryRemove(item.RunId, out Task? _);
                        return tryRemoveResult;
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }

                await RecoverPendingRunsAsync(stoppingToken);
                var wait = GetNextPendingExpiry(pending);
                await _queue.WaitForSchedulerSignalAsync(wait, stoppingToken);
            }
        }
        catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
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
                var whenAllResult = Task.WhenAll(active);
                var fromSecondsResult = TimeSpan.FromSeconds(_queue.Options.ShutdownGracePeriodSeconds);
                var delayResult = Task.Delay(fromSecondsResult, CancellationToken.None);
                await Task.WhenAny(whenAllResult, delayResult);
            }
        }
    }

    private async Task ApplyPersistedEnvironmentSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRunEnvironmentSettingsStore>();
        var settings = await store.GetAsync(cancellationToken);
        if (settings is not null)
        {
            _queue.Configure(settings);
        }
    }

    private async Task RecoverPendingRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        foreach (var pending in await runStore.ListPendingAsync(cancellationToken))
        {
            if (_queue.IsTracked(pending.Run.Id))
            {
                continue;
            }

            using var reservation = _queue.TryReserve();
            if (reservation is null)
            {
                break;
            }

            var item = new RunWorkItem(pending.Run.Id, pending.Run.ProjectId, pending.Definition, pending.Options.ProjectInputs, pending.Run.QueuedAt, pending.Options.TimeoutSeconds, pending.Options.MaxSteps, pending.Options.MaxNodeVisits);
            if (!_queue.TryEnqueueReserved(item, reservation))
            {
                _logger.LogWarning("Recovered run {RunId} could not be returned to the scheduler. 恢复的运行实例无法重新加入调度器。", item.RunId);
                break;
            }
        }
    }

    private async Task ReconcileInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var interruptionService = scope.ServiceProvider.GetRequiredService<RunInterruptionService>();
        var flowRunQuery = new FlowRunQuery([FlowRunStatus.Running], Take: 10_000);
        var running = await runStore.ListAsync(flowRunQuery, cancellationToken);
        var reconciled = 0;
        foreach (var run in running)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await interruptionService.InterruptAsync(run.Id, "engine_restart", RunErrorCodes.WorkerLost, "The worker was lost before the run reached a terminal state. Worker 在流程到达终态前已丢失。", cancellationToken);
                if (result.IsInterrupted)
                {
                    reconciled++;
                }
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReconciliationFailedLog(_logger, run.Id, exception);
            }
        }

        if (reconciled > 0)
        {
            _logger.LogWarning("Reconciled {Count} orphaned running flow run(s) after engine startup. 引擎启动后已将这些孤儿运行实例标记为中断。", reconciled);
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
                var evaluatedValue22 = _queue.GetCancellationSource(item.RunId) ?? RunCancellationSources.Worker;
                await CompletePendingRunAsync(item, FlowRunStatusDto.Cancelled, RunErrorCodes.Cancelled, "The queued run was cancelled. 排队中的运行实例已取消。", evaluatedValue22, cancellationToken);
                continue;
            }

            if (now - item.QueuedAt < TimeSpan.FromSeconds(_queue.Options.QueueWaitTimeoutSeconds))
            {
                continue;
            }

            pending.RemoveAt(index);
            await CompletePendingRunAsync(item, FlowRunStatusDto.TimedOut, RunErrorCodes.QueueTimeout, "The run exceeded the queue wait timeout. 运行实例超过了队列等待超时。", RunCancellationSources.Worker, cancellationToken);
        }
    }

    private TimeSpan? GetNextPendingExpiry(List<RunWorkItem> pending)
    {
        if (pending.Count == 0)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var deadline = pending.Min(item =>
        {
            var addSecondsResult = item.QueuedAt.AddSeconds(_queue.Options.QueueWaitTimeoutSeconds);
            return addSecondsResult;
        });
        TimeSpan? evaluatedValue23 = deadline <= now ? TimeSpan.FromMilliseconds(1) : deadline - now;
        return evaluatedValue23;
    }

    private async Task CompletePendingRunAsync(RunWorkItem item, FlowRunStatusDto terminalStatus, string code, string message, string cancellationSource, CancellationToken cancellationToken)
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
                {
                    run.Cancel(cancellationSource, DateTimeOffset.UtcNow);
                }
                else
                {
                    run.Complete(FlowRunStatus.TimedOut, DateTimeOffset.UtcNow, message);
                }

                await runStore.SaveAsync(run, CancellationToken.None);
                var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, terminalStatus, code, message);
                await PublishTerminalAsync(run, workerRunResultDto, eventStore, CancellationToken.None);
            }
        }
        finally
        {
            _queue.RemoveQueued(item.RunId);
        }
    }

    private async Task ExecuteItemAsync(RunWorkItem item, RunExecutionQueue.RunExecutionLease lease, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var outputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
            var getCancellationTokenResult = _queue.GetCancellationToken(item.RunId);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, getCancellationTokenResult);
            var runToken = cancellation.Token;
            var run = await runStore.FindAsync(item.RunId, runToken);
            if (run is null || run.IsTerminal)
            {
                return;
            }

            if (runToken.IsCancellationRequested)
            {
                var cancellationSource = ResolveCancellationSource(item.RunId, stoppingToken);
                run.Cancel(cancellationSource, DateTimeOffset.UtcNow);
                await runStore.SaveAsync(run, CancellationToken.None);
                var workerRunResultDto24 = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, RunErrorCodes.Cancelled, "The queued run was cancelled. 排队中的运行实例已取消。");
                await PublishTerminalAsync(run, workerRunResultDto24, eventStore, CancellationToken.None);
                return;
            }

            _logger.LogInformation("Starting flow run {RunId} for project {ProjectId}, flow {FlowId}. 开始运行流程实例。", item.RunId, item.ProjectId, item.Definition.Id);
            var startedAt = DateTimeOffset.UtcNow;
            var addSecondsResult25 = startedAt.AddSeconds(item.TimeoutSeconds);
            run.MarkRunning(startedAt, addSecondsResult25);
            await runStore.SaveAsync(run, runToken);
            var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
            var serializeResult = JsonSerializer.Serialize(item.Definition, createWebOptionsResult);
            var toStringResult = item.ProjectId.ToString("D");
            var getLibraryIdsResult = ProjectLibraryService.GetLibraryIds(item.Definition);
            var request = new WorkerRunRequestDto(WorkerProtocol.Version, item.RunId, item.Definition.Id, item.Definition.Version, serializeResult, run.Deadline!.Value, toStringResult, item.ProjectInputs, item.MaxSteps, _scriptRoot, _libraryRoot, item.MaxNodeVisits, getLibraryIdsResult, WorkpieceRootPath: _workpieceRoot);
            var delegateWorkerRunEventSink = new DelegateWorkerRunEventSink((workerEvent, token) =>
            {
                var handleEventAsyncResult = HandleEventAsync(workerEvent, runStore, eventStore, outputStore, token);
                return handleEventAsyncResult;
            });
            var result = await _workerClient.RunAsync(request, delegateWorkerRunEventSink, runToken);
            if (result.Status == FlowRunStatusDto.Cancelled)
            {
                var resolveCancellationSourceResult = ResolveCancellationSource(item.RunId, stoppingToken);
                run.Cancel(resolveCancellationSourceResult, DateTimeOffset.UtcNow);
            }
            else
            {
                var toDomainStatusResult = ToDomainStatus(result.Status);
                run.Complete(toDomainStatusResult, DateTimeOffset.UtcNow, result.ErrorMessage);
            }

            await PublishTerminalAsync(run, result, eventStore, CancellationToken.None);
            await runStore.SaveAsync(run, CancellationToken.None);
            _logger.LogInformation("Flow run {RunId} finished with status {Status}. 流程实例已结束。", item.RunId, run.Status);
        }
        catch (OperationCanceledException)
        {
            var resolveCancellationSourceResult26 = ResolveCancellationSource(item.RunId, stoppingToken);
            await MarkCancelledAfterStartAsync(item.RunId, resolveCancellationSourceResult26);
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

    private string ResolveCancellationSource(Guid runId, CancellationToken stoppingToken)
    {
        var evaluatedValue27 = _queue.GetCancellationSource(runId) ?? (stoppingToken.IsCancellationRequested ? RunCancellationSources.HostShutdown : RunCancellationSources.Worker);
        return evaluatedValue27;
    }

    private async Task MarkCancelledAfterStartAsync(Guid runId, string cancellationSource)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var run = await runStore.FindAsync(runId, CancellationToken.None);
        if (run is null || run.IsTerminal)
        {
            return;
        }

        run.Cancel(cancellationSource, DateTimeOffset.UtcNow);
        await runStore.SaveAsync(run, CancellationToken.None);
        var workerRunResultDto28 = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, WorkerErrorCodes.Cancelled, "The worker run was cancelled. Worker 运行已取消。");
        await PublishTerminalAsync(run, workerRunResultDto28, eventStore, CancellationToken.None);
    }

    private async Task MarkFailedAfterStartAsync(Guid runId, Exception exception)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var run = await runStore.FindAsync(runId, CancellationToken.None);
        if (run is null || run.IsTerminal)
        {
            return;
        }

        var errorMessage = $"Worker run failed. Worker 运行失败。 {exception.Message}";
        run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, errorMessage);
        await runStore.SaveAsync(run, CancellationToken.None);
        var workerRunResultDto29 = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Failed, WorkerErrorCodes.RunFailed, errorMessage);
        await PublishTerminalAsync(run, workerRunResultDto29, eventStore, CancellationToken.None);
    }

    private async ValueTask HandleEventAsync(WorkerEventEnvelopeDto workerEvent, IFlowRunStore runStore, IFlowRunEventStore eventStore, IFlowRunOutputStore outputStore, CancellationToken cancellationToken)
    {
        if (workerEvent.ProtocolVersion != WorkerProtocol.Version || workerEvent.RunId == Guid.Empty || await runStore.FindAsync(workerEvent.RunId, cancellationToken)is null)
        {
            return;
        }

        // WorkerSupervisor already enforces monotonic sequences. Keep the
        // store boundary defensive so a custom client cannot duplicate events.
        // WorkerSupervisor 已校验序列；在持久化边界再次防御，避免自定义客户端写入重复事件。
        var lastSequence = await eventStore.GetLastSequenceAsync(workerEvent.RunId, cancellationToken);
        if (workerEvent.Sequence <= lastSequence)
        {
            return;
        }

        var type = workerEvent.EventType switch
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
        var item = new FlowRunEvent(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, type, workerEvent.NodeId, workerEvent.PayloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var output = CreateNodeOutput(workerEvent);
        if (output is not null)
        {
            await outputStore.AppendAsync([output], cancellationToken);
        }

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
            var groupResult = _hub.Clients.Group($"run:{item.RunId:D}");
            await groupResult.SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "SignalR run event push failed for {RunId}. SignalR 运行事件推送失败。", item.RunId);
        }
    }

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

        try
        {
            using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Node output payload must be a JSON object. 节点输出载荷必须是 JSON 对象。");
            }

            var root = payload.RootElement;
            var inputsJson = root.TryGetProperty("inputs", out var inputs) ? inputs.GetRawText() : "{}";
            var outputsJson = root.TryGetProperty("outputs", out var outputs) ? outputs.GetRawText() : "{}";
            var readPayloadStringResult = ReadPayloadString(root, "branch");
            var readPayloadStringResult30 = ReadPayloadString(root, "errorCode");
            var readPayloadStringResult31 = ReadPayloadString(root, "errorMessage");
            var flowRunOutput = new FlowRunOutput(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, workerEvent.NodeId, outcome, readPayloadStringResult, outputsJson, readPayloadStringResult30, readPayloadStringResult31, inputsJson);
            return flowRunOutput;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Worker node output payload is invalid. Worker 节点输出载荷无效。", exception);
        }
    }

    private static string? ReadPayloadString(JsonElement payload, string propertyName)
    {
        string? evaluatedValue32 = payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return evaluatedValue32;
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
        var sequence = await eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1;
        var createWebOptionsResult34 = SereinJsonSerialization.CreateWebOptions();
        var serializeResult35 = JsonSerializer.Serialize(new { result.Status, result.ErrorCode, result.ErrorMessage, cancellationSource = run.CancellationReason, variables = result.FinalVariables }, createWebOptionsResult34);
        var item = new FlowRunEvent(run.Id, sequence, DateTimeOffset.UtcNow, type, null, serializeResult35);
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
            var groupResult33 = _hub.Clients.Group($"run:{run.Id:D}");
            await groupResult33.SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Terminal SignalR event push failed for {RunId}. 终止 SignalR 事件推送失败。", run.Id);
        }
    }

    private static FlowRunStatus ToDomainStatus(FlowRunStatusDto status) => status switch
    {
        FlowRunStatusDto.Succeeded => FlowRunStatus.Succeeded,
        FlowRunStatusDto.Cancelled => FlowRunStatus.Cancelled,
        FlowRunStatusDto.TimedOut => FlowRunStatus.TimedOut,
        _ => FlowRunStatus.Failed
    };
    private static Action<ILogger, Guid, Exception?> createWorkerRunFailedLogInitialValue()
    {
        var eventId = new EventId(4101, "WorkerRunFailed");
        Action<ILogger, Guid, Exception?> workerRunFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Worker run {RunId} failed before completion. Worker 运行在完成前失败。");
        return workerRunFailedLogInitialValue;
    }

    private static Action<ILogger, Guid, Exception?> createReconciliationFailedLogInitialValue()
    {
        var eventId21 = new EventId(4103, "InterruptedRunReconciliationFailed");
        Action<ILogger, Guid, Exception?> reconciliationFailedLogInitialValue = LoggerMessage.Define<Guid>(LogLevel.Error, eventId21, "Interrupted run reconciliation failed for {RunId}. 中断运行实例对账失败。");
        return reconciliationFailedLogInitialValue;
    }
}
