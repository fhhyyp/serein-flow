using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Api.IntegrationTests;

public sealed class RunMessageApiIntegrationTests
{
    [Fact]
    public async Task PublicRouteBindsArbitraryJsonAndReturnsAcceptedWithoutApiAuthentication()
    {
        var run = CreateRunningRun();
        Assert.Equal(FlowRunStatus.Running, run.Status);
        var worker = new RecordingWorkerClient();
        using var factory = new ApiFactory(run, worker);
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{\"payload\":[null,\"订单\",42,true],\"contractId\":\"order.v1\",\"channelKind\":\"eventBus\"}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            $"/api/runs/{run.Id:D}/messages/ orders.created ",
            content);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Accepted,
            $"{responseBody}; deliveries={worker.Deliveries.Count}; runStatus={run.Status}");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(worker.Deliveries);
        Assert.Equal("orders.created", worker.Deliveries[0].Topic);
        Assert.Equal(WorkerMessageChannelKindDto.EventBus, worker.Deliveries[0].ChannelKind);
        Assert.Equal("[null,\"订单\",42,true]", worker.Deliveries[0].PayloadJson);

        using var responseDocument = JsonDocument.Parse(responseBody);
        Assert.Equal("accepted", responseDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(run.Id, responseDocument.RootElement.GetProperty("runId").GetGuid());
    }

    private static FlowRun CreateRunningRun()
    {
        var run = FlowRun.Start(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        run.MarkRunning(DateTimeOffset.UtcNow);
        return run;
    }

    private sealed class ApiFactory(FlowRun run, RecordingWorkerClient worker) : WebApplicationFactory<Program>
    {
        private readonly string _dataRoot = Path.Combine(
            Path.GetTempPath(),
            $"sereinflow-run-message-api-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.UseSetting("SereinFlow:DataRoot", _dataRoot);
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _dataRoot,
                    ["SereinFlow:ApiDocumentation:Enabled"] = "true",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFlowRunStore>();
                services.AddSingleton<IFlowRunStore>(new TestRunStore(run));
                services.RemoveAll<IWorkerMessageRunClient>();
                services.AddSingleton<IWorkerMessageRunClient>(worker);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_dataRoot))
            {
                try
                {
                    Directory.Delete(_dataRoot, recursive: true);
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

    private sealed class RecordingWorkerClient : IWorkerMessageRunClient
    {
        public List<WorkerMessageDeliveryDto> Deliveries { get; } = [];

        public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(
            WorkerMessageDeliveryDto delivery,
            CancellationToken cancellationToken = default)
        {
            Deliveries.Add(delivery);
            return Task.FromResult(new WorkerMessageDeliveryResponseDto(
                WorkerMessageDeliveryStatusDto.Accepted,
                WorkerProtocol.Version,
                delivery.RunId,
                delivery.MessageId,
                delivery.Topic,
                null,
                null));
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
            => Task.FromResult<IReadOnlyList<FlowRun>>([]);

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
