using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Implemented by the compile-time generated registry in each tool assembly.
/// 由各工具程序集内编译生成的注册表实现。
/// </summary>
public interface IMcpToolRegistry
{
    IReadOnlyList<McpGeneratedTool> Tools { get; }
}

/// <summary>
/// Generated metadata and direct delegates; no MethodInfo or expression compilation.
/// 生成的元数据和直接委托，不使用 MethodInfo 或表达式编译。
/// </summary>
public sealed record McpGeneratedTool(
    Type HandlerType,
    Type? RequestType,
    IMcpToolMetadata Metadata,
    Action<IServiceCollection> RegisterHandler,
    Func<McpToolCallContext, JsonElement, JsonSerializerOptions, CancellationToken, Task<object?>> Execute)
{
    public Func<JsonSerializerOptions, IReadOnlyList<string>>? RequiredProperties { get; init; }
}
