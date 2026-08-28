namespace SereinFlow.Domain;

/// <summary>
/// Persisted control-plane state for one interactive debug run. Execution
/// state itself remains inside the isolated Worker process.
/// 一个交互式调试运行的持久化控制面状态。实际执行状态仍位于隔离的 Worker 进程中。
/// </summary>
public sealed class FlowDebugSession
{
    private FlowDebugSession(
        Guid id,
        Guid runId,
        Guid projectId,
        Guid flowId,
        IReadOnlyList<string> breakpointNodeIds,
        DateTimeOffset createdAt)
    {
        Id = id;
        RunId = runId;
        ProjectId = projectId;
        FlowId = flowId;
        BreakpointNodeIds = breakpointNodeIds;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = FlowDebugSessionStatus.Pending;
    }

    public Guid Id { get; }

    public Guid RunId { get; }

    public Guid ProjectId { get; }

    public Guid FlowId { get; }

    public IReadOnlyList<string> BreakpointNodeIds { get; }

    public FlowDebugSessionStatus Status { get; private set; }

    public string? CurrentNodeId { get; private set; }

    public Guid? ActiveInvocationId { get; private set; }

    public string? ActiveFlipflopNodeId { get; private set; }

    public int QueuedTriggerCount { get; private set; }

    /// <summary>
    /// Last control command accepted by the API. It is durable so a refreshed
    /// browser can continue with the next strictly increasing sequence.
    /// API 已接受的最后一个控制命令。持久化该值后，浏览器刷新仍可使用下一个严格递增序号继续控制。
    /// </summary>
    public long LastCommandSequence { get; private set; }

    public string? FailureMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsTerminal => Status is FlowDebugSessionStatus.Completed
        or FlowDebugSessionStatus.Cancelled
        or FlowDebugSessionStatus.Failed;

    public static FlowDebugSession Create(
        Guid runId,
        Guid projectId,
        Guid flowId,
        IEnumerable<string>? breakpointNodeIds,
        DateTimeOffset createdAt,
        Guid? id = null)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("Run ID cannot be empty. 运行 ID 不能为空。", nameof(runId));
        if (projectId == Guid.Empty)
            throw new ArgumentException("Project ID cannot be empty. 项目 ID 不能为空。", nameof(projectId));
        if (flowId == Guid.Empty)
            throw new ArgumentException("Flow ID cannot be empty. 流程 ID 不能为空。", nameof(flowId));

        var breakpoints = (breakpointNodeIds ?? [])
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        return new FlowDebugSession(id ?? Guid.NewGuid(), runId, projectId, flowId, breakpoints, createdAt);
    }

    public static FlowDebugSession Rehydrate(
        Guid id,
        Guid runId,
        Guid projectId,
        Guid flowId,
        IEnumerable<string>? breakpointNodeIds,
        FlowDebugSessionStatus status,
        string? currentNodeId,
        Guid? activeInvocationId,
        string? activeFlipflopNodeId,
        int queuedTriggerCount,
        long lastCommandSequence,
        string? failureMessage,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var session = Create(runId, projectId, flowId, breakpointNodeIds, createdAt, id);
        session.Status = status;
        session.CurrentNodeId = currentNodeId;
        session.ActiveInvocationId = activeInvocationId;
        session.ActiveFlipflopNodeId = activeFlipflopNodeId;
        session.QueuedTriggerCount = Math.Max(0, queuedTriggerCount);
        session.LastCommandSequence = Math.Max(0, lastCommandSequence);
        session.FailureMessage = failureMessage;
        session.UpdatedAt = updatedAt;
        return session;
    }

    public void MarkRunning(DateTimeOffset updatedAt)
    {
        EnsureNotTerminal();
        Status = FlowDebugSessionStatus.Running;
        CurrentNodeId = null;
        UpdatedAt = updatedAt;
    }

    public void Pause(
        string nodeId,
        DateTimeOffset updatedAt,
        Guid? activeInvocationId = null,
        string? activeFlipflopNodeId = null)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("Paused node ID cannot be empty. 暂停节点 ID 不能为空。", nameof(nodeId));

        Status = FlowDebugSessionStatus.Paused;
        CurrentNodeId = nodeId.Trim();
        ActiveInvocationId = activeInvocationId;
        ActiveFlipflopNodeId = activeFlipflopNodeId?.Trim();
        UpdatedAt = updatedAt;
    }

    public void Resume(DateTimeOffset updatedAt)
    {
        if (Status != FlowDebugSessionStatus.Paused)
            throw new InvalidOperationException("Only a paused debug session can resume. 只有暂停的调试会话可以继续运行。");

        Status = FlowDebugSessionStatus.Running;
        CurrentNodeId = null;
        UpdatedAt = updatedAt;
    }

    public void SetActiveInvocation(
        Guid? activeInvocationId,
        string? activeFlipflopNodeId,
        int queuedTriggerCount,
        DateTimeOffset updatedAt)
    {
        if (IsTerminal)
            return;

        ActiveInvocationId = activeInvocationId;
        ActiveFlipflopNodeId = activeFlipflopNodeId?.Trim();
        QueuedTriggerCount = Math.Max(0, queuedTriggerCount);
        UpdatedAt = updatedAt;
    }

    public void AcceptCommand(long commandSequence, DateTimeOffset updatedAt)
    {
        EnsureNotTerminal();
        if (commandSequence <= LastCommandSequence)
        {
            throw new InvalidOperationException(
                "The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。");
        }

        LastCommandSequence = commandSequence;
        UpdatedAt = updatedAt;
    }

    public void Complete(FlowRunStatus runStatus, string? message, DateTimeOffset updatedAt)
    {
        if (IsTerminal)
            return;

        Status = runStatus switch
        {
            FlowRunStatus.Succeeded => FlowDebugSessionStatus.Completed,
            FlowRunStatus.Cancelled => FlowDebugSessionStatus.Cancelled,
            _ => FlowDebugSessionStatus.Failed
        };
        CurrentNodeId = null;
        ActiveInvocationId = null;
        ActiveFlipflopNodeId = null;
        QueuedTriggerCount = 0;
        FailureMessage = Status == FlowDebugSessionStatus.Completed ? null : message?.Trim();
        UpdatedAt = updatedAt;
    }

    public void Fail(string message, DateTimeOffset updatedAt)
    {
        if (IsTerminal)
            return;

        Status = FlowDebugSessionStatus.Failed;
        CurrentNodeId = null;
        FailureMessage = string.IsNullOrWhiteSpace(message)
            ? "The debug session failed. 调试会话失败。"
            : message.Trim();
        UpdatedAt = updatedAt;
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
        {
            throw new InvalidOperationException(
                "A terminal debug session cannot transition again. 终态调试会话不能再次转换。");
        }
    }
}
