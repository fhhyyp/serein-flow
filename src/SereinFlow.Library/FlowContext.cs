namespace SereinFlow.Runtime.Abstractions;

/// <summary>
/// Provides restricted per-node context to trusted node-library methods. It allows a node to
/// select an execution branch without exposing arbitrary flow data, environment variables, or
/// host services.
/// 向受信任的节点类库方法提供受限的节点上下文。它允许节点选择执行分支，但不会公开任意流程数据、
/// 环境变量或宿主服务。
/// </summary>
public interface IFlowContext
{
    /// <summary>
    /// Gets the unique identifier of the active flow run.
    /// 获取当前活动流程运行的唯一标识。
    /// </summary>
    Guid RunId { get; }

    /// <summary>
    /// Gets the stable identifier of the node currently being invoked.
    /// 获取当前正在调用节点的稳定标识。
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// Gets the unique identifier of this node execution step within the flow run.
    /// 获取本次节点执行步骤在流程运行中的唯一标识。
    /// </summary>
    Guid ExecutionId { get; }

    /// <summary>
    /// Gets the token that is cancelled when the Worker stops or cancels the current flow run.
    /// 获取当 Worker 停止或取消当前流程运行时会被取消的令牌。
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Selects the success branch for the current node invocation.
    /// 为当前节点调用选择成功分支。
    /// </summary>
    void SelectSuccess();

    /// <summary>
    /// Selects the failure branch for the current node invocation.
    /// 为当前节点调用选择失败分支。
    /// </summary>
    /// <param name="code">
    /// An optional machine-readable failure code.
    /// 可选的、可供机器读取的失败代码。
    /// </param>
    /// <param name="message">
    /// An optional human-readable failure message.
    /// 可选的、供人阅读的失败消息。
    /// </param>
    void SelectFailure(string? code = null, string? message = null);

    /// <summary>
    /// Selects the error branch for the current node invocation.
    /// 为当前节点调用选择错误分支。
    /// </summary>
    /// <param name="code">
    /// An optional machine-readable error code.
    /// 可选的、可供机器读取的错误代码。
    /// </param>
    /// <param name="message">
    /// An optional human-readable error message.
    /// 可选的、供人阅读的错误消息。
    /// </param>
    void SelectError(string? code = null, string? message = null);
}

/// <summary>
/// Provides the metadata identity used by the safe PE scanner.
/// 提供安全 PE 扫描器使用的元数据标识。
/// </summary>
public static class FlowContextContract
{
    /// <summary>
    /// Gets the fully qualified metadata name of <see cref="IFlowContext"/>.
    /// 获取 <see cref="IFlowContext"/> 的完全限定元数据名称。
    /// </summary>
    public static readonly string FullName = typeof(IFlowContext).FullName ?? nameof(IFlowContext);
}
