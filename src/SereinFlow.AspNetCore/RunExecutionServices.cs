using System;
using System.Threading;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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

/// <summary>
/// Represents run work item data used by SereinFlow.
/// 表示 SereinFlow 使用的运行WorkItem数据。
/// </summary>
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
        return new()
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
    }

    public RunExecutionSettingsDto ToDto()
    {
        return new(QueueCapacity, MaxConcurrentRuns, MaxConcurrentListenerRuns, MaxConcurrentRunsPerProject, QueueWaitTimeoutSeconds, ShutdownGracePeriodSeconds, SynchronousInvocationTimeoutSeconds, MaxLibraryUploadBytes);
    }

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
            MaxLibraryUploadBytes = settings.MaxLibraryUploadBytes,
        }.Normalize();
    }
}

/// <summary>
/// Represents run execution snapshot data used by SereinFlow.
/// 表示 SereinFlow 使用的运行执行快照数据。
/// </summary>
public sealed record RunExecutionSnapshot(int QueueCapacity, int QueuedCount, int ActiveRunCount, int ActiveListenerRunCount, int MaxConcurrentRuns, int MaxConcurrentListenerRuns, int MaxConcurrentRunsPerProject);
/// <summary>
/// Captures the outcome of run submission operations.
/// 记录运行Submission操作的结果。
/// </summary>
public sealed record RunSubmissionResult(FlowRun? Run, int StatusCode, string? ErrorTitle = null, object? ErrorBody = null, long? CurrentVersion = null)
{
    [MemberNotNullWhen(true, nameof(Run))]
    public bool IsAccepted => Run is not null;
}

/// <summary>
/// Validates and submits flow run requests to the execution queue.
/// 校验流程运行请求并提交到执行队列。
/// </summary>
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

    /// <summary>
    /// Submits a run request for a saved flow.
    /// 提交已保存流程的运行请求。
    /// </summary>
    public async Task<RunSubmissionResult> SubmitAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        return await SubmitCoreAsync(projectId, flowId, request, definitionOverride: null, cancellationToken);
    }

    /// <summary>
    /// Submits a run request using the supplied flow definition.
    /// 使用给定的流程定义提交运行请求。
    /// </summary>
    public async Task<RunSubmissionResult> SubmitDefinitionAsync(Guid projectId, FlowDefinitionDto definition, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return await SubmitCoreAsync(projectId, definition.Id, request, definition, cancellationToken);
    }

    /// <summary>
    /// Submits a run request for the published production version.
    /// 提交已发布生产版本的运行请求。
    /// </summary>
    public async Task<RunSubmissionResult> SubmitProductionAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        return await SubmitCoreAsync(projectId, flowId, request, definitionOverride: null, cancellationToken: cancellationToken, definitionTrack: FlowVersionTrackDto.Production);
    }

    private async Task<RunSubmissionResult> SubmitCoreAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, FlowDefinitionDto? definitionOverride, CancellationToken cancellationToken, FlowVersionTrackDto definitionTrack = FlowVersionTrackDto.Development)
    {
        using var reservation = _queue.TryReserve();
        if (reservation is null)
        {
            return new RunSubmissionResult(null, StatusCodes.Status429TooManyRequests, "The run queue is full. 运行队列已满。", new { code = RunErrorCodes.QueueFull });
        }

        var preparation = await _runService.PrepareAsync(projectId, flowId, request, definitionOverride: definitionOverride, cancellationToken: cancellationToken, definitionTrack: definitionTrack);
        if (!preparation.IsSuccess)
        {
            return new RunSubmissionResult(null, preparation.StatusCode, preparation.ErrorTitle, preparation.ErrorBody, preparation.CurrentVersion);
        }

        var prepared = preparation.Preparation!;
        var item = new RunWorkItem(prepared.Run.Id, projectId, prepared.Definition, prepared.ProjectInputs, prepared.Run.QueuedAt, prepared.TimeoutSeconds, prepared.MaxSteps, prepared.MaxNodeVisits);
        if (_queue.TryEnqueueReserved(item, reservation))
        {
            return new RunSubmissionResult(prepared.Run, StatusCodes.Status202Accepted);
        }

        prepared.Run.Cancel(RunErrorCodes.SchedulerUnavailable, DateTimeOffset.UtcNow);
        await _runStore.SaveAsync(prepared.Run, CancellationToken.None);
        return new RunSubmissionResult(null, StatusCodes.Status503ServiceUnavailable, "The run scheduler is unavailable. 运行调度器不可用。", new { code = RunErrorCodes.SchedulerUnavailable });
    }
}

/// <summary>
/// Reserves execution capacity and coordinates queued and active runs.
/// 预留执行容量并协调排队中和活动中的运行。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The public type models a run scheduling queue, including reservations and execution leases; retain its domain name and API identity.")]
public sealed class RunExecutionQueue : IDisposable
{
    private readonly Channel<RunWorkItem> _queue;
    private readonly Dictionary<Guid, RunCancellation> _cancellations = [];
    private readonly Dictionary<Guid, string> _cancellationSources = [];
    private readonly ConcurrentDictionary<Guid, ActiveRunInfo> _activeRuns = new();
    // A single pending wake-up is enough: scheduling always re-evaluates all
    // work and limits. This prevents signal counts accumulating under load.
    // 一个待处理唤醒信号已经足够：调度会重新检查所有任务和限制，避免高负载下信号计数累积。
    private readonly SemaphoreSlim _schedulerSignal = new(0, 1);
    private readonly CancellationTokenSource _schedulerShutdown = new();
    private readonly object _gate = new();
    private RunExecutionOptions _options;
    private int _queuedCount;
    private bool _accepting = true;
    private bool _disposed;
    private bool _shutdownSignalled;
    private bool _schedulerResourcesDisposed;
    private int _schedulerWaiters;
    private readonly IFileUploadSettings _fileUploadSettings;
    public RunExecutionQueue(IOptions<RunExecutionOptions> options, IFileUploadSettings? fileUploadSettings = null)
    {
        var runExecutionOptions = (options?.Value ?? new RunExecutionOptions());
        _options = runExecutionOptions.Normalize();
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

    /// <summary>
    /// Applies run queue settings and returns the effective settings.
    /// 应用运行队列设置并返回生效配置。
    /// </summary>
    public RunExecutionSettingsDto Configure(RunExecutionSettingsDto settings)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _options = RunExecutionOptions.FromDto(settings);
            _fileUploadSettings.Configure(_options.MaxLibraryUploadBytes);
        }

        SignalScheduler();
        return Options.ToDto();
    }

    /// <summary>
    /// Attempts to reserve a queue slot for a new run.
    /// 尝试为新运行预留队列位置。
    /// </summary>
    public RunQueueReservation? TryReserve()
    {
        lock (_gate)
        {
            if (!_accepting || _queuedCount >= _options.QueueCapacity)
            {
                return null;
            }

            Interlocked.Increment(ref _queuedCount);
            return new RunQueueReservation(this);
        }
    }

    /// <summary>
    /// Adds a run to the queue using a previously acquired reservation.
    /// 使用已取得的预留将运行加入队列。
    /// </summary>
    public bool TryEnqueueReserved(RunWorkItem item, RunQueueReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!reservation.TryClaim(this))
        {
            throw new InvalidOperationException("The queue reservation is not valid. 队列预留无效。");
        }

        var source = new RunCancellation();
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
        lock (_gate)
        {
            if (!_disposed && _queue.Reader.TryRead(out var next))
            {
                item = next;
                return true;
            }

            item = default!;
            return false;
        }
    }

    public CancellationToken GetCancellationToken(Guid runId)
    {
        lock (_gate)
        {
            return _cancellations.TryGetValue(runId, out var source) ? source.Token : new CancellationToken(canceled: true);
        }
    }

    /// <summary>
    /// Requests cancellation of a queued or active run.
    /// 请求取消排队中或活动中的运行。
    /// </summary>
    public bool Cancel(Guid runId)
    {
        return Cancel(runId, RunCancellationSources.Api);
    }

    public bool Cancel(Guid runId, string source)
    {
        RunCancellation? cancellation = null;
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

        var cancelled = cancellation!.Cancel();
        SignalScheduler();
        return cancelled;
    }

    public string? GetCancellationSource(Guid runId)
    {
        lock (_gate)
        {
            return _cancellationSources.TryGetValue(runId, out var source) ? source : null;
        }
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

    /// <summary>
    /// Attempts to acquire global and per-project execution capacity.
    /// 尝试取得全局及项目级执行容量。
    /// </summary>
    public bool TryAcquireExecutionSlots(RunWorkItem item, out RunExecutionLease? lease)
    {
        lease = null;
        lock (_gate)
        {
            if (_disposed || _activeRuns.Count >= _options.MaxConcurrentRuns)
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

            if (!TryRegisterListener())
            {
                return false;
            }

            lease = new RunExecutionLease(this, item.RunId);
            return true;
            bool TryRegisterListener()
            {
                var activeRunInfo = new ActiveRunInfo(item.ProjectId, listenerRun);
                return _activeRuns.TryAdd(item.RunId, activeRunInfo);
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
        RunCancellation? source = null;
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

            if (!TryRegisterFlowCallListener())
            {
                return false;
            }

            source = new RunCancellation();
            _cancellations[item.RunId] = source;
            lease = new RunExecutionLease(this, item.RunId);
            bool TryRegisterFlowCallListener()
            {
                var activeRunInfo = new ActiveRunInfo(item.ProjectId, listenerRun);
                return _activeRuns.TryAdd(item.RunId, activeRunInfo);
            }
        }

        SignalScheduler();
        return true;
    }

    public RunExecutionSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var queuedCount = Math.Max(0, Volatile.Read(ref _queuedCount));
            var activeListenerRunCount = _activeRuns.Values.Count(static item => item.IsListenerRun);
            return new(_options.QueueCapacity, queuedCount, _activeRuns.Count, activeListenerRunCount, _options.MaxConcurrentRuns, _options.MaxConcurrentListenerRuns, _options.MaxConcurrentRunsPerProject);
        }
    }

    /// <summary>
    /// Waits for queued work or a scheduler state change.
    /// 等待队列任务或调度状态发生变化。
    /// </summary>
    public async Task<bool> WaitForSchedulerSignalAsync(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        CancellationTokenSource lifetime;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _schedulerShutdown.Token);
            _schedulerWaiters++;
        }

        try
        {
            if (timeout is { } value && value > TimeSpan.Zero)
            {
                return await _schedulerSignal.WaitAsync(value, lifetime.Token);
            }

            await _schedulerSignal.WaitAsync(lifetime.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        finally
        {
            lifetime.Dispose();
            lock (_gate)
            {
                _schedulerWaiters--;
                TryDisposeSchedulerResources();
            }
        }
    }

    /// <summary>
    /// Stops new run submissions and cancels tracked runs.
    /// 停止接收新运行并取消已跟踪的运行。
    /// </summary>
    public void StopAcceptingAndCancelAll()
    {
        RunCancellation[] sources;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _accepting = false;
            _queue.Writer.TryComplete();
            sources = _cancellations
                .Where(static pair => !pair.Value.IsCancellationRequested)
                .Select(pair =>
                {
                    _cancellationSources.TryAdd(pair.Key, RunCancellationSources.HostShutdown);
                    return pair.Value;
                })
                .ToArray();
        }

        foreach (var source in sources)
        {
            source.Cancel();
        }

        SignalScheduler();
    }

    /// <summary>
    /// Cancels outstanding work and releases owned resources after in-flight waits finish.
    /// 取消未完成任务，并在正在进行的等待结束后释放拥有的资源。
    /// </summary>
    public void Dispose()
    {
        RunCancellation[] sources;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _accepting = false;
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out _))
            {
            }

            Interlocked.Exchange(ref _queuedCount, 0);
            foreach (var pair in _cancellations)
            {
                if (!pair.Value.IsCancellationRequested)
                {
                    _cancellationSources.TryAdd(pair.Key, RunCancellationSources.HostShutdown);
                }
            }

            sources = _cancellations.Values.ToArray();
            _cancellations.Clear();
        }

        List<Exception>? failures = null;
        try
        {
            _schedulerShutdown.Cancel();
            foreach (var source in sources)
            {
                try
                {
                    source.Cancel();
                }
                catch (Exception exception)
                {
                    failures ??= [];
                    failures.Add(exception);
                }
                finally
                {
                    source.Dispose();
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _shutdownSignalled = true;
                TryDisposeSchedulerResources();
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("Run cancellation callbacks failed during queue disposal. 队列释放期间运行取消回调失败。", failures);
        }
    }

    // Call only under _gate: SemaphoreSlim.Dispose must not overlap WaitAsync or Release.
    // 仅在 _gate 内调用：SemaphoreSlim.Dispose 不能与 WaitAsync 或 Release 并发。
    private void TryDisposeSchedulerResources()
    {
        if (_shutdownSignalled && _schedulerWaiters == 0 && !_schedulerResourcesDisposed)
        {
            _schedulerResourcesDisposed = true;
            _schedulerSignal.Dispose();
            _schedulerShutdown.Dispose();
        }
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
        RunCancellation? source = null;
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
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

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
            .Any(node =>
            {
                return node.Type == NodeTypeDto.Flipflop && !incoming.Contains(node.Id);
            });
    }

    private static string NormalizeCancellationSource(string source)
    {
        return string.IsNullOrWhiteSpace(source) ? RunCancellationSources.Worker : source.Trim();
    }

    private sealed record ActiveRunInfo(Guid ProjectId, bool IsListenerRun);

    private sealed class RunCancellation : IDisposable
    {
        private readonly CancellationTokenSource _source = new();
        private readonly object _gate = new();
        private bool _disposed;
        private int _cancelling;

        internal CancellationToken Token
        {
            get
            {
                lock (_gate)
                {
                    return _disposed ? new CancellationToken(canceled: true) : _source.Token;
                }
            }
        }

        internal bool IsCancellationRequested
        {
            get
            {
                lock (_gate)
                {
                    return _disposed || _source.IsCancellationRequested;
                }
            }
        }

        internal bool Cancel()
        {
            lock (_gate)
            {
                if (_disposed || _source.IsCancellationRequested)
                {
                    return false;
                }

                _cancelling++;
            }

            try
            {
                _source.Cancel();
                return true;
            }
            finally
            {
                lock (_gate)
                {
                    _cancelling--;
                    if (_disposed && _cancelling == 0)
                    {
                        _source.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_cancelling == 0)
                {
                    _source.Dispose();
                }
            }
        }
    }

    public sealed class RunQueueReservation : IDisposable
    {
        private RunExecutionQueue? _owner;
        internal RunQueueReservation(RunExecutionQueue owner)
        {
            _owner = owner;
        }

        internal bool TryClaim(RunExecutionQueue owner)
        {
            return ReferenceEquals(Interlocked.Exchange(ref _owner, null), owner);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.ReleaseQueueSlot();
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
            Interlocked.Exchange(ref _owner, null)?.ReleaseExecutionSlots(_runId);
        }
    }
}

public sealed class RunEventBroadcaster
{
    public const int SubscriberCapacity = 256;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>> _channels = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _completedRuns = new();
    private static readonly TimeSpan CompletedRunRetention = TimeSpan.FromMinutes(10);
    public ChannelReader<FlowRunEventDto> Subscribe(Guid runId, CancellationToken cancellationToken = default)
    {
        CleanupCompletedRuns();
        var subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateBounded<FlowRunEventDto>(new BoundedChannelOptions(SubscriberCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        if (_completedRuns.ContainsKey(runId))
        {
            channel.Writer.TryComplete();
            return channel.Reader;
        }

        var subscribers = _channels.GetOrAdd(runId, static _ =>
        {
            return new ConcurrentDictionary<Guid, Channel<FlowRunEventDto>>();
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
            foreach (var subscriber in subscribers)
            {
                if (!subscriber.Value.Writer.TryWrite(item))
                {
                    // End the slow stream at its last delivered sequence. SSE
                    // reconnects using Last-Event-ID and replays durable events.
                    // 在最后成功投递的序号处结束慢速流；SSE 通过 Last-Event-ID 重连并回放持久化事件。
                    RemoveSubscription(item.RunId, subscriber.Key);
                }
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
        return Groups.AddToGroupAsync(Context.ConnectionId, $"run:{runId:D}");
    }

    public Task Unsubscribe(Guid runId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run:{runId:D}");
    }
}

/// <summary>
/// Runs queued flows in the hosted application.
/// 在宿主应用中执行队列里的流程。
/// </summary>
public sealed class RunExecutionHostedService : BackgroundService
{
    private static readonly Action<ILogger, Guid, Exception?> WorkerRunFailedLog = CreateWorkerRunFailedLogInitialValue();
    private static readonly Action<ILogger, Guid, Exception?> ReconciliationFailedLog = CreateReconciliationFailedLogInitialValue();
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

    /// <summary>
    /// Processes queued runs and supervises their execution lifecycle.
    /// 处理队列中的运行并监督其执行生命周期。
    /// </summary>
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
                        return ExecuteItemAsync(item, lease!, stoppingToken);
                    }, CancellationToken.None);
                    _activeTasks[item.RunId] = executionTask;
                    _ = executionTask.ContinueWith(_ =>
                    {
                        return _activeTasks.TryRemove(item.RunId, out Task? _);
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
                await Task.WhenAny(Task.WhenAll(active), Task.Delay(TimeSpan.FromSeconds(_queue.Options.ShutdownGracePeriodSeconds), CancellationToken.None));
            }
        }
    }

    private async Task ApplyPersistedEnvironmentSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runEnvironmentSettingsStore = scope.ServiceProvider.GetRequiredService<IRunEnvironmentSettingsStore>();
        var settings = await runEnvironmentSettingsStore.GetAsync(cancellationToken);
        if (settings is not null)
        {
            _queue.Configure(settings);
        }
    }

    private async Task RecoverPendingRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        foreach (var pending in await flowRunStore.ListPendingAsync(cancellationToken))
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
                HostingLog.RecoveredRunRejected(_logger, item.RunId, null);
                break;
            }
        }
    }

    private async Task ReconcileInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var runInterruptionService = scope.ServiceProvider.GetRequiredService<RunInterruptionService>();
        var flowRunQuery = new FlowRunQuery([FlowRunStatus.Running], Take: 10_000);
        var running = await flowRunStore.ListAsync(flowRunQuery, cancellationToken);
        var reconciled = 0;
        foreach (var run in running)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await runInterruptionService.InterruptAsync(run.Id, "engine_restart", RunErrorCodes.WorkerLost, "The worker was lost before the run reached a terminal state. Worker 在流程到达终态前已丢失。", cancellationToken);
                if (result.IsInterrupted)
                {
                    reconciled++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            HostingLog.OrphanedRunsReconciled(_logger, reconciled, null);
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
                var cancellationSource = _queue.GetCancellationSource(item.RunId) ?? RunCancellationSources.Worker;
                await CompletePendingRunAsync(item, FlowRunStatusDto.Cancelled, RunErrorCodes.Cancelled, "The queued run was cancelled. 排队中的运行实例已取消。", cancellationSource, cancellationToken);
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
            return item.QueuedAt.AddSeconds(_queue.Options.QueueWaitTimeoutSeconds);
        });
        return deadline <= now ? TimeSpan.FromMilliseconds(1) : deadline - now;
    }

    private async Task CompletePendingRunAsync(RunWorkItem item, FlowRunStatusDto terminalStatus, string code, string message, string cancellationSource, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var run = await flowRunStore.FindAsync(item.RunId, cancellationToken);
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

                await flowRunStore.SaveAsync(run, CancellationToken.None);
                var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, terminalStatus, code, message);
                await PublishTerminalAsync(run, workerRunResultDto, flowRunEventStore, CancellationToken.None);
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
            var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var flowRunOutputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _queue.GetCancellationToken(item.RunId));
            var runToken = cancellation.Token;
            var run = await flowRunStore.FindAsync(item.RunId, runToken);
            if (run is null || run.IsTerminal)
            {
                return;
            }

            if (runToken.IsCancellationRequested)
            {
                var cancellationSource = ResolveCancellationSource(item.RunId, stoppingToken);
                run.Cancel(cancellationSource, DateTimeOffset.UtcNow);
                await flowRunStore.SaveAsync(run, CancellationToken.None);
                var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, RunErrorCodes.Cancelled, "The queued run was cancelled. 排队中的运行实例已取消。");
                await PublishTerminalAsync(run, workerRunResultDto, flowRunEventStore, CancellationToken.None);
                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                HostingLog.RunStarting(_logger, item.RunId, item.ProjectId, item.Definition.Id, null);
            }
            var startedAt = DateTimeOffset.UtcNow;
            var addSeconds = startedAt.AddSeconds(item.TimeoutSeconds);
            run.MarkRunning(startedAt, addSeconds);
            await flowRunStore.SaveAsync(run, runToken);
            var webOptions = SereinJsonSerialization.CreateWebOptions();
            var definitionJson = JsonSerializer.Serialize(item.Definition, webOptions);
            var projectId = item.ProjectId.ToString("D");
            var allowedLibraryIds = ProjectLibraryService.GetLibraryIds(item.Definition);
            var request = new WorkerRunRequestDto(WorkerProtocol.Version, item.RunId, item.Definition.Id, item.Definition.Version, definitionJson, run.Deadline!.Value, projectId, item.ProjectInputs, item.MaxSteps, _scriptRoot, _libraryRoot, item.MaxNodeVisits, allowedLibraryIds, WorkpieceRootPath: _workpieceRoot);
            var delegateWorkerRunEventSink = new DelegateWorkerRunBatchEventSink(HandleEventBatchAsync);
            var result = await _workerClient.RunAsync(request, delegateWorkerRunEventSink, runToken);
            if (result.Status == FlowRunStatusDto.Cancelled)
            {
                var mappedCancellationSource = ResolveCancellationSource(item.RunId, stoppingToken);
                run.Cancel(mappedCancellationSource, DateTimeOffset.UtcNow);
            }
            else
            {
                var domainStatus = ToDomainStatus(result.Status);
                run.Complete(domainStatus, DateTimeOffset.UtcNow, result.ErrorMessage);
            }

            await PublishTerminalAsync(run, result, flowRunEventStore, CancellationToken.None);
            await flowRunStore.SaveAsync(run, CancellationToken.None);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                HostingLog.RunFinished(_logger, item.RunId, run.Status, null);
            }
        }
        catch (OperationCanceledException)
        {
            var resolvedCancellationSource = ResolveCancellationSource(item.RunId, stoppingToken);
            await MarkCancelledAfterStartAsync(item.RunId, resolvedCancellationSource);
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
        return _queue.GetCancellationSource(runId) ?? (stoppingToken.IsCancellationRequested ? RunCancellationSources.HostShutdown : RunCancellationSources.Worker);
    }

    private async Task MarkCancelledAfterStartAsync(Guid runId, string cancellationSource)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var run = await flowRunStore.FindAsync(runId, CancellationToken.None);
        if (run is null || run.IsTerminal)
        {
            return;
        }

        run.Cancel(cancellationSource, DateTimeOffset.UtcNow);
        await flowRunStore.SaveAsync(run, CancellationToken.None);
        var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, WorkerErrorCodes.Cancelled, "The worker run was cancelled. Worker 运行已取消。");
        await PublishTerminalAsync(run, workerRunResultDto, flowRunEventStore, CancellationToken.None);
    }

    private async Task MarkFailedAfterStartAsync(Guid runId, Exception exception)
    {
        using var scope = _scopeFactory.CreateScope();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var run = await flowRunStore.FindAsync(runId, CancellationToken.None);
        if (run is null || run.IsTerminal)
        {
            return;
        }

        var errorMessage = $"Worker run failed. Worker 运行失败。 {exception.Message}";
        run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, errorMessage);
        await flowRunStore.SaveAsync(run, CancellationToken.None);
        var workerRunResultDto = new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Failed, WorkerErrorCodes.RunFailed, errorMessage);
        await PublishTerminalAsync(run, workerRunResultDto, flowRunEventStore, CancellationToken.None);
    }

    private async ValueTask HandleEventBatchAsync(IReadOnlyList<WorkerEventEnvelopeDto> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        // Each batch owns its scope, including when a timed-out sink is still unwinding.
        // 每个批次独立持有服务作用域；即使接收端超时后仍在退出，该作用域也归该批次所有。
        using var scope = _scopeFactory.CreateScope();
        var flowRunStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowRunEventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
        var flowRunOutputStore = scope.ServiceProvider.GetRequiredService<IFlowRunOutputStore>();
        var runId = events[0].RunId;
        if (runId == Guid.Empty || await flowRunStore.FindAsync(runId, cancellationToken) is null)
        {
            return;
        }

        // WorkerSupervisor already enforces monotonic sequences. Keep the
        // store boundary defensive so a custom client cannot duplicate events.
        // WorkerSupervisor 已校验序列；在持久化边界再次防御，避免自定义客户端写入重复事件。
        var lastSequence = await flowRunEventStore.GetLastSequenceAsync(runId, cancellationToken);
        var items = new List<FlowRunEvent>(events.Count);
        var outputs = new List<FlowRunOutput>();
        foreach (var workerEvent in events.OrderBy(static item => item.Sequence))
        {
            if (workerEvent.ProtocolVersion != WorkerProtocol.Version || workerEvent.RunId != runId || workerEvent.Sequence <= lastSequence)
            {
                continue;
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
            items.Add(item);
            lastSequence = workerEvent.Sequence;
            var output = CreateNodeOutput(workerEvent);
            if (output is not null)
            {
                outputs.Add(output);
            }
        }

        await flowRunEventStore.AppendAsync(items, cancellationToken);
        await flowRunOutputStore.AppendAsync(outputs, cancellationToken);
        foreach (var item in items)
        {
            await PublishLiveEventAsync(item, cancellationToken);
        }
    }

    private async Task PublishLiveEventAsync(FlowRunEvent item, CancellationToken cancellationToken)
    {
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
            HostingLog.LiveRunBroadcastFailed(_logger, item.RunId, exception);
        }

        try
        {
            var group = _hub.Clients.Group($"run:{item.RunId:D}");
            await group.SendAsync("runEvent", dto, cancellationToken);
        }
        catch (Exception exception)
        {
            HostingLog.RunPushFailed(_logger, item.RunId, exception);
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
            var branch = ReadPayloadString(root, "branch");
            var errorCode = ReadPayloadString(root, "errorCode");
            var errorMessage = ReadPayloadString(root, "errorMessage");
            return new FlowRunOutput(workerEvent.RunId, workerEvent.Sequence, workerEvent.Timestamp, workerEvent.NodeId, outcome, branch, outputsJson, errorCode, errorMessage, inputsJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Worker node output payload is invalid. Worker 节点输出载荷无效。", exception);
        }
    }

    private static string? ReadPayloadString(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
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
        var webOptions = SereinJsonSerialization.CreateWebOptions();
        var payloadJson = JsonSerializer.Serialize(new { result.Status, result.ErrorCode, result.ErrorMessage, cancellationSource = run.CancellationReason, variables = result.FinalVariables }, webOptions);
        var item = new FlowRunEvent(run.Id, sequence, DateTimeOffset.UtcNow, type, null, payloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var dto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        try
        {
            await _broadcaster.PublishAsync(dto, cancellationToken);
        }
        catch (Exception exception)
        {
            HostingLog.TerminalRunBroadcastFailed(_logger, run.Id, exception);
        }

        try
        {
            var group = _hub.Clients.Group($"run:{run.Id:D}");
            await group.SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            HostingLog.TerminalRunPushFailed(_logger, run.Id, exception);
        }
    }

    private static FlowRunStatus ToDomainStatus(FlowRunStatusDto status) => status switch
    {
        FlowRunStatusDto.Succeeded => FlowRunStatus.Succeeded,
        FlowRunStatusDto.Cancelled => FlowRunStatus.Cancelled,
        FlowRunStatusDto.TimedOut => FlowRunStatus.TimedOut,
        _ => FlowRunStatus.Failed
    };
    private static Action<ILogger, Guid, Exception?> CreateWorkerRunFailedLogInitialValue()
    {
        var eventId = new EventId(4101, "WorkerRunFailed");
        return LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Worker run {RunId} failed before completion. Worker 运行在完成前失败。");
    }

    private static Action<ILogger, Guid, Exception?> CreateReconciliationFailedLogInitialValue()
    {
        var eventId = new EventId(4103, "InterruptedRunReconciliationFailed");
        return LoggerMessage.Define<Guid>(LogLevel.Error, eventId, "Interrupted run reconciliation failed for {RunId}. 中断运行实例对账失败。");
    }
}
