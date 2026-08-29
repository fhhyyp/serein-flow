namespace SereinFlow.Runtime.Abstractions;

/// <summary>
/// Restricted per-node context exposed to trusted node-library methods.
/// It allows a node to select an execution branch without exposing arbitrary
/// flow data, environment variables, or host services.
/// 提供给受信任节点类库方法的受限上下文。节点可以选择执行分支，但不能访问任意流程数据、
/// 环境变量或宿主服务。
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
/// Metadata identity used by the safe PE scanner.
/// 供安全 PE 扫描使用的元数据身份。
/// </summary>
public static class FlowContextContract
{
    public static readonly string FullName = typeof(IFlowContext).FullName ?? nameof(IFlowContext);
}
