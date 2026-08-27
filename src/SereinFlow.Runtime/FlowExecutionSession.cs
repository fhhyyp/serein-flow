using System.Collections.Concurrent;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class FlowExecutionSession : IExecutionContext, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cancellationSource;
    private readonly SharedExecutionState _sharedState;
    private readonly bool _ownsResources;
    private readonly IReadOnlyDictionary<string, object?> _flowCallInputs;
    private readonly int _frameDepth;
    private int _cancelled;

    public FlowExecutionSession(
        Guid? runId = null,
        CancellationToken cancellationToken = default,
        int maxSteps = 10_000,
        int maxNodeVisits = 1_000)
        : this(runId, cancellationToken, maxSteps, maxNodeVisits, null, null, ownsResources: true, null, frameDepth: 0)
    {
    }

    private FlowExecutionSession(
        Guid? runId,
        CancellationToken cancellationToken,
        int maxSteps,
        int maxNodeVisits,
        SharedExecutionState? sharedState,
        ResourceLeaseRegistry? resources,
        bool ownsResources,
        IReadOnlyDictionary<string, object?>? flowCallInputs,
        int frameDepth)
    {
        if (maxSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "Maximum flow steps must be positive. 最大流程步数必须为正数。");
        if (maxNodeVisits < 1)
            throw new ArgumentOutOfRangeException(nameof(maxNodeVisits), "Maximum node visits must be positive. 最大节点访问次数必须为正数。");
        RunId = runId ?? Guid.NewGuid();
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sharedState = sharedState ?? new SharedExecutionState();
        Resources = resources ?? new ResourceLeaseRegistry();
        _ownsResources = ownsResources;
        _flowCallInputs = flowCallInputs ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        _frameDepth = frameDepth;
        MaxSteps = maxSteps;
        MaxNodeVisits = maxNodeVisits;
    }

    public Guid RunId { get; }

    public ResourceLeaseRegistry Resources { get; }

    public int MaxSteps { get; }

    public int MaxNodeVisits { get; }

    public int StepCount => Volatile.Read(ref _sharedState.StepCount);

    internal ExecutionPlan? Plan { get; private set; }

    public CancellationToken CancellationToken => _cancellationSource.Token;

    public object? Read(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Execution context keys cannot be empty. 执行上下文键不能为空。", nameof(key));
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    public void Write(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Execution context keys cannot be empty. 执行上下文键不能为空。", nameof(key));
        _values[key] = value;
    }

    public IReadOnlyDictionary<string, object?> Snapshot() => new Dictionary<string, object?>(_values, StringComparer.Ordinal);

    internal void AttachPlan(ExecutionPlan plan) => Plan = plan;

    public bool TryBeginStep(string nodeId, out string? errorCode)
    {
        var step = Interlocked.Increment(ref _sharedState.StepCount);
        if (step > MaxSteps)
        {
            errorCode = "flow.step_limit_exceeded";
            return false;
        }

        var visits = _sharedState.NodeVisits.AddOrUpdate(nodeId, 1, static (_, count) => count + 1);
        if (visits > MaxNodeVisits)
        {
            errorCode = "flow.cycle_detected";
            return false;
        }

        errorCode = null;
        return true;
    }

    public long NextSequence() => Interlocked.Increment(ref _sharedState.Sequence);

    internal FlowExecutionSession CreateChild()
    {
        var child = new FlowExecutionSession(
            RunId,
            CancellationToken,
            MaxSteps,
            MaxNodeVisits,
            _sharedState,
            Resources,
            ownsResources: false,
            null,
            // A listener trigger is an isolated value context, not a FlowCall
            // frame. Keep its visible depth unchanged so audit consumers can
            // distinguish FlowCall nesting from trigger instances.
            // 监听触发器是隔离值上下文，并非 FlowCall 调用帧；保持可见深度不变，便于审计区分两者。
            FrameDepth);
        if (Plan is not null)
            child.AttachPlan(Plan);
        CopyProjectScopeTo(child);
        return child;
    }

    /// <summary>
    /// Creates an isolated FlowCall frame. It shares the run identity,
    /// cancellation token, resource leases and safety budget, while explicitly
    /// excluding the caller's node outputs.
    /// 创建隔离的 FlowCall 调用帧。它共享运行标识、取消令牌、资源租约和安全预算，
    /// 但明确排除调用方的节点输出。
    /// </summary>
    internal FlowExecutionSession CreateFlowCallFrame(
        string targetNodeId,
        IReadOnlyDictionary<string, object?> inputs)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
            throw new ArgumentException("FlowCall target node ID cannot be empty. FlowCall 目标节点 ID 不能为空。", nameof(targetNodeId));
        ArgumentNullException.ThrowIfNull(inputs);

        var child = new FlowExecutionSession(
            RunId,
            CancellationToken,
            MaxSteps,
            MaxNodeVisits,
            _sharedState,
            Resources,
            ownsResources: false,
            new Dictionary<string, object?>(inputs, StringComparer.Ordinal),
            FrameDepth + 1);
        if (Plan is not null)
            child.AttachPlan(Plan);
        child._flowCallTargetNodeId = targetNodeId;
        CopyProjectScopeTo(child);
        return child;
    }

    private string? _flowCallTargetNodeId;

    internal bool TryReadFlowCallInput(string nodeId, string parameterId, out object? value)
    {
        if (string.Equals(nodeId, _flowCallTargetNodeId, StringComparison.Ordinal)
            && _flowCallInputs.TryGetValue(parameterId, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    public int FrameDepth => _frameDepth;

    private void CopyProjectScopeTo(FlowExecutionSession child)
    {
        foreach (var value in Snapshot())
        {
            if (value.Key.StartsWith("project.", StringComparison.Ordinal)
                || string.Equals(value.Key, "projectId", StringComparison.Ordinal))
            {
                child.Write(value.Key, value.Value);
            }
        }
    }

    public bool Cancel()
    {
        if (Interlocked.Exchange(ref _cancelled, 1) == 1)
        {
            return false;
        }

        _cancellationSource.Cancel();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsResources)
            await Resources.DisposeAsync();
        _cancellationSource.Dispose();
    }

    private sealed class SharedExecutionState
    {
        public long Sequence;
        public int StepCount;
        public ConcurrentDictionary<string, int> NodeVisits { get; } = new(StringComparer.Ordinal);
    }
}
