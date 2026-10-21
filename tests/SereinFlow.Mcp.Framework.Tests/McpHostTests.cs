using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework.Tests;

public sealed class McpHostTests
{
    [Fact]
    public async Task GeneratedHostRegistersHandlersAndAppliesBusinessConfiguration()
    {
        var services = new ServiceCollection();
        services.AddStandaloneTools(new Generated.McpToolRegistry(), typeof(McpFrameworkTests.GreetingTools));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var catalog = StandaloneHost.CreateCatalog(new Generated.McpToolRegistry(), [typeof(McpFrameworkTests.GreetingTools)]);
        Assert.Contains(catalog.Descriptors.Single().InputSchema.GetProperty("properties").EnumerateObject(), property => property.Name == "Name");
        await using var scope = provider.CreateAsyncScope();
        var context = new McpToolCallContext(scope.ServiceProvider);
        context.SetValue(new McpFrameworkTests.Caller("generated host"));
        Assert.True(catalog.TryGet("greet", out var tool));
        var result = Assert.IsType<McpFrameworkTests.Greeting>(await tool!.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { Name = "Alice" })));
        Assert.Equal("generated host:Alice", result.Text);
    }

    [Fact]
    public void GeneratedDefaultHostWorksWithoutAnyConfigurationHooks()
    {
        var services = new ServiceCollection();
        DefaultHost.AddMcpTools(services);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<McpToolExecutor>());
        Assert.NotEmpty(DefaultHost.CreateCatalog().Descriptors);
    }
}

[McpHost("AddStandaloneTools")]
internal static partial class StandaloneHost
{
    static partial void ConfigureServices(IServiceCollection services) => services.AddScoped<McpFrameworkTests.ScopeProbe>();
    static partial void ConfigureJsonOptions(ref JsonSerializerOptions options) => options = new JsonSerializerOptions();
}

[McpHost]
internal static partial class DefaultHost;
