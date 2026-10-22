namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Runtime policy contract implemented by compile-time generated declarations.
/// 由编译期生成声明实现的运行时策略契约。
/// </summary>
public interface IMcpToolMetadata
{
    string Name { get; }
    string Description { get; }
    McpToolExecutionKind ExecutionKind { get; }
    bool RequiresIdempotencyKey { get; }
}

/// <summary>
/// Identifies whether an MCP tool reads data or changes state.
/// 标识 MCP 工具用于读取数据还是变更状态。
/// </summary>
public enum McpToolExecutionKind
{
    /// <summary>
    /// Indicates the read value.
    /// 表示“读取”这一取值。
    /// </summary>
    Read,
    /// <summary>
    /// Indicates the mutation value.
    /// 表示“Mutation”这一取值。
    /// </summary>
    Mutation,
}
