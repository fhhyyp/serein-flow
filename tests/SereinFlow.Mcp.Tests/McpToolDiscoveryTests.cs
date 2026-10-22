using SereinFlow.Security.ApiKeys;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;

namespace SereinFlow.Mcp.Tests;

public sealed class McpToolDiscoveryTests
{
    [Fact]
    public async Task DiscoversBindsAndDisposesScopedHandlersWithoutCapturingThemInCatalog()
    {
        var services = new ServiceCollection();
        services.AddScoped<Probe>();
        services.AddTestAuthorization();
        services.AddSereinFlowMcpTools(new Generated.McpToolRegistry(), typeof(EchoTools));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var catalog = SereinFlowMcpHosting.CreateCatalog(new Generated.McpToolRegistry(), [typeof(EchoTools)]);
        var descriptor = Assert.Single(catalog.Descriptors);
        Assert.Equal("echo", descriptor.Name);
        Assert.Equal("object", descriptor.InputSchema.GetProperty("type").GetString());
        Assert.True(descriptor.InputSchema.GetProperty("properties").TryGetProperty("name", out _));
        Assert.True(catalog.TryGet("echo", out var tool));
        var principal = ApiKeyActors.Create("caller", null, new HashSet<SereinFlowPermission>(), IsAdministrator: true);
        Probe first;
        using (var scope = provider.CreateScope())
        {
            first = scope.ServiceProvider.GetRequiredService<Probe>();
            var context = CreateCallContext(scope, principal);
            var response = Assert.IsType<EchoResponse>(await tool.ExecuteAsync(context,
                JsonSerializer.SerializeToElement(new { name = "one" }), CancellationToken.None));
            Assert.Equal(first.Id, response.ScopeId);
            Assert.Equal("one:caller", response.Text);
        }
        Assert.True(first.Disposed);
        using var secondScope = provider.CreateScope();
        var second = Assert.IsType<EchoResponse>(await tool!.ExecuteAsync(CreateCallContext(secondScope, principal),
            JsonSerializer.SerializeToElement(new { name = "two" }), CancellationToken.None));
        Assert.NotEqual(first.Id, second.ScopeId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":null}")]
    [InlineData("{\"name\":\"  \"}")]
    [InlineData("{\"name\":12}")]
    [InlineData("{\"name\":\"ok\",\"child\":{\"name\":\"\"}}")]
    [InlineData("{\"name\":\"ok\",\"children\":[{\"name\":\"\"}]}")]
    [InlineData("{\"name\":\"ok\",\"entries\":{\"first\":{\"name\":\"\"}}}")]
    public async Task InvalidRequestsAreRejectedBeforeHandlerActivation(string json)
    {
        var catalog = SereinFlowMcpHosting.CreateCatalog(new Generated.McpToolRegistry(), [typeof(EchoTools)]);
        Assert.True(catalog.TryGet("echo", out var tool));
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var document = JsonDocument.Parse(json);
        var context = new McpToolCallContext(scope.ServiceProvider);
        var error = await Assert.ThrowsAsync<McpParameterBindingException>(() => tool!.ExecuteAsync(context, document.RootElement, CancellationToken.None));
        Assert.NotNull(error.Path);
    }

    [Fact]
    public async Task CancellationAndBusinessExceptionsKeepTheirOriginalTypes()
    {
        var catalog = SereinFlowMcpHosting.CreateCatalog(new Generated.McpToolRegistry(), [typeof(FailureTools)]);
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = new McpToolCallContext(scope.ServiceProvider);
        Assert.True(catalog.TryGet("fail", out var fail));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fail!.ExecuteAsync(context, default, CancellationToken.None));
        Assert.True(catalog.TryGet("cancel", out var cancel));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancel!.ExecuteAsync(context, default, cancellation.Token));
    }

    [Fact]
    public void MissingContractsFailAtCatalogCreation()
    {
        var missing = new McpToolDescriptor("missing", "Missing", JsonSerializer.SerializeToElement(new { type = "object" }));
        Assert.Throws<InvalidOperationException>(() => SereinFlowMcpHosting.CreateCatalog(new Generated.McpToolRegistry(), [typeof(EchoTools)], contracts: [missing]));
    }

    [Fact]
    public async Task ExecutorStillEnforcesAuthorizationBeforeInvokingDiscoveredTool()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ApiKeyService(null!));
        services.AddTestAuthorization();
        services.AddSereinFlowMcpTools(new Generated.McpToolRegistry(), typeof(EchoTools));
        using var provider = services.BuildServiceProvider();
        var executor = new McpToolExecutor(provider.GetRequiredService<IServiceScopeFactory>());
        var catalog = SereinFlowMcpHosting.CreateCatalog(new Generated.McpToolRegistry(), [typeof(EchoTools)]);
        var error = await Assert.ThrowsAsync<McpProtocolException>(() => executor.ExecuteAsync(catalog, "echo",
            JsonSerializer.SerializeToElement(new { name = "one" }), CancellationToken.None));
        Assert.Contains("authentication", error.Message);
    }

    [Fact]
    public async Task ExecutorDisposesAsyncOnlyDependencies()
    {
        var state = new DisposalState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSingleton(new ApiKeyService(null!));
        services.AddScoped<AsyncProbe>();
        services.AddTestAuthorization();
        services.AddSereinFlowMcpTools(new Generated.McpToolRegistry(), typeof(AsyncTools));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        provider.GetRequiredService<ISereinFlowActorAccessor>().Current =
            ApiKeyActors.Create("local", null, new HashSet<SereinFlowPermission>(), IsAdministrator: true);
        var executor = new McpToolExecutor(provider.GetRequiredService<IServiceScopeFactory>());
        await executor.ExecuteAsync(SereinFlowMcpHosting.CreateCatalog(new Generated.McpToolRegistry(), [typeof(AsyncTools)]), "async-dispose", default, CancellationToken.None);
        Assert.True(state.Disposed);
    }

    private static McpToolCallContext CreateCallContext(IServiceScope scope, SereinFlowActor? principal)
    {
        var context = new McpToolCallContext(scope.ServiceProvider);
        context.SetValue(new SereinFlowMcpCaller(principal));
        return context;
    }

    public sealed class Probe : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    public sealed class EchoTools(Probe probe)
    {
        [SereinFlowMcpTool("echo", SereinFlowPermission.ProjectRead, Description = "Echo a name.")]
        public Task<EchoResponse> Echo(EchoRequest request, SereinFlowMcpCaller caller, CancellationToken cancellationToken)
            => Task.FromResult(new EchoResponse($"{request.Name}:{caller.RequirePrincipal().Id}", probe.Id));
    }

    public sealed record EchoRequest([property: Required] string Name, EchoChild? Child = null,
        IReadOnlyList<EchoChild>? Children = null, IReadOnlyDictionary<string, EchoChild>? Entries = null);
    public sealed record EchoChild([property: Required] string Name);
    public sealed record EchoResponse(string Text, Guid ScopeId);

    public sealed class DisposalState
    {
        public bool Disposed { get; set; }
    }

    public sealed class AsyncProbe(DisposalState state) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            state.Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class AsyncTools(AsyncProbe probe)
    {
        [SereinFlowMcpTool("async-dispose", SereinFlowPermission.ProjectRead, Description = "Use an async dependency.")]
        public Task<object> Run() => Task.FromResult<object>(probe.GetType().Name);
    }

    public static class FailureTools
    {
        [SereinFlowMcpTool("fail", SereinFlowPermission.ProjectRead, Description = "Fail.")]
        public static Task<object> Fail() => throw new InvalidOperationException("business failure");

        [SereinFlowMcpTool("cancel", SereinFlowPermission.ProjectRead, Description = "Cancel.")]
        public static Task<object> Cancel(CancellationToken cancellationToken) => Task.FromCanceled<object>(cancellationToken);
    }

}
