using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework;

/// <summary>Implemented by the compile-time generated registry in each tool assembly.</summary>
public interface IMcpToolRegistry
{
    IReadOnlyList<McpGeneratedTool> Tools { get; }
}

/// <summary>Generated metadata and direct delegates; no MethodInfo or expression compilation.</summary>
public sealed record McpGeneratedTool(
    Type HandlerType,
    Type? RequestType,
    IMcpToolMetadata Metadata,
    Action<IServiceCollection> RegisterHandler,
    Func<McpToolCallContext, JsonElement, JsonSerializerOptions, CancellationToken, Task<object?>> Execute)
{
    public Func<JsonSerializerOptions, IReadOnlyList<string>>? RequiredProperties { get; init; }
}
