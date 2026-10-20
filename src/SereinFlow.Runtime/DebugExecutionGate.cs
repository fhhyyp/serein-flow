using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;
/// <summary>
/// A single-foreground debug gate. It pauses before configured breakpoints and
/// can grant one node permission at a time through continue, step, or cancel.
/// 单前台调试闸门。在配置的断点前暂停，并通过继续、单步或取消逐个授予节点执行许可。
/// </summary>
public sealed class DebugExecutionGate : IExecutionGate
{
    private readonly object _sync = new();
    private readonly HashSet<string> _breakpointNodeIds;
    private readonly Func<NodeExecutionBoundary, CancellationToken, ValueTask>? _onPaused;
    private TaskCompletionSource<ExecutionGateDecision>? _pendingDecision;
    private NodeExecutionBoundary? _currentBoundary;
    private bool _pauseBeforeNextNode;
    public DebugExecutionGate(IEnumerable<string> breakpointNodeIds, Func<NodeExecutionBoundary, CancellationToken, ValueTask>? onPaused = null)
    {
        ArgumentNullException.ThrowIfNull(breakpointNodeIds);
        var selectResult = breakpointNodeIds
            .Where(static nodeId =>
            {
                var evaluatedValue = !string.IsNullOrWhiteSpace(nodeId);
                return evaluatedValue;
            })
            .Select(static nodeId =>
            {
                var trimResult = nodeId.Trim();
                return trimResult;
            });
        _breakpointNodeIds = selectResult.ToHashSet(StringComparer.Ordinal);
        _onPaused = onPaused;
    }

    public NodeExecutionBoundary? CurrentBoundary
    {
        get
        {
            lock (_sync)
            {
                return _currentBoundary;
            }
        }
    }

    public async ValueTask<ExecutionGateDecision> BeforeNodeAsync(NodeExecutionBoundary boundary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        TaskCompletionSource<ExecutionGateDecision>? decisionSource;
        lock (_sync)
        {
            var shouldPause = _pauseBeforeNextNode || _breakpointNodeIds.Contains(boundary.NodeId);
            if (!shouldPause)
            {
                return ExecutionGateDecision.Proceed;
            }

            if (_pendingDecision is { Task.IsCompleted: false })
            {
                throw new InvalidOperationException("The debug gate already has a paused node. 调试闸门已有暂停节点。");
            }

            _pauseBeforeNextNode = false;
            _currentBoundary = boundary;
            decisionSource = new TaskCompletionSource<ExecutionGateDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingDecision = decisionSource;
        }

        try
        {
            if (_onPaused is not null)
            {
                var onPausedResult = _onPaused(boundary, cancellationToken);
                await onPausedResult.ConfigureAwait(false);
            }

            var waitAsyncResult = decisionSource.Task.WaitAsync(cancellationToken);
            var configureAwaitResult = await waitAsyncResult.ConfigureAwait(false);
            return configureAwaitResult;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_pendingDecision, decisionSource))
                {
                    _pendingDecision = null;
                    _currentBoundary = null;
                }
            }
        }
    }

    public bool TryContinue()
    {
        var tryReleaseResult = TryRelease(ExecutionGateDecision.Proceed, pauseBeforeNextNode: false);
        return tryReleaseResult;
    }

    public bool TryStep()
    {
        var tryReleaseResult1 = TryRelease(ExecutionGateDecision.Proceed, pauseBeforeNextNode: true);
        return tryReleaseResult1;
    }

    public bool TryCancel()
    {
        var tryReleaseResult2 = TryRelease(ExecutionGateDecision.Cancel, pauseBeforeNextNode: false);
        return tryReleaseResult2;
    }

    private bool TryRelease(ExecutionGateDecision decision, bool pauseBeforeNextNode)
    {
        lock (_sync)
        {
            if (_pendingDecision is null || _pendingDecision.Task.IsCompleted)
            {
                return false;
            }

            if (pauseBeforeNextNode)
            {
                _pauseBeforeNextNode = true;
            }

            var trySetResultResult = _pendingDecision.TrySetResult(decision);
            return trySetResultResult;
        }
    }
}
