using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpRunMessageToolTests
{
    private static readonly string[] ChannelKindNames = ["queue", "eventBus"];
    [Fact]
    public async Task CatalogDeclaresArbitraryPayloadMutationWithRequiredIdempotencyKey()
    {
        using var host = CreateHost();
        var descriptor = (await host.Backend.ListToolsAsync(CancellationToken.None))
            .Single(item => item.Name == "sereinflow_publish_run_message");
        var getRawTextResult = descriptor.InputSchema.GetRawText();
        using var schema = JsonDocument.Parse(getRawTextResult);
        var root = schema.RootElement;
        var getPropertyResult = root.GetProperty("type");
        var getStringResult5 = getPropertyResult.GetString();
        Assert.Equal("object", getStringResult5);
        var getPropertyResult6 = root.GetProperty("required");
        var selectResult = getPropertyResult6.EnumerateArray()
            .Select(item =>
            {
                var getStringResult = item.GetString();
                return getStringResult;
            });
        Assert.Contains("runId", selectResult);
        var getPropertyResult7 = root.GetProperty("required");
        var selectResult9 = getPropertyResult7.EnumerateArray()
            .Select(item =>
            {
                var getStringResult1 = item.GetString();
                return getStringResult1;
            });
        Assert.Contains("topic", selectResult9);
        var getPropertyResult10 = root.GetProperty("required");
        var selectResult12 = getPropertyResult10.EnumerateArray()
            .Select(item =>
            {
                var getStringResult2 = item.GetString();
                return getStringResult2;
            });
        Assert.Contains("payload", selectResult12);
        var getPropertyResult13 = root.GetProperty("required");
        var selectResult15 = getPropertyResult13.EnumerateArray()
            .Select(item =>
            {
                var getStringResult3 = item.GetString();
                return getStringResult3;
            });
        Assert.Contains("idempotencyKey", selectResult15);
        var getPropertyResult16 = root.GetProperty("properties");
        var getPropertyResult17 = getPropertyResult16.GetProperty("payload");
        var tryGetPropertyResult = getPropertyResult17.TryGetProperty("type", out _);
        Assert.False(tryGetPropertyResult);
        var getPropertyResult18 = root.GetProperty("properties");
        var getPropertyResult19 = getPropertyResult18.GetProperty("channelKind");
        var getPropertyResult20 = getPropertyResult19.GetProperty("enum");
        var selectResult22 = getPropertyResult20.EnumerateArray()
            .Select(item =>
            {
                var getStringResult4 = item.GetString();
                return getStringResult4;
            });
        Assert.Equal(ChannelKindNames, selectResult22);
        var toNameResult = McpPermissionNames.ToName(McpPermissionDto.RunMessagePublish);
        Assert.Equal("run.message.publish", toNameResult);
        var tryParseResult = McpPermissionNames.TryParse("run.message.publish", out var parsedPermission);
        Assert.True(tryParseResult);
        Assert.Equal(McpPermissionDto.RunMessagePublish, parsedPermission);
        var evaluatedValue23 = host.Services.GetRequiredService<McpToolCatalog>().Descriptors;
        var tool = evaluatedValue23.Single(item => item.Name == "sereinflow_publish_run_message");
        var getRawTextResult24 = descriptor.InputSchema.GetRawText();
        var getRawTextResult25 = tool.InputSchema.GetRawText();
        Assert.Equal(getRawTextResult24, getRawTextResult25);
    }

    [Fact]
    public async Task PublishUsesSharedServiceAndReplaysThePersistedResultWithoutRedelivery()
    {
        var run = CreateRunningRun();
        var delivery = new RecordingDeliveryService(run.Id);
        using var host = CreateHost(run, delivery);
        var toHashSetResult = new[]
        {
            McpPermissionDto.RunMessagePublish
        }.ToHashSet();
        var mcpPrincipal = new McpPrincipal("run-message-caller", run.ProjectId, toHashSetResult);
        host.SetPrincipal(mcpPrincipal);
        var arguments = JsonSerializer.SerializeToElement(new { runId = run.Id, topic = " orders.created ", payload = new object?[] { null, "订单", 42, true }, channelKind = "EVENTBUS", contractId = "orders.v1", idempotencyKey = "publish-once", });
        var first = await host.Backend.CallToolAsync("sereinflow_publish_run_message", arguments, CancellationToken.None);
        var replay = await host.Backend.CallToolAsync("sereinflow_publish_run_message", arguments, CancellationToken.None);
        var firstResponse = Assert.IsType<WorkerMessageDeliveryResponseDto>(first.Value);
        var replayJson = Assert.IsType<JsonElement>(replay.Value);
        Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, firstResponse.Status);
        Assert.False(firstResponse.Duplicate);
        var getPropertyResult26 = replayJson.GetProperty("messageId");
        var getGuidResult = getPropertyResult26.GetGuid();
        Assert.Equal(firstResponse.MessageId, getGuidResult);
        Assert.Equal(1, delivery.CallCount);
        var trimResult = delivery.LastCommand!.Topic.Trim();
        Assert.Equal("orders.created", trimResult);
        Assert.Equal(WorkerMessageChannelKindDto.EventBus, delivery.LastCommand.ChannelKind);
        Assert.Equal(JsonValueKind.Array, delivery.LastCommand.Payload.ValueKind);
    }

    [Fact]
    public async Task ProjectScopedCallerCannotPublishToAnotherProjectsRun()
    {
        var run = CreateRunningRun();
        var delivery = new RecordingDeliveryService(run.Id);
        using var host = CreateHost(run, delivery);
        var newGuidResult = Guid.NewGuid();
        var toHashSetResult27 = new[]
        {
            McpPermissionDto.RunMessagePublish
        }.ToHashSet();
        var mcpPrincipal28 = new McpPrincipal("other-project-caller", newGuidResult, toHashSetResult27);
        host.SetPrincipal(mcpPrincipal28);
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(new { runId = run.Id, topic = "orders.created", payload = "value", idempotencyKey = "denied-publish", });
            var callToolAsyncResult = host.Backend.CallToolAsync("sereinflow_publish_run_message", serializeToElementResult, CancellationToken.None);
            return callToolAsyncResult;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
        Assert.Equal(0, delivery.CallCount);
    }

    [Fact]
    public async Task MissingPayloadReturnsStableInvalidArgumentError()
    {
        var run = CreateRunningRun();
        var delivery = new RecordingDeliveryService(run.Id);
        using var host = CreateHost(run, delivery);
        var toHashSetResult31 = new[]
        {
            McpPermissionDto.RunMessagePublish
        }.ToHashSet();
        var mcpPrincipal32 = new McpPrincipal("run-message-caller", run.ProjectId, toHashSetResult31);
        host.SetPrincipal(mcpPrincipal32);
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult29 = JsonSerializer.SerializeToElement(new { runId = run.Id, topic = "orders.created", idempotencyKey = "missing-payload", });
            var callToolAsyncResult30 = host.Backend.CallToolAsync("sereinflow_publish_run_message", serializeToElementResult29, CancellationToken.None);
            return callToolAsyncResult30;
        });
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, exception.Code);
        var errorData = JsonSerializer.SerializeToElement(exception.ErrorData);
        var getPropertyResult33 = errorData.GetProperty("code");
        var getStringResult34 = getPropertyResult33.GetString();
        Assert.Equal(MessageErrorCodes.PayloadRequired, getStringResult34);
        Assert.Equal(0, delivery.CallCount);
    }

    [Fact]
    public async Task SharedServiceFailureIsConvertedToStableMcpErrorCode()
    {
        var missingRunId = Guid.NewGuid();
        var delivery = new RecordingDeliveryService(missingRunId)
        {
            ResultFactory = _ =>
            {
                var runMessageDeliveryResult = new RunMessageDeliveryResult(RunMessageDeliveryDisposition.RunNotFound, null, RunErrorCodes.NotFound, "Run not found. 未找到运行实例。", 404);
                return runMessageDeliveryResult;
            }
        };
        using var host = CreateHost(null, delivery);
        var toHashSetResult37 = new[]
        {
            McpPermissionDto.RunMessagePublish
        }.ToHashSet();
        var mcpPrincipal38 = new McpPrincipal("run-message-admin", null, toHashSetResult37, IsAdministrator: true);
        host.SetPrincipal(mcpPrincipal38);
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult35 = JsonSerializer.SerializeToElement(new { runId = missingRunId, topic = "orders.created", payload = new { value = 1 }, idempotencyKey = "missing-run", });
            var callToolAsyncResult36 = host.Backend.CallToolAsync("sereinflow_publish_run_message", serializeToElementResult35, CancellationToken.None);
            return callToolAsyncResult36;
        });
        Assert.Equal(McpProtocolErrorCodes.ResourceNotFound, exception.Code);
        var errorData = JsonSerializer.SerializeToElement(exception.ErrorData);
        var getPropertyResult39 = errorData.GetProperty("code");
        var getStringResult40 = getPropertyResult39.GetString();
        Assert.Equal(RunErrorCodes.NotFound, getStringResult40);
    }

    private static FlowRun CreateRunningRun()
    {
        var newGuidResult41 = Guid.NewGuid();
        var newGuidResult42 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult41, newGuidResult42, 1, DateTimeOffset.UtcNow);
        run.MarkRunning(DateTimeOffset.UtcNow);
        return run;
    }

    private static TestHost CreateHost(FlowRun? run = null, RecordingDeliveryService? delivery = null)
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue44 = $"sereinflow-mcp-message-test-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult, evaluatedValue44);
        var configurationBuilder = new ConfigurationBuilder();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = root
        };
        var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection(dictionary);
        var configuration = addInMemoryCollectionResult.Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowStorage(configuration, root);
        services.AddSereinFlowApplication();
        services.AddSereinFlowMcp(configuration);
        if (run is not null)
        {
            var testRunStore = new TestRunStore(run);
            services.AddSingleton<IFlowRunStore>(testRunStore);
        }

        if (delivery is not null)
        {
            services.AddSingleton<IRunMessageDeliveryService>(delivery);
        }

        services.AddSingleton<SereinFlowMcpBackend>(provider =>
        {
            var evaluatedValue43 = (SereinFlowMcpBackend)provider.GetRequiredService<ISereinFlowMcpBackend>();
            return evaluatedValue43;
        });
        var serviceProviderOptions = new ServiceProviderOptions
        {
            ValidateScopes = true
        };
        var provider = services.BuildServiceProvider(serviceProviderOptions);
        var testHost = new TestHost(provider, root);
        return testHost;
    }

    private sealed class TestHost(IServiceProvider services, string root) : IDisposable
    {
        public IServiceProvider Services { get; } = services;
        public string Root { get; } = root;

        public SereinFlowMcpBackend Backend
        {
            get
            {
                var getRequiredServiceResult = Services.GetRequiredService<SereinFlowMcpBackend>();
                return getRequiredServiceResult;
            }
        }

        public void SetPrincipal(McpPrincipal principal)
        {
            Services.GetRequiredService<IMcpPrincipalAccessor>().Current = principal;
        }

        public void Dispose()
        {
            if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (Directory.Exists(Root))
            {
                try
                {
                    Directory.Delete(Root, recursive: true);
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

    private sealed class RecordingDeliveryService(Guid expectedRunId) : IRunMessageDeliveryService
    {
        public int CallCount { get; private set; }
        public RunMessageDeliveryCommand? LastCommand { get; private set; }
        public Func<RunMessageDeliveryCommand, RunMessageDeliveryResult>? ResultFactory { get; init; }

        public Task<RunMessageDeliveryResult> DeliverAsync(RunMessageDeliveryCommand command, CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedRunId, command.RunId);
            CallCount++;
            LastCommand = command;
            var newGuidResult45 = Guid.NewGuid();
            var trimResult46 = command.Topic.Trim();
            var response = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Accepted, 1, command.RunId, newGuidResult45, trimResult46, null, null);
            var evaluatedValue47 = ResultFactory?.Invoke(command) ?? new RunMessageDeliveryResult(RunMessageDeliveryDisposition.Accepted, response, null, "The message was accepted. 消息已接受。", 202);
            var fromResultResult = Task.FromResult(evaluatedValue47);
            return fromResultResult;
        }
    }

    private sealed class TestRunStore(FlowRun run) : IFlowRunStore
    {
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult48 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult48;
        }

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult49 = Task.FromResult<IReadOnlyList<FlowRun>>([run]);
            return fromResultResult49;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult50 = Task.FromResult<FlowRun?>(run.Id == runId ? run : null);
            return fromResultResult50;
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult51 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult51;
        }

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
        {
            var fromResultResult52 = Task.FromResult(false);
            return fromResultResult52;
        }

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
        {
            throw new NotSupportedException();
        }

        public FlowRun? Find(Guid runId) => run.Id == runId ? run : null;
        public FlowDefinitionDto? GetSnapshot(Guid runId) => null;
        public bool Save(FlowRun value) => false;
    }
}
