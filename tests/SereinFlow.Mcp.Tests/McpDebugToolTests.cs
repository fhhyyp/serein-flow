using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpDebugToolTests
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();

    [Fact]
    public async Task CatalogExposesTheCompleteDebugLoopAndCollectionResources()
    {
        using var host = CreateHost();
        var catalog = host.Services.GetRequiredService<McpToolCatalog>();
        var names = catalog.Descriptors.Select(static item => item.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("sereinflow_list_runs", names);
        Assert.Contains("sereinflow_list_debug_sessions", names);
        Assert.Contains("sereinflow_start_debug_session", names);
        Assert.Contains("sereinflow_continue_debug", names);
        Assert.Contains("sereinflow_step_debug", names);
        Assert.Contains("sereinflow_stop_debug", names);

        Assert.True(catalog.TryGet("sereinflow_start_debug_session", out var start));
        Assert.NotNull(start);
        Assert.True(start!.RequiresIdempotencyKey);
        Assert.Equal(McpToolExecutionKind.Mutation, start.ExecutionKind);
        var startRequired = start.Descriptor.InputSchema
            .GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[] { "projectId", "flowId", "idempotencyKey" }.ToHashSet(StringComparer.Ordinal),
            startRequired);

        var step = catalog.Descriptors.Single(item => item.Name == "sereinflow_step_debug");
        var stepRequired = step.InputSchema
            .GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[] { "sessionId", "commandSequence" }.ToHashSet(StringComparer.Ordinal),
            stepRequired);
        Assert.Equal("integer", step.InputSchema.GetProperty("properties").GetProperty("commandSequence").GetProperty("type").GetString());

        var resources = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var listedResources = await resources.ListResourcesAsync(CancellationToken.None);
        Assert.Contains(listedResources, item => item.Uri == "sereinflow://runs");
        Assert.Contains(listedResources, item => item.Uri == "sereinflow://debug-sessions");
        var templates = await resources.ListResourceTemplatesAsync(CancellationToken.None);
        Assert.DoesNotContain(templates, item => item.UriTemplate == "sereinflow://runs");
        Assert.DoesNotContain(templates, item => item.UriTemplate == "sereinflow://debug-sessions");
    }

    [Fact]
    public async Task ActiveDebugSessionDiscoveryIsProjectScoped()
    {
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var sessions = new[]
        {
            FlowDebugSession.Create(
                Guid.NewGuid(), firstProjectId, Guid.NewGuid(), ["node-a"], DateTimeOffset.UtcNow),
            FlowDebugSession.Create(
                Guid.NewGuid(), secondProjectId, Guid.NewGuid(), ["node-b"], DateTimeOffset.UtcNow)
        };
        var store = new InMemoryDebugSessionStore();
        using var host = CreateHost(services => services.AddSingleton<IFlowDebugSessionStore>(store));
        await store.CreateAsync(sessions[0]);
        await store.CreateAsync(sessions[1]);

        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal(
            "project-debug-reader",
            firstProjectId,
            new[] { McpPermissionDto.DebugRead }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var page = Deserialize<AiPageDto<AiDebugSessionSummaryDto>>(
            (await backend.CallToolAsync(
                "sereinflow_list_debug_sessions",
                JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None)).Value);

        var item = Assert.Single(page.Items);
        Assert.Equal(firstProjectId, item.ProjectId);

        var otherProjectDenied = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_list_debug_sessions",
            JsonSerializer.SerializeToElement(new { projectId = secondProjectId }),
            CancellationToken.None));
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, otherProjectDenied.Code);

        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal(
            "run-reader",
            firstProjectId,
            new[] { McpPermissionDto.RunRead }.ToHashSet());
        var runPermissionPage = Deserialize<AiPageDto<AiDebugSessionSummaryDto>>(
            (await backend.CallToolAsync(
                "sereinflow_list_debug_sessions",
                JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None)).Value);
        Assert.Equal(firstProjectId, Assert.Single(runPermissionPage.Items).ProjectId);

        var resource = Deserialize<AiPageDto<AiDebugSessionSummaryDto>>(
            (await backend.ReadResourceAsync("sereinflow://debug-sessions", CancellationToken.None)).Value);
        Assert.Equal(firstProjectId, Assert.Single(resource.Items).ProjectId);
    }

    [Fact]
    public async Task DebugStartIsIdempotentAndConflictingPayloadsAreRejected()
    {
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var fake = new TestDebugSessionService(projectId, flowId);
        using var host = CreateHost(services => services.AddSingleton<IFlowDebugSessionService>(fake));
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal(
            "debug-admin",
            null,
            new[] { McpPermissionDto.DebugControl }.ToHashSet(),
            IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var arguments = JsonSerializer.SerializeToElement(new
        {
            projectId,
            flowId,
            breakpointNodeIds = new[] { "node-a" },
            idempotencyKey = "debug-start-once"
        });

        var first = Deserialize<McpDebugSessionStartedDto>(
            (await backend.CallToolAsync("sereinflow_start_debug_session", arguments, CancellationToken.None)).Value);
        var replay = Deserialize<McpDebugSessionStartedDto>(
            (await backend.CallToolAsync("sereinflow_start_debug_session", arguments, CancellationToken.None)).Value);

        Assert.Equal(first.SessionId, replay.SessionId);
        Assert.Equal(1, fake.CreateCalls);

        var conflict = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_start_debug_session",
            JsonSerializer.SerializeToElement(new
            {
                projectId,
                flowId,
                breakpointNodeIds = new[] { "node-b" },
                idempotencyKey = "debug-start-once"
            }),
            CancellationToken.None));
        Assert.Equal(McpProtocolErrorCodes.Conflict, conflict.Code);
    }

    [Fact]
    public async Task DebugControlChecksProjectScopeAndCommandSequence()
    {
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var fake = new TestDebugSessionService(projectId, flowId);
        using var host = CreateHost(services => services.AddSingleton<IFlowDebugSessionService>(fake));
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal(
            "wrong-project-debug-controller",
            Guid.NewGuid(),
            new[] { McpPermissionDto.DebugControl }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var arguments = JsonSerializer.SerializeToElement(new
        {
            sessionId = fake.Session.Id,
            commandSequence = 1L
        });

        var denied = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_continue_debug", arguments, CancellationToken.None));
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, denied.Code);

        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal(
            "correct-project-debug-controller",
            projectId,
            new[] { McpPermissionDto.DebugControl }.ToHashSet());
        var accepted = Deserialize<McpDebugCommandAcceptedDto>(
            (await backend.CallToolAsync("sereinflow_continue_debug", arguments, CancellationToken.None)).Value);
        Assert.True(accepted.Accepted);
        Assert.Equal(1, accepted.CommandSequence);

        var duplicate = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_continue_debug", arguments, CancellationToken.None));
        Assert.Equal(McpProtocolErrorCodes.Conflict, duplicate.Code);
    }

    private static T Deserialize<T>(object? value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;

    private static TestHost CreateHost(Action<IServiceCollection>? configureServices = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sereinflow-mcp-debug-test-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SereinFlow:DataRoot"] = root,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowStorage(configuration, root);
        services.AddSereinFlowApplication();
        services.AddSereinFlowMcp(configuration);
        configureServices?.Invoke(services);
        services.AddSingleton<SereinFlowMcpBackend>(provider =>
            (SereinFlowMcpBackend)provider.GetRequiredService<ISereinFlowMcpBackend>());
        return new TestHost(
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }),
            root);
    }

    private sealed class TestDebugSessionService : IFlowDebugSessionService
    {
        public TestDebugSessionService(Guid projectId, Guid flowId)
        {
            Session = FlowDebugSession.Create(Guid.NewGuid(), projectId, flowId, ["node-a"], DateTimeOffset.UtcNow);
        }

        public FlowDebugSession Session { get; }

        public int CreateCalls { get; private set; }

        public Task<FlowDebugSessionStartResult> CreateAsync(
            Guid projectId,
            Guid flowId,
            StartFlowDebugSessionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new FlowDebugSessionStartResult(Session, 202));
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowDebugSession?>(sessionId == Session.Id ? Session : null);

        public Task<FlowDebugStateWaitResult> WaitForChangeAsync(
            Guid sessionId,
            long afterRevision,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new FlowDebugStateWaitResult(Session, false, false));

        public Task<FlowDebugSessionCommandResult> ContinueAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
            => AcceptCommand(commandSequence);

        public Task<FlowDebugSessionCommandResult> StepAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
            => AcceptCommand(commandSequence);

        public Task<FlowDebugSessionCommandResult> StopAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
            => AcceptCommand(commandSequence);

        private Task<FlowDebugSessionCommandResult> AcceptCommand(long commandSequence)
        {
            try
            {
                Session.AcceptCommand(commandSequence, DateTimeOffset.UtcNow);
                return Task.FromResult(new FlowDebugSessionCommandResult(202));
            }
            catch (InvalidOperationException exception)
            {
                return Task.FromResult(new FlowDebugSessionCommandResult(409, exception.Message, DebugErrorCodes.CommandSequenceConflict));
            }
        }
    }

    private sealed class InMemoryDebugSessionStore : IFlowDebugSessionStore
    {
        private readonly List<FlowDebugSession> _sessions = [];

        public Task<FlowDebugSession> CreateAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
        {
            _sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_sessions.SingleOrDefault(item => item.Id == sessionId));

        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult(_sessions.SingleOrDefault(item => item.RunId == runId));

        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowDebugSession>>(_sessions.Where(item => !item.IsTerminal).ToArray());

        public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
            => Task.FromResult(_sessions.Any(item => item.Id == session.Id));
    }

    private sealed class TestHost(ServiceProvider provider, string root) : IDisposable
    {
        public IServiceProvider Services => provider;

        public void Dispose()
        {
            provider.Dispose();
            if (!Directory.Exists(root))
                return;
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
