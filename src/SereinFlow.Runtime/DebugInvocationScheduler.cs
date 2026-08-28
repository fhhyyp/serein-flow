namespace SereinFlow.Runtime;

/// <summary>
/// Serializes debug-only listener-trigger downstream work. The listener itself
/// never waits for this queue, so it can continue receiving trigger events.
/// The queue is deliberately bounded and rejects excess work explicitly.
/// 串行化仅调试模式下的监听触发下游工作。监听器本身不会等待该队列，因此能够继续接收
/// 触发事件。队列刻意有界，超额工作会被明确拒绝。
/// </summary>
public sealed class DebugInvocationScheduler
{
    private readonly object _sync = new();
    private readonly Queue<WorkItem> _waiting = [];
    private readonly int _maximumQueuedInvocations;
    private WorkItem? _active;
    private Task? _activeTask;
    private bool _stopped;

    public DebugInvocationScheduler(int maximumQueuedInvocations = 64)
    {
        _maximumQueuedInvocations = Math.Clamp(maximumQueuedInvocations, 0, 1_024);
    }

    public int MaximumQueuedInvocations => _maximumQueuedInvocations;

    public bool TrySchedule(
        Func<Task> executeAsync,
        Func<Task> discardAsync,
        out int queuePosition)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(discardAsync);

        WorkItem? start = null;
        lock (_sync)
        {
            if (_stopped || (_active is not null && _waiting.Count >= _maximumQueuedInvocations))
            {
                queuePosition = -1;
                return false;
            }

            var work = new WorkItem(executeAsync, discardAsync);
            if (_active is null)
            {
                _active = work;
                start = work;
                queuePosition = 0;
            }
            else
            {
                _waiting.Enqueue(work);
                queuePosition = _waiting.Count;
            }
        }

        if (start is not null)
            Start(start);
        return true;
    }

    public async Task StopAndDrainAsync()
    {
        WorkItem[] discarded;
        Task? activeTask;
        lock (_sync)
        {
            if (_stopped)
            {
                activeTask = _activeTask;
                discarded = [];
            }
            else
            {
                _stopped = true;
                discarded = _waiting.ToArray();
                _waiting.Clear();
                activeTask = _activeTask;
            }
        }

        foreach (var work in discarded)
            await DiscardSafelyAsync(work).ConfigureAwait(false);
        if (activeTask is not null)
            await activeTask.ConfigureAwait(false);
    }

    private void Start(WorkItem work)
    {
        var task = ExecuteAsync(work);
        lock (_sync)
            _activeTask = task;
    }

    private async Task ExecuteAsync(WorkItem work)
    {
        try
        {
            await work.ExecuteAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The owning FlowRunner records shared cancellation; this class
            // owns only scheduling and disposal of queued work.
            // FlowRunner 负责记录共享取消；此类仅拥有排队与待处理工作释放职责。
        }
        catch
        {
            // Individual trigger work publishes its own diagnostics. One
            // failure must not stop reception of subsequent triggers.
            // 单次触发工作自行发布诊断；一个失败不能停止后续触发的接收。
        }
        finally
        {
            WorkItem? next = null;
            lock (_sync)
            {
                _active = null;
                if (!_stopped && _waiting.TryDequeue(out var queued))
                {
                    _active = queued;
                    next = queued;
                }
            }

            if (next is not null)
                Start(next);
        }
    }

    private static async Task DiscardSafelyAsync(WorkItem work)
    {
        try
        {
            await work.DiscardAsync().ConfigureAwait(false);
        }
        catch
        {
            // A child session release failure must not keep the scheduler alive.
            // 子会话释放失败时不能让调度器继续滞留。
        }
    }

    private sealed record WorkItem(Func<Task> ExecuteAsync, Func<Task> DiscardAsync);
}
