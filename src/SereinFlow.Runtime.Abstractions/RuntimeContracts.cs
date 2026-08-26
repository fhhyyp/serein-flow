using SereinFlow.Domain;

namespace SereinFlow.Runtime.Abstractions;

public interface IExecutionContext
{
    object? Read(string key);

    void Write(string key, object? value);

    IReadOnlyDictionary<string, object?> Snapshot();
}

public sealed record NodeExecutionRequest(
    NodeDefinition Node,
    IExecutionContext Context,
    IReadOnlyDictionary<string, object?> Inputs);

public sealed record NodeExecutionResult(
    bool IsSuccess,
    IReadOnlyDictionary<string, object?> Outputs,
    ExecutionBranch NextBranch = ExecutionBranch.Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyDictionary<string, object?>? Inputs = null)
{
    public static NodeExecutionResult Success(IReadOnlyDictionary<string, object?>? outputs = null)
        => new(true, outputs ?? new Dictionary<string, object?>(), ExecutionBranch.Success);

    public static NodeExecutionResult Failure(string errorCode, string errorMessage)
        => new(false, new Dictionary<string, object?>(), ExecutionBranch.Failure, errorCode, errorMessage);

    public static NodeExecutionResult Error(string errorCode, string errorMessage)
        => new(false, new Dictionary<string, object?>(), ExecutionBranch.Error, errorCode, errorMessage);
}

public interface INodeExecutor
{
    NodeType NodeType { get; }

    ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken);
}

public interface IGlobalFlipflopExecutor
{
    ValueTask<NodeExecutionResult> WaitForTriggerAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken);
}

public interface IFlowCallExecutorConfiguration
{
    void Configure(Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>> execute);
}

public sealed record RuntimeEvent(
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string? NodeId,
    IReadOnlyDictionary<string, object?> Payload);

public interface IRunEventPublisher
{
    ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken);
}

public sealed class NullRunEventPublisher : IRunEventPublisher
{
    public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
