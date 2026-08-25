using System.Collections.Concurrent;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class FlowExecutionSession : IExecutionContext, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cancellationSource;
    private readonly SharedExecutionState _sharedState;
    private int _cancelled;

    public FlowExecutionSession(
        Guid? runId = null,
        CancellationToken cancellationToken = default,
        int maxSteps = 10_000,
        int maxNodeVisits = 1_000)
        : this(runId, cancellationToken, maxSteps, maxNodeVisits, null)
    {
    }

    private FlowExecutionSession(
        Guid? runId,
        CancellationToken cancellationToken,
        int maxSteps,
        int maxNodeVisits,
        SharedExecutionState? sharedState)
    {
        if (maxSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "Maximum flow steps must be positive. 最大流程步数必须为正数。");
        if (maxNodeVisits < 1)
            throw new ArgumentOutOfRangeException(nameof(maxNodeVisits), "Maximum node visits must be positive. 最大节点访问次数必须为正数。");
        RunId = runId ?? Guid.NewGuid();
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sharedState = sharedState ?? new SharedExecutionState();
        Resources = new ResourceLeaseRegistry();
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
        var child = new FlowExecutionSession(RunId, CancellationToken, MaxSteps, MaxNodeVisits, _sharedState);
        if (Plan is not null)
            child.AttachPlan(Plan);
        foreach (var value in Snapshot())
            child.Write(value.Key, value.Value);
        return child;
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
