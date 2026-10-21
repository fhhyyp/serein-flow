using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework.Tests;

public sealed class McpToolPipelineTests
{
    [Fact]
    public async Task MiddlewareAndGeneratedHandlerShareTheAsyncCallScope()
    {
        var trace = new Trace();
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddScoped<McpFrameworkTests.ScopeProbe>();
        services.AddScoped<IMcpToolMiddleware, CallerMiddleware>();
        services.AddMcpToolHandlers(new Generated.McpToolRegistry(), typeof(McpFrameworkTests.GreetingTools));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var catalog = McpToolDiscovery.CreateCatalog(new Generated.McpToolRegistry(), toolTypes: [typeof(McpFrameworkTests.GreetingTools)]);
        var executor = new McpToolExecutor(provider.GetRequiredService<IServiceScopeFactory>());
        var result = Assert.IsType<McpFrameworkTests.Greeting>(await executor.ExecuteAsync(catalog, "greet",
            JsonSerializer.SerializeToElement(new { name = "Alice" })));
        Assert.Equal("middleware:Alice", result.Text);
        Assert.Equal(trace.Probe!.Id, result.ScopeId);
        Assert.True(trace.Probe.Disposed);
        Assert.Equal(["before", "after"], trace.Events);
    }

    [Fact]
    public async Task InvalidArgumentsAndMissingToolsUseFrameworkErrors()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var executor = new McpToolExecutor(provider.GetRequiredService<IServiceScopeFactory>());
        var catalog = new McpToolCatalog([]);
        await Assert.ThrowsAsync<McpParameterBindingException>(() => executor.ExecuteAsync(catalog, "missing",
            JsonSerializer.SerializeToElement(new[] { 1 })));
        await Assert.ThrowsAsync<McpToolNotFoundException>(() => executor.ExecuteAsync(catalog, "missing", default));
    }

    public sealed class Trace
    {
        public List<string> Events { get; } = [];
        public McpFrameworkTests.ScopeProbe? Probe { get; set; }
    }

    public sealed class CallerMiddleware(Trace trace, McpFrameworkTests.ScopeProbe probe) : IMcpToolMiddleware
    {
        public async Task<object?> InvokeAsync(McpToolExecutionContext context, McpToolExecutionDelegate next, CancellationToken cancellationToken)
        {
            trace.Probe = probe;
            trace.Events.Add("before");
            context.CallContext.SetValue(new McpFrameworkTests.Caller("middleware"));
            var value = await next(context, cancellationToken);
            trace.Events.Add("after");
            return value;
        }
    }
}
