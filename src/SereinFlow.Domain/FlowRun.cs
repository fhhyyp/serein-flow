namespace SereinFlow.Domain;

public sealed class FlowRun
{
    private FlowRun(
        Guid id,
        Guid projectId,
        Guid flowId,
        long flowVersion,
        DateTimeOffset createdAt,
        FlowConcurrencyMode concurrencyMode,
        bool isListenerRun)
    {
        Id = id;
        ProjectId = projectId;
        FlowId = flowId;
        FlowVersion = flowVersion;
        CreatedAt = createdAt;
        QueuedAt = createdAt;
        ConcurrencyMode = concurrencyMode;
        IsListenerRun = isListenerRun;
        Status = FlowRunStatus.Pending;
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public Guid FlowId { get; }

    public long FlowVersion { get; }

    public FlowRunStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset QueuedAt { get; private set; }

    public FlowConcurrencyMode ConcurrencyMode { get; }

    public bool IsListenerRun { get; }

    public string? ExclusivityKey => ConcurrencyMode == FlowConcurrencyMode.ExclusiveReject
        ? FlowId.ToString("D")
        : null;

    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// The execution deadline is assigned when this run actually obtains a
    /// Worker slot. Pending time is governed by the queue wait timeout.
    /// 实际取得 Worker 槽位时才设置执行截止时间；排队时间由队列等待超时控制。
    /// </summary>
    public DateTimeOffset? Deadline { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? ErrorSummary { get; private set; }

    public bool IsTerminal => Status is FlowRunStatus.Succeeded or FlowRunStatus.Failed or FlowRunStatus.Cancelled or FlowRunStatus.TimedOut;

    public static FlowRun Start(Guid flowId, long flowVersion, DateTimeOffset createdAt, Guid? id = null)
        => Start(Guid.Empty, flowId, flowVersion, createdAt, FlowConcurrencyMode.Parallel, false, id);

    public static FlowRun Start(Guid projectId, Guid flowId, long flowVersion, DateTimeOffset createdAt, Guid? id = null)
        => Start(projectId, flowId, flowVersion, createdAt, FlowConcurrencyMode.Parallel, false, id);

    public static FlowRun Start(
        Guid projectId,
        Guid flowId,
        long flowVersion,
        DateTimeOffset createdAt,
        FlowConcurrencyMode concurrencyMode,
        bool isListenerRun,
        Guid? id = null)
    {
        if (flowId == Guid.Empty)
        {
            throw new ArgumentException("Flow ID cannot be empty. 流程 ID 不能为空。", nameof(flowId));
        }

        if (flowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(flowVersion), "Flow version must be positive. 流程版本必须为正数。");

        if (!Enum.IsDefined(concurrencyMode))
            throw new ArgumentOutOfRangeException(nameof(concurrencyMode), "The flow concurrency mode is invalid. 流程并发模式无效。");

        return new FlowRun(id ?? Guid.NewGuid(), projectId, flowId, flowVersion, createdAt, concurrencyMode, isListenerRun);
    }

    public static FlowRun Rehydrate(
        Guid id,
        Guid projectId,
        Guid flowId,
        long flowVersion,
        FlowRunStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        string? cancellationReason,
        string? errorSummary,
        FlowConcurrencyMode concurrencyMode = FlowConcurrencyMode.Parallel,
        bool isListenerRun = false,
        DateTimeOffset? queuedAt = null,
        DateTimeOffset? deadline = null)
    {
        var run = Start(projectId, flowId, flowVersion, createdAt, concurrencyMode, isListenerRun, id);
        run.Status = status;
        run.QueuedAt = queuedAt ?? createdAt;
        run.StartedAt = startedAt;
        run.Deadline = deadline;
        run.EndedAt = endedAt;
        run.CancellationReason = cancellationReason;
        run.ErrorSummary = errorSummary;
        return run;
    }

    public void MarkRunning(DateTimeOffset startedAt)
        => MarkRunning(startedAt, startedAt.AddMinutes(5));

    public void MarkRunning(DateTimeOffset startedAt, DateTimeOffset deadline)
    {
        EnsureNotTerminal();
        if (Status != FlowRunStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending run can start. 只有待处理状态的运行实例可以启动。");
        }

        if (deadline < startedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadline),
                "The execution deadline cannot be earlier than the start time. 执行截止时间不能早于开始时间。");
        }

        Status = FlowRunStatus.Running;
        StartedAt = startedAt;
        Deadline = deadline;
    }

    public void Complete(FlowRunStatus terminalStatus, DateTimeOffset endedAt, string? errorSummary = null)
    {
        EnsureNotTerminal();
        if (terminalStatus is not (FlowRunStatus.Succeeded or FlowRunStatus.Failed or FlowRunStatus.TimedOut))
        {
            throw new ArgumentException("Complete requires a successful, failed, or timed out status. 完成操作需要成功、失败或超时状态。", nameof(terminalStatus));
        }

        Status = terminalStatus;
        EndedAt = endedAt;
        ErrorSummary = errorSummary;
    }

    public void Cancel(string reason, DateTimeOffset endedAt)
    {
        if (Status == FlowRunStatus.Cancelled)
        {
            return;
        }

        EnsureNotTerminal();
        Status = FlowRunStatus.Cancelled;
        EndedAt = endedAt;
        CancellationReason ??= string.IsNullOrWhiteSpace(reason) ? "cancelled" : reason.Trim();
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
        {
            throw new InvalidOperationException("A terminal run cannot transition again. 终止状态的运行实例不能再次转换。");
        }
    }
}
