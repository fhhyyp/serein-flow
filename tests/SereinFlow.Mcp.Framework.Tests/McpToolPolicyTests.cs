using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework.Tests;

public sealed class McpToolPolicyTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"idempotencyKey\":null}")]
    [InlineData("{\"idempotencyKey\":123}")]
    [InlineData("{\"idempotencyKey\":\"  \"}")]
    public async Task MutationGateRejectsInvalidKeys(string json)
    {
        var gate = new McpToolMutationGate();
        using var document = JsonDocument.Parse(json);
        var exception = await Assert.ThrowsAsync<McpParameterBindingException>(() => gate.AcquireAsync(Tool("write"), document.RootElement, default));
        Assert.Equal("MCP parameter 'idempotencyKey' is required.", exception.Message);
        Assert.True(exception.SuppressErrorData);
    }

    [Fact]
    public async Task CancelledWaiterDoesNotReleaseTheHolderOrBlockOtherTools()
    {
        var gate = new McpToolMutationGate();
        var arguments = JsonSerializer.SerializeToElement(new { idempotencyKey = "once" });
        using var holder = await gate.AcquireAsync(Tool("write"), arguments, default);
        using var cancellation = new CancellationTokenSource();
        var waiter = gate.AcquireAsync(Tool("write"), arguments, cancellation.Token);
        Assert.False(waiter.IsCompleted);
        using var other = await gate.AcquireAsync(Tool("other"), arguments, default);
        Assert.NotNull(other);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        var following = gate.AcquireAsync(Tool("write"), arguments, default);
        Assert.False(following.IsCompleted);
        holder!.Dispose();
        holder.Dispose();
        using var acquired = await following.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(acquired);
    }

    [Fact]
    public async Task FailedMutationReleasesItsLeaseAndRecordsOncePerCallInSeparateAsyncScopes()
    {
        var state = new State();
        await using var provider = Provider(state);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new InvalidOperationException("first mutation failed");
        var calls = 0;
        var tool = Tool("write", async () =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await release.Task;
                throw error;
            }
            return "second";
        });
        var catalog = new McpToolCatalog([tool]);
        var executor = provider.GetRequiredService<McpToolExecutor>();
        var arguments = JsonSerializer.SerializeToElement(new { idempotencyKey = "once" });
        var first = executor.ExecuteAsync(catalog, "write", arguments);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = executor.ExecuteAsync(catalog, "write", arguments);
        Assert.Equal(1, Volatile.Read(ref calls));
        release.SetResult();
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => first));
        Assert.Equal("second", await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, state.Completions.Count);
        Assert.Single(state.Completions, item => item.Outcome == "failed");
        Assert.Single(state.Completions, item => item.Outcome == "succeeded");
        Assert.All(state.Completions, item => Assert.True(item.Duration >= TimeSpan.Zero));
        Assert.Equal(4, state.Probes.Count);
        Assert.All(state.Probes, probe => Assert.True(probe.Disposed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordingFailureDoesNotReplaceTheResultOrOriginalException(bool failTool)
    {
        var state = new State { FailRecording = true };
        await using var provider = Provider(state);
        var error = new InvalidOperationException("original tool failure");
        var tool = Tool("write", () => failTool ? Task.FromException<object?>(error) : Task.FromResult<object?>("ok"));
        var call = provider.GetRequiredService<McpToolExecutor>().ExecuteAsync(new McpToolCatalog([tool]), "write",
            JsonSerializer.SerializeToElement(new { idempotencyKey = "once" }));
        if (failTool) Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => call));
        else Assert.Equal("ok", await call);
        Assert.Single(state.Completions);
        Assert.All(state.Probes, probe => Assert.True(probe.Disposed));
    }

    [Fact]
    public async Task AuthorizationPrecedesKeyBindingAndFailureIsMappedBeforeRecording()
    {
        var state = new State { Deny = true };
        await using var provider = Provider(state);
        var invoked = false;
        var tool = Tool("write", () => { invoked = true; return Task.FromResult<object?>("unexpected"); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => provider.GetRequiredService<McpToolExecutor>()
            .ExecuteAsync(new McpToolCatalog([tool]), "write", default));
        Assert.False(invoked);
        var completion = Assert.Single(state.Completions);
        Assert.Equal("denied", completion.Outcome);
        Assert.Equal("denied_by_host", completion.Summary);
        Assert.IsType<UnauthorizedAccessException>(completion.Failure!.Exception);
    }

    private static McpToolDefinition Tool(string name, Func<Task<object?>>? call = null)
        => new(new McpToolDescriptor(name, "Write.", JsonSerializer.SerializeToElement(new { type = "object" })),
            new McpToolAttribute(name) { ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true },
            (_, _, _) => call?.Invoke() ?? Task.FromResult<object?>("ok"));

    private static ServiceProvider Provider(State state)
    {
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<Probe>();
        services.AddSingleton<IMcpToolMiddleware, Policy>();
        services.AddMcpToolHandlers(new Generated.McpToolRegistry());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public sealed class State
    {
        public bool FailRecording { get; init; }
        public bool Deny { get; init; }
        public ConcurrentQueue<McpToolCompletion> Completions { get; } = new();
        public ConcurrentBag<Probe> Probes { get; } = [];
    }

    public sealed class Probe(State state) : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public void Register() => state.Probes.Add(this);
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    public sealed class Policy(State state, McpToolMutationGate gate) : McpToolPolicy(gate)
    {
        protected override ValueTask AuthorizeAsync(McpToolExecutionContext context, CancellationToken cancellationToken)
        {
            if (state.Deny) throw new InvalidOperationException("deny");
            var probe = context.CallContext.Services.GetRequiredService<Probe>();
            probe.Register();
            context.CallContext.SetValue(probe);
            return ValueTask.CompletedTask;
        }

        protected override McpToolFailure MapFailure(McpToolExecutionContext context, Exception exception)
            => state.Deny ? new(new UnauthorizedAccessException("Denied by host.", exception), "denied", "denied_by_host")
                : base.MapFailure(context, exception);

        protected override Task RecordAsync(IServiceProvider services, McpToolCompletion completion)
        {
            var probe = services.GetRequiredService<Probe>();
            probe.Register();
            if (completion.Context.CallContext.TryGetValue<Probe>(out var callProbe)) Assert.NotSame(callProbe, probe);
            state.Completions.Enqueue(completion);
            return state.FailRecording ? Task.FromException(new InvalidOperationException("audit failed")) : Task.CompletedTask;
        }
    }
}
