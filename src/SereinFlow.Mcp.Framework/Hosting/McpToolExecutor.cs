using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The public middleware contract exposes this named delegate; retain its type identity for host implementations.")]
public delegate Task<object?> McpToolExecutionDelegate(McpToolExecutionContext context, CancellationToken cancellationToken);

/// <summary>
/// Host policies wrap the shared execution pipeline without replacing tool models.
/// 宿主策略包裹共用执行管线，不替换工具模型。
/// </summary>
public interface IMcpToolMiddleware
{
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "The public middleware parameter supports existing C# named calls and matches the established continuation convention.")]
    Task<object?> InvokeAsync(McpToolExecutionContext context, McpToolExecutionDelegate next, CancellationToken cancellationToken);
}

public sealed class McpToolExecutionContext
{
    internal McpToolExecutionContext(IServiceScope scope, string name, JsonElement arguments, McpToolDefinition? tool)
    {
        Scope = scope;
        Name = name;
        Arguments = arguments;
        Tool = tool;
        CallContext = new McpToolCallContext(scope.ServiceProvider);
    }

    public IServiceScope Scope { get; }
    public McpToolCallContext CallContext { get; }
    public string Name { get; }
    public JsonElement Arguments { get; }
    public McpToolDefinition? Tool { get; }
    public bool CanExecute => Tool is not null && Arguments.ValueKind is JsonValueKind.Object or JsonValueKind.Undefined;
}

public sealed class McpToolNotFoundException(string name) : Exception($"MCP tool '{name}' is not supported. 不支持此 MCP 工具。");

/// <summary>
/// Owns tool lookup, per-call async DI scope, middleware ordering and direct invocation.
/// 负责工具查找、每次调用的异步依赖注入作用域、中间件顺序和直接调用。
/// </summary>
public sealed class McpToolExecutor(IServiceScopeFactory scopeFactory)
{
    public async Task<object?> ExecuteAsync(McpToolCatalog catalog, string name, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        await using var scope = scopeFactory.CreateAsyncScope();
        catalog.TryGet(name, out var tool);
        var context = new McpToolExecutionContext(scope, name, arguments, tool);
        McpToolExecutionDelegate pipeline = InvokeToolAsync;
        var middleware = scope.ServiceProvider.GetServices<IMcpToolMiddleware>().ToArray();
        for (var index = middleware.Length - 1; index >= 0; index--)
        {
            var current = middleware[index];
            var next = pipeline;
            pipeline = (call, cancellation) => current.InvokeAsync(call, next, cancellation);
        }
        return await pipeline(context, cancellationToken);
    }

    private static Task<object?> InvokeToolAsync(McpToolExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Tool is not null)
            return context.Tool.ExecuteAsync(context.CallContext, context.Arguments, cancellationToken);
        if (context.Arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
            throw new McpParameterBindingException("MCP tool arguments must be a JSON object. MCP 工具参数必须是 JSON 对象。", "$");
        throw new McpToolNotFoundException(context.Name);
    }
}
