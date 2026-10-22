using SereinFlow.Contracts;
using SereinFlow.Runtime;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;
internal sealed class DebugRunController
{
    private readonly Guid _runId;
    private readonly WorkerDebugOptionsDto _options;
    private readonly object _sync = new();
    private DebugExecutionGate? _gate;
    private long _lastCommandSequence;
    public DebugRunController(Guid runId, WorkerDebugOptionsDto options)
    {
        if (options.DebugSessionId == Guid.Empty)
        {
            throw new ArgumentException("The debug session ID cannot be empty. 调试会话 ID 不能为空。", nameof(options));
        }

        _runId = runId;
        _options = options;
    }

    public DebugExecutionGate CreateGate(FlowExecutionSession session, WorkerEventPublisher publisher)
    {
        var gate = new DebugExecutionGate(_options.BreakpointNodeIds, (boundary, token) =>
        {
            var toStringResult = boundary.NodeType.ToString();
            var pause = new WorkerDebugPauseDto(_options.DebugSessionId, boundary.RunId, boundary.NodeId, toStringResult, boundary.Step, boundary.Inputs, boundary.FrameDepth, boundary.InvocationId, boundary.ExecutionId);
            var nextSequenceResult = session.NextSequence();
            var publishDebugPausedAsyncResult = publisher.PublishDebugPausedAsync(pause, nextSequenceResult, token);
            return publishDebugPausedAsyncResult;
        });
        lock (_sync)
        {
            _gate = gate;
        }

        return gate;
    }

    public bool TryContinue(WorkerDebugCommandDto command)
    {
        var tryApplyResult = TryApply(command, static gate =>
        {
            var tryContinueResult = gate.TryContinue();
            return tryContinueResult;
        });
        return tryApplyResult;
    }

    public bool TryStep(WorkerDebugCommandDto command)
    {
        var tryApplyResult1 = TryApply(command, static gate =>
        {
            var tryStepResult = gate.TryStep();
            return tryStepResult;
        });
        return tryApplyResult1;
    }

    public bool TryStop(WorkerDebugCommandDto command)
    {
        if (!IsValidCommand(command))
        {
            return false;
        }

        lock (_sync)
        {
            if (command.CommandSequence <= _lastCommandSequence)
            {
                return false;
            }

            // A listener may be blocked in WaitForTriggerAsync and have no
            // current boundary. Stop still needs to cancel that wait.
            // 监听器可能阻塞在 WaitForTriggerAsync，且没有当前执行边界；停止命令仍须取消该等待。
            _gate?.TryCancel();
            _lastCommandSequence = command.CommandSequence;
            return true;
        }
    }

    private bool TryApply(WorkerDebugCommandDto command, Func<DebugExecutionGate, bool> apply)
    {
        if (!IsValidCommand(command))
        {
            return false;
        }

        lock (_sync)
        {
            if (command.CommandSequence <= _lastCommandSequence || _gate is null)
            {
                return false;
            }

            if (!apply(_gate))
            {
                return false;
            }

            _lastCommandSequence = command.CommandSequence;
            return true;
        }
    }

    private bool IsValidCommand(WorkerDebugCommandDto command) => command.ProtocolVersion == WorkerProtocolConstants.Version && command.RunId == _runId && command.DebugSessionId == _options.DebugSessionId && command.CommandSequence >= 1;
}
