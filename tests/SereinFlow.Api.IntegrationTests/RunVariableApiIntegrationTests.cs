using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api.IntegrationTests;

public sealed class RunVariableApiIntegrationTests
{
    [Fact]
    public async Task RoutesAuthorizeAndReadWritePagedValuesFromTheWorker()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var path = $"/api/runs/{factory.Run.Id}/variables";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, factory.Worker.Calls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        var write = await client.PutAsJsonAsync(path + "/batch.count", new { value = 42, valueType = "int32" });
        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
        var item = await write.Content.ReadFromJsonAsync<RunVariableDto>();
        Assert.Equal("System.Int32", item!.ClrType);
        var read = await client.GetFromJsonAsync<RunVariableDto>(path + "/batch.count");
        Assert.Equal(42, read!.Value!.Value.GetInt32());
        var response = await client.GetAsync(path + "?offset=0&take=1");
        Assert.True(response.Headers.CacheControl!.NoStore);
        var page = await response.Content.ReadFromJsonAsync<RunVariableSnapshotDto>();
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal("batch.count", Assert.Single(page.Variables).Name);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/missing")).StatusCode);
    }

    [Fact]
    public async Task InvalidInputAndCompletedRunsDoNotReachTheWorker()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        var path = $"/api/runs/{factory.Run.Id}/variables";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?take=201")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path + "/value", new { value = 1, valueType = "My.Type, My.Assembly" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path + "/value", new { valueType = "int32" })).StatusCode);
        factory.Run.Cancel("test", DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, factory.Worker.Calls);
    }

    [Fact]
    public async Task FinishedRunsReadSavedVariablesAndRejectWritesWithoutAWorker()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var snapshot = new RunVariableSnapshotDto(factory.Run.Id,
            [new("count", "System.Int32", true, JsonSerializer.SerializeToElement(11), ValueJson: "11"),
             new("image", "OpenCvSharp.Mat", false, null, "unsupported_type")], 2, 0, false, capturedAt, IsFinal: true);
        await factory.SaveFinalAsync(snapshot);
        var path = $"/api/runs/{factory.Run.Id}/variables";
        var first = await client.GetFromJsonAsync<RunVariableSnapshotDto>(path + "?take=1");
        Assert.True(first!.IsFinal);
        Assert.Equal(capturedAt, first.CapturedAt);
        Assert.True(first.HasMore);
        Assert.Equal("count", Assert.Single(first.Variables).Name);
        var second = await client.GetFromJsonAsync<RunVariableSnapshotDto>(path + "?offset=1&take=1");
        Assert.False(second!.HasMore);
        Assert.Equal("OpenCvSharp.Mat", Assert.Single(second.Variables).ClrType);
        Assert.Equal(11, (await client.GetFromJsonAsync<RunVariableDto>(path + "/count"))!.Value!.Value.GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path + "/count", new { value = 12, valueType = "int32" })).StatusCode);
        Assert.Equal(0, factory.Worker.Calls);
    }

    [Fact]
    public async Task AWorkerCompletionRaceFallsBackToTheSavedEmptySnapshot()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        await factory.SaveFinalAsync(new(factory.Run.Id, [], 0, 0, false, DateTimeOffset.UtcNow, IsFinal: true), completeRun: false);
        factory.Worker.Inactive = true;
        var snapshot = await client.GetFromJsonAsync<RunVariableSnapshotDto>($"/api/runs/{factory.Run.Id}/variables");
        Assert.True(snapshot!.IsFinal);
        Assert.Empty(snapshot.Variables);
        Assert.Equal(1, factory.Worker.Calls);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string Key = "variable-api-test-bootstrap-key";
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"sereinflow-variable-api-{Guid.NewGuid():N}");
        public FlowRun Run { get; } = FlowRun.Start(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        public RecordingWorker Worker { get; } = new();
        public TestEventStore Events { get; } = new();
        public async Task SaveFinalAsync(RunVariableSnapshotDto snapshot, bool completeRun = true)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>().AppendAsync(
                [new(Run.Id, 1, DateTimeOffset.UtcNow, RunErrorCodes.Completed, null,
                    JsonSerializer.Serialize(new { variables = snapshot }, SereinJsonSerialization.CreateWebOptions()))]);
            if (completeRun) Run.Complete(FlowRunStatus.Succeeded, DateTimeOffset.UtcNow);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Run.MarkRunning(DateTimeOffset.UtcNow);
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _root,
                    ["SereinFlow:Mcp:BootstrapAdminKey"] = Key,
                    ["SereinFlow:Api:RequireAuthentication"] = "true",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFlowRunStore>();
                services.AddSingleton<IFlowRunStore>(new TestRunStore(Run));
                services.RemoveAll<IWorkerVariableRunClient>();
                services.AddSingleton<IWorkerVariableRunClient>(Worker);
                services.RemoveAll<IFlowRunEventStore>();
                services.AddSingleton<IFlowRunEventStore>(Events);
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (disposing && Directory.Exists(_root)) Directory.Delete(_root, true); }
            catch (IOException) { /* SQLite pooled handles may outlive the disposed test host. */ }
        }
    }

    private sealed class RecordingWorker : IWorkerVariableRunClient
    {
        public int Calls { get; private set; }
        public bool Inactive { get; set; }
        private RunVariableDto? _value;
        public Task<WorkerVariableResponseDto> AccessVariablesAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Inactive) return Task.FromResult(new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 409, Code: WorkerErrorCodes.NotActive));
            if (request.Operation == "set") _value = new(request.Name!, "System.Int32", true, request.Value);
            if (request.Operation == "list") return Task.FromResult(new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 200,
                Snapshot: new(request.RunId, _value is null ? [] : [_value], _value is null ? 0 : 1, request.Offset, false, DateTimeOffset.UtcNow)));
            if (_value?.Name != request.Name) return Task.FromResult(new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 404, Code: VariableErrorCodes.NotFound));
            return Task.FromResult(new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 200, Variable: _value));
        }
    }

    private sealed class TestRunStore(FlowRun run) : IFlowRunStore
    {
        public Task<FlowRun?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == run.Id ? run : null);
        public FlowRun? Find(Guid id) => id == run.Id ? run : null;
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
        // The fixture owns this run; exclude it from hosted startup orphan reconciliation.
        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowRun>>([]);
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<FlowDefinitionDto?>(null);
        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow) => throw new NotSupportedException();
        public FlowDefinitionDto? GetSnapshot(Guid id) => null;
        public bool Save(FlowRun value) => false;
    }

    private sealed class TestEventStore : IFlowRunEventStore
    {
        private readonly List<FlowRunEvent> _events = [];
        public Task AppendAsync(IReadOnlyList<FlowRunEvent> items, CancellationToken cancellationToken = default) { Append(items); return Task.CompletedTask; }
        public Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid id, long sequence, CancellationToken cancellationToken = default) => Task.FromResult(GetAfter(id, sequence));
        public Task<long> GetLastSequenceAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_events.Where(item => item.RunId == id).Select(item => item.Sequence).DefaultIfEmpty().Max());
        public void Append(IReadOnlyList<FlowRunEvent> items) => _events.AddRange(items);
        public IReadOnlyList<FlowRunEvent> GetAfter(Guid id, long sequence) => _events.Where(item => item.RunId == id && item.Sequence > sequence).ToArray();
    }
}
