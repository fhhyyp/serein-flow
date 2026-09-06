using SereinFlow.Core.Api;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Library;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Worker.Runner;

internal sealed class LibraryFlowContext(
    Guid runId,
    string nodeId,
    CancellationToken cancellationToken,
    Guid executionId) : IFlowContext
{
    private ExecutionBranch _branch = ExecutionBranch.Success;
    private string? _code;
    private string? _message;

    public Guid RunId { get; } = runId;

    public string NodeId { get; } = nodeId;

    public Guid ExecutionId { get; } = executionId;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public void SelectSuccess()
    {
        _branch = ExecutionBranch.Success;
        _code = null;
        _message = null;
    }

    public void SelectFailure(string? code = null, string? message = null)
    {
        _branch = ExecutionBranch.Failure;
        _code = string.IsNullOrWhiteSpace(code) ? NodeErrorCodes.BranchFailure : code;
        _message = string.IsNullOrWhiteSpace(message)
            ? "The node selected the Failure branch. 节点选择了 Failure 分支。"
            : message;
    }

    public void SelectError(string? code = null, string? message = null)
    {
        _branch = ExecutionBranch.Error;
        _code = string.IsNullOrWhiteSpace(code) ? NodeErrorCodes.BranchError : code;
        _message = string.IsNullOrWhiteSpace(message)
            ? "The node selected the Error branch. 节点选择了 Error 分支。"
            : message;
    }

    public NodeExecutionResult Apply(NodeExecutionResult result)
        => _branch == ExecutionBranch.Success
            ? result
            : result with
            {
                IsSuccess = false,
                NextBranch = _branch,
                ErrorCode = _code,
                ErrorMessage = _message
            };
}
