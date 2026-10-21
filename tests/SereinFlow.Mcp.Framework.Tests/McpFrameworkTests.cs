using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Mcp.Framework;

namespace SereinFlow.Mcp.Framework.Tests;

public sealed class McpFrameworkTests
{
    [Fact]
    public async Task StandaloneHostBindsRequestsAndResolvesScopedConstructorServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeProbe>();
        services.AddMcpToolHandlers(new Generated.McpToolRegistry(), typeof(GreetingTools));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var options = new McpToolDiscoveryOptions();
        var catalog = McpToolDiscovery.CreateCatalog(new Generated.McpToolRegistry(), options: options, toolTypes: [typeof(GreetingTools)]);
        var descriptor = Assert.Single(catalog.Descriptors);
        Assert.Equal("greet", descriptor.Name);
        Assert.Equal("object", descriptor.InputSchema.GetProperty("type").GetString());
        Assert.Contains(descriptor.InputSchema.GetProperty("required").EnumerateArray(), field => field.GetString() == "name");
        Assert.True(catalog.TryGet("greet", out var tool));
        ScopeProbe first;
        await using (var scope = provider.CreateAsyncScope())
        {
            first = scope.ServiceProvider.GetRequiredService<ScopeProbe>();
            var context = new McpToolCallContext(scope.ServiceProvider, new Dictionary<Type, object> { [typeof(Caller)] = new Caller("host") });
            var result = Assert.IsType<Greeting>(await tool!.ExecuteAsync(context, JsonSerializer.SerializeToElement(new { name = "Alice" })));
            Assert.Equal("host:Alice", result.Text);
            Assert.Equal(first.Id, result.ScopeId);
        }
        Assert.True(first.Disposed);
        await using var secondScope = provider.CreateAsyncScope();
        Assert.NotEqual(first.Id, secondScope.ServiceProvider.GetRequiredService<ScopeProbe>().Id);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"name\":12}")]
    [InlineData("{\"name\":\"  \"}")]
    public async Task InvalidInputsFailWithoutActivatingHandlers(string json)
    {
        var options = new McpToolDiscoveryOptions();
        var catalog = McpToolDiscovery.CreateCatalog(new Generated.McpToolRegistry(), options: options, toolTypes: [typeof(GreetingTools)]);
        Assert.True(catalog.TryGet("greet", out var tool));
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var document = JsonDocument.Parse(json);
        await Assert.ThrowsAsync<McpParameterBindingException>(() => tool!.ExecuteAsync(new McpToolCallContext(provider), document.RootElement));
    }

    [Fact]
    public void FrameworkAssemblyHasNoSereinFlowBusinessDependencies()
    {
        Assert.DoesNotContain(typeof(McpToolDiscovery).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name!.StartsWith("SereinFlow.", StringComparison.Ordinal));
    }

    public sealed class ScopeProbe : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [McpContext]
    public sealed record Caller(string Name);
    public sealed record GreetingRequest([property: Required] string Name);
    public sealed record Greeting(string Text, Guid ScopeId);

    public sealed class GreetingTools(ScopeProbe probe)
    {
        [McpTool("greet", Description = "Greet a person.")]
        public Task<Greeting> GreetAsync(GreetingRequest request, Caller caller, CancellationToken cancellationToken)
            => Task.FromResult(new Greeting($"{caller.Name}:{request.Name}", probe.Id));
    }

}
