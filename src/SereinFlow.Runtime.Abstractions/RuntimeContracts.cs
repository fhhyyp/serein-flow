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
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static NodeExecutionResult Success(IReadOnlyDictionary<string, object?>? outputs = null)
        => new(true, outputs ?? new Dictionary<string, object?>());

    public static NodeExecutionResult Failure(string errorCode, string errorMessage)
        => new(false, new Dictionary<string, object?>(), errorCode, errorMessage);
}

public interface INodeExecutor
{
    NodeType NodeType { get; }

    ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken);
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
