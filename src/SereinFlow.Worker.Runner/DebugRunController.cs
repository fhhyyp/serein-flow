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
            throw new ArgumentException(
                "The debug session ID cannot be empty. 调试会话 ID 不能为空。",
                nameof(options));
        }

        _runId = runId;
        _options = options;
    }

    public DebugExecutionGate CreateGate(FlowExecutionSession session, WorkerEventPublisher publisher)
    {
        var gate = new DebugExecutionGate(_options.BreakpointNodeIds, (boundary, token) =>
        {
            var pause = new WorkerDebugPauseDto(
                _options.DebugSessionId,
                boundary.RunId,
                boundary.NodeId,
                boundary.NodeType.ToString(),
                boundary.Step,
                boundary.Inputs,
                boundary.FrameDepth,
                boundary.InvocationId,
                boundary.ExecutionId);
            return publisher.PublishDebugPausedAsync(pause, session.NextSequence(), token);
        });
        lock (_sync)
            _gate = gate;
        return gate;
    }

    public bool TryContinue(WorkerDebugCommandDto command)
        => TryApply(command, static gate => gate.TryContinue());

    public bool TryStep(WorkerDebugCommandDto command)
        => TryApply(command, static gate => gate.TryStep());

    public bool TryStop(WorkerDebugCommandDto command)
    {
        if (!IsValidCommand(command))
            return false;

        lock (_sync)
        {
            if (command.CommandSequence <= _lastCommandSequence)
                return false;

            // A listener may be blocked in WaitForTriggerAsync and have no
            // current boundary. Stop still needs to cancel that wait.
            _gate?.TryCancel();
            _lastCommandSequence = command.CommandSequence;
            return true;
        }
    }

    private bool TryApply(WorkerDebugCommandDto command, Func<DebugExecutionGate, bool> apply)
    {
        if (!IsValidCommand(command))
            return false;

        lock (_sync)
        {
            if (command.CommandSequence <= _lastCommandSequence || _gate is null)
                return false;

            if (!apply(_gate))
                return false;

            _lastCommandSequence = command.CommandSequence;
            return true;
        }
    }

    private bool IsValidCommand(WorkerDebugCommandDto command)
        => command.ProtocolVersion == WorkerProtocolConstants.Version
           && command.RunId == _runId
           && command.DebugSessionId == _options.DebugSessionId
           && command.CommandSequence >= 1;
}
