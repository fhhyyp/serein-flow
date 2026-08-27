using SereinFlow.Domain;

namespace SereinFlow.Runtime.Abstractions;

public interface IExecutionContext
{
    object? Read(string key);

    void Write(string key, object? value);

    IReadOnlyDictionary<string, object?> Snapshot();
}

/// <summary>
/// A per-node, restricted control surface injected into trusted node-library
/// methods. It exposes no environment or arbitrary flow data APIs, so a node
/// can choose its branch without relying on exception control flow.
/// 每次节点调用注入的受限控制面。它不暴露环境或任意流程数据 API，使节点方法可在
/// 不依赖异常控制流的情况下选择分支。
/// </summary>
public interface IFlowContext
{
    Guid RunId { get; }

    string NodeId { get; }

    CancellationToken CancellationToken { get; }

    void SelectSuccess();

    void SelectFailure(string? code = null, string? message = null);

    void SelectError(string? code = null, string? message = null);
}

/// <summary>
/// Metadata identity used by safe PE scanning. Keeping it beside the public
/// interface avoids handwritten type-name literals in scanner code.
/// 供安全 PE 扫描使用的元数据身份。它与公开接口放在一起，避免扫描器手写类型名称。
/// </summary>
public static class FlowContextContract
{
    public static readonly string FullName = typeof(IFlowContext).FullName ?? nameof(IFlowContext);
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
