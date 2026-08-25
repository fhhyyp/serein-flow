using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class ConditionNodeExecutor : INodeExecutor
{
    public NodeType NodeType => NodeType.Condition;

    public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = request.Inputs.Values.FirstOrDefault();
        if (value is not bool condition)
        {
            return ValueTask.FromResult(NodeExecutionResult.Failure(
                "condition.input_not_boolean",
                "Condition input must be a Boolean. Condition 输入必须是布尔值。"));
        }

        var result = new NodeExecutionResult(
            true,
            new Dictionary<string, object?> { ["value"] = condition },
            condition ? ExecutionBranch.Success : ExecutionBranch.Failure);
        return ValueTask.FromResult(result);
    }
}

public sealed class FlowCallNodeExecutor : INodeExecutor, IFlowCallExecutorConfiguration
{
    private Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>>? _execute;

    public NodeType NodeType => NodeType.FlowCall;

    public void Configure(Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>> execute)
        => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        => _execute is null
            ? ValueTask.FromResult(NodeExecutionResult.Error(
                "flowcall.executor_not_configured",
                "FlowCall executor is not configured. FlowCall 执行器尚未配置。"))
            : _execute(request, cancellationToken);
}
