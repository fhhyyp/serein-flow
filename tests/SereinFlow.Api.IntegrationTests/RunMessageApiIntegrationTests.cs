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
    public async Task AuthenticatedRouteBindsArbitraryJsonAndReturnsAccepted()
    {
        var run = CreateRunningRun();
        Assert.Equal(FlowRunStatus.Running, run.Status);
        var worker = new RecordingWorkerClient();
        using var factory = new ApiFactory(run, worker);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "message-test-key");
        using var content = new StringContent("{\"payload\":[null,\"订单\",42,true],\"contractId\":\"order.v1\",\"channelKind\":\"eventBus\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/api/runs/{run.Id:D}/messages/ orders.created ", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"{responseBody}; deliveries={worker.Deliveries.Count}; runStatus={run.Status}");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(worker.Deliveries);
        Assert.Equal("orders.created", worker.Deliveries[0].Topic);
        Assert.Equal(WorkerMessageChannelKindDto.EventBus, worker.Deliveries[0].ChannelKind);
        Assert.Equal("[null,\"订单\",42,true]", worker.Deliveries[0].PayloadJson);
        using var responseDocument = JsonDocument.Parse(responseBody);
        var getPropertyResult = responseDocument.RootElement.GetProperty("status");
        var getStringResult = getPropertyResult.GetString();
        Assert.Equal("accepted", getStringResult);
        var getPropertyResult1 = responseDocument.RootElement.GetProperty("runId");
        var getGuidResult = getPropertyResult1.GetGuid();
        Assert.Equal(run.Id, getGuidResult);
    }

    private static FlowRun CreateRunningRun()
    {
        var newGuidResult = Guid.NewGuid();
        var newGuidResult2 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult, newGuidResult2, 1, DateTimeOffset.UtcNow);
        run.MarkRunning(DateTimeOffset.UtcNow);
        return run;
    }

    private sealed class ApiFactory(FlowRun run, RecordingWorkerClient worker) : WebApplicationFactory<Program>
    {
        private readonly string _dataRoot = CreatedataRootInitialValue();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("SereinFlow:Security:Mode", "ApiKey");
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.UseSetting("SereinFlow:DataRoot", _dataRoot);
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                var dictionary = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _dataRoot,
                    ["SereinFlow:Security:Mode"] = "ApiKey",
                    ["SereinFlow:Security:ApiKeys:BootstrapAdminKey"] = "message-test-key",
                    ["SereinFlow:ApiDocumentation:Enabled"] = "true",
                };
                configuration.AddInMemoryCollection(dictionary);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFlowRunStore>();
                var testRunStore = new TestRunStore(run);
                services.AddSingleton<IFlowRunStore>(testRunStore);
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

        private static string CreatedataRootInitialValue()
        {
            var getTempPathResult = Path.GetTempPath();
            var evaluatedValue = $"sereinflow-run-message-api-{Guid.NewGuid():N}";
            string dataRootInitialValue = Path.Combine(getTempPathResult, evaluatedValue);
            return dataRootInitialValue;
        }
    }

    private sealed class RecordingWorkerClient : IWorkerMessageRunClient
    {
        public List<WorkerMessageDeliveryDto> Deliveries { get; } = [];

        public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(WorkerMessageDeliveryDto delivery, CancellationToken cancellationToken = default)
        {
            Deliveries.Add(delivery);
            var workerMessageDeliveryResponseDto = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Accepted, WorkerProtocol.Version, delivery.RunId, delivery.MessageId, delivery.Topic, null, null);
            var fromResultResult = Task.FromResult(workerMessageDeliveryResponseDto);
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
            var fromResultResult3 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult3;
        }

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult4 = Task.FromResult<IReadOnlyList<FlowRun>>([]);
            return fromResultResult4;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult5 = Task.FromResult<FlowRun?>(run.Id == runId ? run : null);
            return fromResultResult5;
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult6 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult6;
        }

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
        {
            var fromResultResult7 = Task.FromResult(false);
            return fromResultResult7;
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
