using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class FlowCallNodeExecutor : INodeExecutor, IFlowCallExecutorConfiguration
{
    private Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>>? _execute;

    public NodeType NodeType => NodeType.FlowCall;

    public void Configure(Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>> execute)
        => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        => _execute is null
            ? ValueTask.FromResult(NodeExecutionResult.Error(
                FlowCallErrorCodes.ExecutorNotConfigured,
                "FlowCall executor is not configured. FlowCall 执行器尚未配置。"))
            : _execute(request, cancellationToken);
}
