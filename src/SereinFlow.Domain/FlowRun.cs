namespace SereinFlow.Domain;

public sealed class FlowRun
{
    private FlowRun(Guid id, Guid flowId, long flowVersion, DateTimeOffset createdAt)
    {
        Id = id;
        FlowId = flowId;
        FlowVersion = flowVersion;
        CreatedAt = createdAt;
        Status = FlowRunStatus.Pending;
    }

    public Guid Id { get; }

    public Guid FlowId { get; }

    public long FlowVersion { get; }

    public FlowRunStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? ErrorSummary { get; private set; }

    public bool IsTerminal => Status is FlowRunStatus.Succeeded or FlowRunStatus.Failed or FlowRunStatus.Cancelled or FlowRunStatus.TimedOut;

    public static FlowRun Start(Guid flowId, long flowVersion, DateTimeOffset createdAt, Guid? id = null)
    {
        if (flowId == Guid.Empty)
        {
            throw new ArgumentException("Flow ID cannot be empty. 流程 ID 不能为空。", nameof(flowId));
        }

        if (flowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(flowVersion), "Flow version must be positive. 流程版本必须为正数。");

        return new FlowRun(id ?? Guid.NewGuid(), flowId, flowVersion, createdAt);
    }

    public void MarkRunning(DateTimeOffset startedAt)
    {
        EnsureNotTerminal();
        if (Status != FlowRunStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending run can start. 只有待处理状态的运行实例可以启动。");
        }

        Status = FlowRunStatus.Running;
        StartedAt = startedAt;
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
