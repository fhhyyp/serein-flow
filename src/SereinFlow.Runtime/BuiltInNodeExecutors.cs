using System.Threading.Tasks;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;
public sealed class FlowCallNodeExecutor : INodeExecutor, IFlowCallExecutorConfiguration
{
    private Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>>? _execute;
    public NodeType NodeType => NodeType.FlowCall;

    public void Configure(Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ValueTask<NodeExecutionResult> evaluatedValue = _execute is null ? resolveFromResultResult() : _execute(request, cancellationToken);
        return evaluatedValue;
        ValueTask<NodeExecutionResult> resolveFromResultResult()
        {
            var errorResult = NodeExecutionResult.Error(FlowCallErrorCodes.ExecutorNotConfigured, "FlowCall executor is not configured. FlowCall 执行器尚未配置。");
            var fromResultResult = ValueTask.FromResult(errorResult);
            return fromResultResult;
        }
    }
}
