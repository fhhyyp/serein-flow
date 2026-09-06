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
        using var schema = JsonDocument.Parse(descriptor.InputSchema.GetRawText());
        var root = schema.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Contains("runId", root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("topic", root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("payload", root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("idempotencyKey", root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.False(root.GetProperty("properties").GetProperty("payload").TryGetProperty("type", out _));
        Assert.Equal(
            ChannelKindNames,
            root.GetProperty("properties").GetProperty("channelKind").GetProperty("enum")
                .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("run.message.publish", McpPermissionNames.ToName(McpPermissionDto.RunMessagePublish));
        Assert.True(McpPermissionNames.TryParse("run.message.publish", out var parsedPermission));
        Assert.Equal(McpPermissionDto.RunMessagePublish, parsedPermission);

        var tool = host.Services.GetRequiredService<McpToolCatalog>().Descriptors
            .Single(item => item.Name == "sereinflow_publish_run_message");
        Assert.Equal(descriptor.InputSchema.GetRawText(), tool.InputSchema.GetRawText());
    }

    [Fact]
    public async Task PublishUsesSharedServiceAndReplaysThePersistedResultWithoutRedelivery()
    {
        var run = CreateRunningRun();
        var delivery = new RecordingDeliveryService(run.Id);
        using var host = CreateHost(run, delivery);
        host.SetPrincipal(new McpPrincipal(
            "run-message-caller",
            run.ProjectId,
            new[] { McpPermissionDto.RunMessagePublish }.ToHashSet()));

        var arguments = JsonSerializer.SerializeToElement(new
        {
            runId = run.Id,
            topic = " orders.created ",
            payload = new object?[] { null, "订单", 42, true },
            channelKind = "EVENTBUS",
            contractId = "orders.v1",
            idempotencyKey = "publish-once",
        });
        var first = await host.Backend.CallToolAsync(
            "sereinflow_publish_run_message", arguments, CancellationToken.None);
        var replay = await host.Backend.CallToolAsync(
            "sereinflow_publish_run_message", arguments, CancellationToken.None);

        var firstResponse = Assert.IsType<WorkerMessageDeliveryResponseDto>(first.Value);
        var replayJson = Assert.IsType<JsonElement>(replay.Value);
        Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, firstResponse.Status);
        Assert.False(firstResponse.Duplicate);
        Assert.Equal(firstResponse.MessageId, replayJson.GetProperty("messageId").GetGuid());
        Assert.Equal(1, delivery.CallCount);
        Assert.Equal("orders.created", delivery.LastCommand!.Topic.Trim());
        Assert.Equal(WorkerMessageChannelKindDto.EventBus, delivery.LastCommand.ChannelKind);
        Assert.Equal(JsonValueKind.Array, delivery.LastCommand.Payload.ValueKind);
    }

    [Fact]
    public async Task ProjectScopedCallerCannotPublishToAnotherProjectsRun()
    {
        var run = CreateRunningRun();
        var delivery = new RecordingDeliveryService(run.Id);
        using var host = CreateHost(run, delivery);
        host.SetPrincipal(new McpPrincipal(
            "other-project-caller",
            Guid.NewGuid(),
            new[] { McpPermissionDto.RunMessagePublish }.ToHashSet()));

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => host.Backend.CallToolAsync(
            "sereinflow_publish_run_message",
            JsonSerializer.SerializeToElement(new
            {
                runId = run.Id,
                topic = "orders.created",
                payload = "value",
                idempotencyKey = "denied-publish",
            }),
            CancellationToken.None));

        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
        Assert.Equal(0, delivery.CallCount);
    }

    [Fact]
    public async Task MissingPayloadReturnsStableInvalidArgumentError()
    {
        var run = CreateRunningRun();
        var delivery = new RecordingDeliveryService(run.Id);
        using var host = CreateHost(run, delivery);
        host.SetPrincipal(new McpPrincipal(
            "run-message-caller",
            run.ProjectId,
            new[] { McpPermissionDto.RunMessagePublish }.ToHashSet()));

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => host.Backend.CallToolAsync(
            "sereinflow_publish_run_message",
            JsonSerializer.SerializeToElement(new
            {
                runId = run.Id,
                topic = "orders.created",
                idempotencyKey = "missing-payload",
            }),
            CancellationToken.None));

        Assert.Equal(McpProtocolErrorCodes.InvalidParams, exception.Code);
        var errorData = JsonSerializer.SerializeToElement(exception.ErrorData);
        Assert.Equal(MessageErrorCodes.PayloadRequired, errorData.GetProperty("code").GetString());
        Assert.Equal(0, delivery.CallCount);
    }

    [Fact]
    public async Task SharedServiceFailureIsConvertedToStableMcpErrorCode()
    {
        var missingRunId = Guid.NewGuid();
        var delivery = new RecordingDeliveryService(missingRunId)
        {
            ResultFactory = _ => new RunMessageDeliveryResult(
                RunMessageDeliveryDisposition.RunNotFound,
                null,
                RunErrorCodes.NotFound,
                "Run not found. 未找到运行实例。",
                404)
        };
        using var host = CreateHost(null, delivery);
        host.SetPrincipal(new McpPrincipal(
            "run-message-admin",
            null,
            new[] { McpPermissionDto.RunMessagePublish }.ToHashSet(),
            IsAdministrator: true));

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => host.Backend.CallToolAsync(
            "sereinflow_publish_run_message",
            JsonSerializer.SerializeToElement(new
            {
                runId = missingRunId,
                topic = "orders.created",
                payload = new { value = 1 },
                idempotencyKey = "missing-run",
            }),
            CancellationToken.None));

        Assert.Equal(McpProtocolErrorCodes.ResourceNotFound, exception.Code);
        var errorData = JsonSerializer.SerializeToElement(exception.ErrorData);
        Assert.Equal(RunErrorCodes.NotFound, errorData.GetProperty("code").GetString());
    }

    private static FlowRun CreateRunningRun()
    {
        var run = FlowRun.Start(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        run.MarkRunning(DateTimeOffset.UtcNow);
        return run;
    }

    private static TestHost CreateHost(
        FlowRun? run = null,
        RecordingDeliveryService? delivery = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sereinflow-mcp-message-test-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SereinFlow:DataRoot"] = root })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowStorage(configuration, root);
        services.AddSereinFlowApplication();
        services.AddSereinFlowMcp(configuration);
        if (run is not null)
            services.AddSingleton<IFlowRunStore>(new TestRunStore(run));
        if (delivery is not null)
            services.AddSingleton<IRunMessageDeliveryService>(delivery);
        services.AddSingleton<SereinFlowMcpBackend>(provider =>
            (SereinFlowMcpBackend)provider.GetRequiredService<ISereinFlowMcpBackend>());
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return new TestHost(provider, root);
    }

    private sealed class TestHost(IServiceProvider services, string root) : IDisposable
    {
        public IServiceProvider Services { get; } = services;
        public string Root { get; } = root;
        public SereinFlowMcpBackend Backend => Services.GetRequiredService<SereinFlowMcpBackend>();

        public void SetPrincipal(McpPrincipal principal)
            => Services.GetRequiredService<IMcpPrincipalAccessor>().Current = principal;

        public void Dispose()
        {
            if (Services is IDisposable disposable)
                disposable.Dispose();
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

        public Task<RunMessageDeliveryResult> DeliverAsync(
            RunMessageDeliveryCommand command,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedRunId, command.RunId);
            CallCount++;
            LastCommand = command;
            var response = new WorkerMessageDeliveryResponseDto(
                WorkerMessageDeliveryStatusDto.Accepted,
                1,
                command.RunId,
                Guid.NewGuid(),
                command.Topic.Trim(),
                null,
                null);
            return Task.FromResult(ResultFactory?.Invoke(command) ?? new RunMessageDeliveryResult(
                RunMessageDeliveryDisposition.Accepted,
                response,
                null,
                "The message was accepted. 消息已接受。",
                202));
        }
    }

    private sealed class TestRunStore(FlowRun run) : IFlowRunStore
    {
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowRun>>([run]);

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowRun?>(run.Id == runId ? run : null);

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowDefinitionDto?>(null);

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
            => throw new NotSupportedException();

        public FlowRun? Find(Guid runId)
            => run.Id == runId ? run : null;

        public FlowDefinitionDto? GetSnapshot(Guid runId)
            => null;

        public bool Save(FlowRun value)
            => false;
    }
}
