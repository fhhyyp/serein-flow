using SereinFlow.Domain;

namespace SereinFlow.Runtime.Abstractions;

/// <summary>
/// A safe execution boundary reached after a node's inputs have been resolved
/// and before its executor can cause any side effect.
/// 节点输入解析完成且执行器尚未产生副作用时到达的安全执行边界。
/// </summary>
public sealed record NodeExecutionBoundary(
    Guid RunId,
    string NodeId,
    NodeType NodeType,
    int Step,
    IReadOnlyDictionary<string, object?> Inputs,
    int FrameDepth,
    Guid? InvocationId = null,
    Guid? ExecutionId = null);

public enum ExecutionGateDecision
{
    Proceed,
    Cancel
}

/// <summary>
/// Controls whether a node may enter its executor. Implementations must not
/// execute nodes or mutate the execution context.
/// 控制节点是否可以进入执行器。实现不得执行节点或修改执行上下文。
/// </summary>
public interface IExecutionGate
{
    ValueTask<ExecutionGateDecision> BeforeNodeAsync(
        NodeExecutionBoundary boundary,
        CancellationToken cancellationToken);
}

public sealed class NoopExecutionGate : IExecutionGate
{
    public static readonly NoopExecutionGate Instance = new();

    private NoopExecutionGate()
    {
    }

    public ValueTask<ExecutionGateDecision> BeforeNodeAsync(
        NodeExecutionBoundary boundary,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(ExecutionGateDecision.Proceed);
}
