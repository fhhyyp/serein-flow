using SereinFlow.Domain;
using LibraryVariableService = SereinFlow.Library.LibraryVariableService;

namespace SereinFlow.Runtime.Abstractions;

public interface IExecutionContext
{
    object? Read(string key);
    void Write(string key, object? value);
    IReadOnlyDictionary<string, object?> Snapshot();
}

/// <summary>
/// Carries request data for node execution operations.
/// 承载节点执行操作的请求数据。
/// </summary>
public sealed record NodeExecutionRequest(NodeDefinition Node, IExecutionContext Context, IReadOnlyDictionary<string, object?> Inputs, NodeExecutionRuntime? Runtime = null, int Step = 0, Guid ExecutionId = default);
/// <summary>
/// Immutable metadata exposed to a node execution. It is intentionally smaller
/// than the flow session so executors cannot inspect or mutate arbitrary flow data.
/// 节点执行可见的不可变元数据。它刻意小于流程会话，避免执行器读取或修改任意流程数据。
/// </summary>
public sealed record NodeExecutionEnvironment(Guid RunId, string ProjectId, Guid FlowId, string CanvasId, string NodeId, int FrameDepth, int Step = 0, Guid? ExecutionId = null);
/// <summary>
/// A structured log entry emitted by a node executor. The runtime assigns the
/// final sequence, run identity, node identity and persistence route.
/// 节点执行器发出的结构化日志。运行时负责分配最终序号、运行标识、节点标识和持久化链路。
/// </summary>
public sealed record NodeExecutionLogEntry(string Level, string Message, object? Value);
/// <summary>
/// The only event surface made available to a node executor.
/// 节点执行器唯一可用的事件出口。
/// </summary>
public interface INodeExecutionEventSink
{
    ValueTask PublishLogAsync(NodeExecutionLogEntry entry, CancellationToken cancellationToken);
}

public sealed class NullNodeExecutionEventSink : INodeExecutionEventSink
{
    public static readonly NullNodeExecutionEventSink Instance = new();
    private NullNodeExecutionEventSink()
    {
    }

    public ValueTask PublishLogAsync(NodeExecutionLogEntry entry, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

/// <summary>
/// Per-invocation facilities supplied by FlowRunner. The cancellation probe is
/// evaluated on demand so script env metadata never becomes stale.
/// FlowRunner 提供的每次调用设施。取消探针按需计算，确保脚本环境元数据不会过期。
/// </summary>
public sealed class NodeExecutionRuntime
{
    private readonly Func<bool> _isCancellationRequested;
    public NodeExecutionRuntime(NodeExecutionEnvironment environment, INodeExecutionEventSink? eventSink = null, Func<bool>? isCancellationRequested = null, LibraryVariableService? variables = null)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        EventSink = eventSink ?? NullNodeExecutionEventSink.Instance;
        _isCancellationRequested = isCancellationRequested ?? (static () => false);
        Variables = variables;
    }

    public NodeExecutionEnvironment Environment { get; }
    public INodeExecutionEventSink EventSink { get; }
    /// <summary>
    /// The table shared by all node invocations in this run, when supplied by the host.
    /// 宿主提供的运行级变量表，由本次运行的全部节点调用共用。
    /// </summary>
    public LibraryVariableService? Variables { get; }

    public bool IsCancellationRequested
    {
        get
        {
            return _isCancellationRequested();
        }
    }
}

/// <summary>
/// Captures the outcome of node execution operations.
/// 记录节点执行操作的结果。
/// </summary>
public sealed record NodeExecutionResult(bool IsSuccess, IReadOnlyDictionary<string, object?> Outputs, ExecutionBranch NextBranch = ExecutionBranch.Success, string? ErrorCode = null, string? ErrorMessage = null, IReadOnlyDictionary<string, object?>? Inputs = null, IReadOnlyDictionary<string, object?>? TransferOutputs = null)
{
    public static NodeExecutionResult Success(IReadOnlyDictionary<string, object?>? outputs = null)
    {
        return new(true, outputs ?? new Dictionary<string, object?>(), ExecutionBranch.Success);
    }

    public static NodeExecutionResult Failure(string errorCode, string errorMessage)
    {
        return new(false, new Dictionary<string, object?>(), ExecutionBranch.Failure, errorCode, errorMessage);
    }

    public static NodeExecutionResult Error(string errorCode, string errorMessage)
    {
        var outputs = new Dictionary<string, object?>();
        return new(false, outputs, ExecutionBranch.Error, errorCode, errorMessage);
    }
}

public interface INodeExecutor
{
    NodeType NodeType { get; }

    ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken);
}

public interface IGlobalFlipflopExecutor
{
    ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken);
}

public interface IFlowCallExecutorConfiguration
{
    void Configure(Func<NodeExecutionRequest, CancellationToken, ValueTask<NodeExecutionResult>> execute);
}

/// <summary>
/// Describes a runtime event.
/// 描述Runtime事件。
/// </summary>
public sealed record RuntimeEvent(Guid RunId, long Sequence, DateTimeOffset Timestamp, string Type, string? NodeId, IReadOnlyDictionary<string, object?> Payload);
public interface IRunEventPublisher
{
    ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken);
}

public sealed class NullRunEventPublisher : IRunEventPublisher
{
    public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
