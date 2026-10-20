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
        var selectResult = catalog.Descriptors.Select(static item => item.Name);
        var names = selectResult.ToHashSet(StringComparer.Ordinal);
        Assert.Contains("sereinflow_list_runs", names);
        Assert.Contains("sereinflow_list_debug_sessions", names);
        Assert.Contains("sereinflow_start_debug_session", names);
        Assert.Contains("sereinflow_continue_debug", names);
        Assert.Contains("sereinflow_step_debug", names);
        Assert.Contains("sereinflow_stop_debug", names);
        var tryGetResult = catalog.TryGet("sereinflow_start_debug_session", out var start);
        Assert.True(tryGetResult);
        Assert.NotNull(start);
        Assert.True(start!.RequiresIdempotencyKey);
        Assert.Equal(McpToolExecutionKind.Mutation, start.ExecutionKind);
        var getPropertyResult = start.Descriptor.InputSchema.GetProperty("required");
        var enumerateArrayResult = getPropertyResult.EnumerateArray();
        var selectResult2 = enumerateArrayResult.Select(static item =>
        {
            var evaluatedValue = item.GetString()!;
            return evaluatedValue;
        });
        var startRequired = selectResult2.ToHashSet(StringComparer.Ordinal);
        var toHashSetResult = new[]
        {
            "projectId",
            "flowId",
            "idempotencyKey"
        }.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(toHashSetResult, startRequired);
        var step = catalog.Descriptors.Single(item => item.Name == "sereinflow_step_debug");
        var getPropertyResult3 = step.InputSchema.GetProperty("required");
        var enumerateArrayResult4 = getPropertyResult3.EnumerateArray();
        var selectResult5 = enumerateArrayResult4.Select(static item =>
        {
            var evaluatedValue1 = item.GetString()!;
            return evaluatedValue1;
        });
        var stepRequired = selectResult5.ToHashSet(StringComparer.Ordinal);
        var toHashSetResult6 = new[]
        {
            "sessionId",
            "commandSequence"
        }.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(toHashSetResult6, stepRequired);
        var getPropertyResult7 = step.InputSchema.GetProperty("properties");
        var getPropertyResult8 = getPropertyResult7.GetProperty("commandSequence");
        var getPropertyResult9 = getPropertyResult8.GetProperty("type");
        var getStringResult = getPropertyResult9.GetString();
        Assert.Equal("integer", getStringResult);
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
            resolveCreateResult(),
            resolveCreateResult11()
        };
        var store = new InMemoryDebugSessionStore();
        using var host = CreateHost(services =>
        {
            services.AddSingleton<IFlowDebugSessionStore>(store);
        });
        await store.CreateAsync(sessions[0]);
        await store.CreateAsync(sessions[1]);
        var toHashSetResult15 = new[]
        {
            McpPermissionDto.DebugRead
        }.ToHashSet();
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("project-debug-reader", firstProjectId, toHashSetResult15);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult16 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue17 = (await backend.CallToolAsync("sereinflow_list_debug_sessions", serializeToElementResult16, CancellationToken.None)).Value;
        var page = Deserialize<AiPageDto<AiDebugSessionSummaryDto>>(evaluatedValue17);
        var item = Assert.Single(page.Items);
        Assert.Equal(firstProjectId, item.ProjectId);
        var otherProjectDenied = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(new { projectId = secondProjectId });
            var callToolAsyncResult = backend.CallToolAsync("sereinflow_list_debug_sessions", serializeToElementResult, CancellationToken.None);
            return callToolAsyncResult;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, otherProjectDenied.Code);
        var toHashSetResult18 = new[]
        {
            McpPermissionDto.RunRead
        }.ToHashSet();
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("run-reader", firstProjectId, toHashSetResult18);
        var serializeToElementResult19 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue20 = (await backend.CallToolAsync("sereinflow_list_debug_sessions", serializeToElementResult19, CancellationToken.None)).Value;
        var runPermissionPage = Deserialize<AiPageDto<AiDebugSessionSummaryDto>>(evaluatedValue20);
        var evaluatedValue21 = Assert.Single(runPermissionPage.Items).ProjectId;
        Assert.Equal(firstProjectId, evaluatedValue21);
        var evaluatedValue22 = (await backend.ReadResourceAsync("sereinflow://debug-sessions", CancellationToken.None)).Value;
        var resource = Deserialize<AiPageDto<AiDebugSessionSummaryDto>>(evaluatedValue22);
        var evaluatedValue23 = Assert.Single(resource.Items).ProjectId;
        Assert.Equal(firstProjectId, evaluatedValue23);
        global::SereinFlow.Domain.FlowDebugSession resolveCreateResult()
        {
            var newGuidResult = Guid.NewGuid();
            var newGuidResult10 = Guid.NewGuid();
            var createResult = FlowDebugSession.Create(newGuidResult, firstProjectId, newGuidResult10, ["node-a"], DateTimeOffset.UtcNow);
            return createResult;
        }

        global::SereinFlow.Domain.FlowDebugSession resolveCreateResult11()
        {
            var newGuidResult12 = Guid.NewGuid();
            var newGuidResult13 = Guid.NewGuid();
            var createResult14 = FlowDebugSession.Create(newGuidResult12, secondProjectId, newGuidResult13, ["node-b"], DateTimeOffset.UtcNow);
            return createResult14;
        }
    }

    [Fact]
    public async Task DebugStartIsIdempotentAndConflictingPayloadsAreRejected()
    {
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var fake = new TestDebugSessionService(projectId, flowId);
        using var host = CreateHost(services =>
        {
            services.AddSingleton<IFlowDebugSessionService>(fake);
        });
        var toHashSetResult26 = new[]
        {
            McpPermissionDto.DebugControl
        }.ToHashSet();
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("debug-admin", null, toHashSetResult26, IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var arguments = JsonSerializer.SerializeToElement(new { projectId, flowId, breakpointNodeIds = new[] { "node-a" }, idempotencyKey = "debug-start-once" });
        var evaluatedValue27 = (await backend.CallToolAsync("sereinflow_start_debug_session", arguments, CancellationToken.None)).Value;
        var first = Deserialize<McpDebugSessionStartedDto>(evaluatedValue27);
        var evaluatedValue28 = (await backend.CallToolAsync("sereinflow_start_debug_session", arguments, CancellationToken.None)).Value;
        var replay = Deserialize<McpDebugSessionStartedDto>(evaluatedValue28);
        Assert.Equal(first.SessionId, replay.SessionId);
        Assert.Equal(1, fake.CreateCalls);
        var conflict = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult24 = JsonSerializer.SerializeToElement(new { projectId, flowId, breakpointNodeIds = new[] { "node-b" }, idempotencyKey = "debug-start-once" });
            var callToolAsyncResult25 = backend.CallToolAsync("sereinflow_start_debug_session", serializeToElementResult24, CancellationToken.None);
            return callToolAsyncResult25;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, conflict.Code);
    }

    [Fact]
    public async Task DebugControlChecksProjectScopeAndCommandSequence()
    {
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var fake = new TestDebugSessionService(projectId, flowId);
        using var host = CreateHost(services =>
        {
            services.AddSingleton<IFlowDebugSessionService>(fake);
        });
        var newGuidResult31 = Guid.NewGuid();
        var toHashSetResult32 = new[]
        {
            McpPermissionDto.DebugControl
        }.ToHashSet();
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("wrong-project-debug-controller", newGuidResult31, toHashSetResult32);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var arguments = JsonSerializer.SerializeToElement(new { sessionId = fake.Session.Id, commandSequence = 1L });
        var denied = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var callToolAsyncResult29 = backend.CallToolAsync("sereinflow_continue_debug", arguments, CancellationToken.None);
            return callToolAsyncResult29;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, denied.Code);
        var toHashSetResult33 = new[]
        {
            McpPermissionDto.DebugControl
        }.ToHashSet();
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("correct-project-debug-controller", projectId, toHashSetResult33);
        var evaluatedValue34 = (await backend.CallToolAsync("sereinflow_continue_debug", arguments, CancellationToken.None)).Value;
        var accepted = Deserialize<McpDebugCommandAcceptedDto>(evaluatedValue34);
        Assert.True(accepted.Accepted);
        Assert.Equal(1, accepted.CommandSequence);
        var duplicate = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var callToolAsyncResult30 = backend.CallToolAsync("sereinflow_continue_debug", arguments, CancellationToken.None);
            return callToolAsyncResult30;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, duplicate.Code);
    }

    private static T Deserialize<T>(object? value)
    {
        var evaluatedValue35 = resolveDeserializeResult()!;
        return evaluatedValue35;
        T? resolveDeserializeResult()
        {
            var serializeResult = JsonSerializer.Serialize(value, JsonOptions);
            var deserializeResult = JsonSerializer.Deserialize<T>(serializeResult, JsonOptions);
            return deserializeResult;
        }
    }

    private static TestHost CreateHost(Action<IServiceCollection>? configureServices = null)
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue37 = $"sereinflow-mcp-debug-test-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult, evaluatedValue37);
        var configurationBuilder = new ConfigurationBuilder();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = root,
        };
        var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection(dictionary);
        var configuration = addInMemoryCollectionResult.Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowStorage(configuration, root);
        services.AddSereinFlowApplication();
        services.AddSereinFlowMcp(configuration);
        configureServices?.Invoke(services);
        services.AddSingleton<SereinFlowMcpBackend>(provider =>
        {
            var evaluatedValue36 = (SereinFlowMcpBackend)provider.GetRequiredService<ISereinFlowMcpBackend>();
            return evaluatedValue36;
        });
        var serviceProviderOptions = new ServiceProviderOptions
        {
            ValidateScopes = true
        };
        var buildServiceProviderResult = services.BuildServiceProvider(serviceProviderOptions);
        var testHost = new TestHost(buildServiceProviderResult, root);
        return testHost;
    }

    private sealed class TestDebugSessionService : IFlowDebugSessionService
    {
        public TestDebugSessionService(Guid projectId, Guid flowId)
        {
            var newGuidResult38 = Guid.NewGuid();
            Session = FlowDebugSession.Create(newGuidResult38, projectId, flowId, ["node-a"], DateTimeOffset.UtcNow);
        }

        public FlowDebugSession Session { get; }
        public int CreateCalls { get; private set; }

        public Task<FlowDebugSessionStartResult> CreateAsync(Guid projectId, Guid flowId, StartFlowDebugSessionRequestDto request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            var flowDebugSessionStartResult = new FlowDebugSessionStartResult(Session, 202);
            var fromResultResult = Task.FromResult(flowDebugSessionStartResult);
            return fromResultResult;
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var fromResultResult39 = Task.FromResult<FlowDebugSession?>(sessionId == Session.Id ? Session : null);
            return fromResultResult39;
        }

        public Task<FlowDebugStateWaitResult> WaitForChangeAsync(Guid sessionId, long afterRevision, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var flowDebugStateWaitResult = new FlowDebugStateWaitResult(Session, false, false);
            var fromResultResult40 = Task.FromResult(flowDebugStateWaitResult);
            return fromResultResult40;
        }

        public Task<FlowDebugSessionCommandResult> ContinueAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
        {
            var acceptCommandResult = AcceptCommand(commandSequence);
            return acceptCommandResult;
        }

        public Task<FlowDebugSessionCommandResult> StepAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
        {
            var acceptCommandResult41 = AcceptCommand(commandSequence);
            return acceptCommandResult41;
        }

        public Task<FlowDebugSessionCommandResult> StopAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default)
        {
            var acceptCommandResult42 = AcceptCommand(commandSequence);
            return acceptCommandResult42;
        }

        private Task<FlowDebugSessionCommandResult> AcceptCommand(long commandSequence)
        {
            try
            {
                Session.AcceptCommand(commandSequence, DateTimeOffset.UtcNow);
                var flowDebugSessionCommandResult = new FlowDebugSessionCommandResult(202);
                var fromResultResult43 = Task.FromResult(flowDebugSessionCommandResult);
                return fromResultResult43;
            }
            catch (InvalidOperationException exception)
            {
                var flowDebugSessionCommandResult44 = new FlowDebugSessionCommandResult(409, exception.Message, DebugErrorCodes.CommandSequenceConflict);
                var fromResultResult45 = Task.FromResult(flowDebugSessionCommandResult44);
                return fromResultResult45;
            }
        }
    }

    private sealed class InMemoryDebugSessionStore : IFlowDebugSessionStore
    {
        private readonly List<FlowDebugSession> _sessions = [];
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
        {
            _sessions.Add(session);
            var fromResultResult46 = Task.FromResult(session);
            return fromResultResult46;
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var singleOrDefaultResult = _sessions.SingleOrDefault(item => item.Id == sessionId);
            var fromResultResult47 = Task.FromResult(singleOrDefaultResult);
            return fromResultResult47;
        }

        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var singleOrDefaultResult48 = _sessions.SingleOrDefault(item => item.RunId == runId);
            var fromResultResult49 = Task.FromResult(singleOrDefaultResult48);
            return fromResultResult49;
        }

        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            var whereResult = _sessions.Where(item => !item.IsTerminal);
            var toArrayResult = whereResult.ToArray();
            var fromResultResult50 = Task.FromResult<IReadOnlyList<FlowDebugSession>>(toArrayResult);
            return fromResultResult50;
        }

        public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
        {
            var anyResult = _sessions.Any(item => item.Id == session.Id);
            var fromResultResult51 = Task.FromResult(anyResult);
            return fromResultResult51;
        }
    }

    private sealed class TestHost(ServiceProvider provider, string root) : IDisposable
    {
        public IServiceProvider Services => provider;

        public void Dispose()
        {
            provider.Dispose();
            if (!Directory.Exists(root))
            {
                return;
            }

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
