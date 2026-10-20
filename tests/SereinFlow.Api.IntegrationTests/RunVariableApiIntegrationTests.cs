using System.Collections.Generic;
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
        var evaluatedValue = (await client.GetAsync(path)).StatusCode;
        Assert.Equal(HttpStatusCode.Unauthorized, evaluatedValue);
        Assert.Equal(0, factory.Worker.Calls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        var write = await client.PutAsJsonAsync(path + "/batch.count", new { value = 42, valueType = "int32" });
        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
        var item = await write.Content.ReadFromJsonAsync<RunVariableDto>();
        Assert.Equal("System.Int32", item!.ClrType);
        var read = await client.GetFromJsonAsync<RunVariableDto>(path + "/batch.count");
        var getInt32Result = read!.Value!.Value.GetInt32();
        Assert.Equal(42, getInt32Result);
        var response = await client.GetAsync(path + "?offset=0&take=1");
        Assert.True(response.Headers.CacheControl!.NoStore);
        var page = await response.Content.ReadFromJsonAsync<RunVariableSnapshotDto>();
        Assert.Equal(1, page!.TotalCount);
        var evaluatedValue1 = Assert.Single(page.Variables).Name;
        Assert.Equal("batch.count", evaluatedValue1);
        var evaluatedValue2 = (await client.GetAsync(path + "/missing")).StatusCode;
        Assert.Equal(HttpStatusCode.NotFound, evaluatedValue2);
    }

    [Fact]
    public async Task InvalidInputAndCompletedRunsDoNotReachTheWorker()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        var path = $"/api/runs/{factory.Run.Id}/variables";
        var evaluatedValue3 = (await client.GetAsync(path + "?take=201")).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, evaluatedValue3);
        var evaluatedValue4 = (await client.PutAsJsonAsync(path + "/value", new { value = 1, valueType = "My.Type, My.Assembly" })).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, evaluatedValue4);
        var evaluatedValue5 = (await client.PutAsJsonAsync(path + "/value", new { valueType = "int32" })).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, evaluatedValue5);
        factory.Run.Cancel("test", DateTimeOffset.UtcNow);
        var evaluatedValue6 = (await client.GetAsync(path)).StatusCode;
        Assert.Equal(HttpStatusCode.Conflict, evaluatedValue6);
        Assert.Equal(0, factory.Worker.Calls);
    }

    [Fact]
    public async Task FinishedRunsReadSavedVariablesAndRejectWritesWithoutAWorker()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        IReadOnlyList<RunVariableDto> evaluatedValue8 = [resolveevaluatedValue(), new("image", "OpenCvSharp.Mat", false, null, "unsupported_type")];
        var snapshot = new RunVariableSnapshotDto(factory.Run.Id, evaluatedValue8, 2, 0, false, capturedAt, IsFinal: true);
        await factory.SaveFinalAsync(snapshot);
        var path = $"/api/runs/{factory.Run.Id}/variables";
        var first = await client.GetFromJsonAsync<RunVariableSnapshotDto>(path + "?take=1");
        Assert.True(first!.IsFinal);
        Assert.Equal(capturedAt, first.CapturedAt);
        Assert.True(first.HasMore);
        var evaluatedValue9 = Assert.Single(first.Variables).Name;
        Assert.Equal("count", evaluatedValue9);
        var second = await client.GetFromJsonAsync<RunVariableSnapshotDto>(path + "?offset=1&take=1");
        Assert.False(second!.HasMore);
        var evaluatedValue10 = Assert.Single(second.Variables).ClrType;
        Assert.Equal("OpenCvSharp.Mat", evaluatedValue10);
        var evaluatedValue11 = (await client.GetFromJsonAsync<RunVariableDto>(path + "/count"))!.Value!.Value;
        var getInt32Result12 = evaluatedValue11.GetInt32();
        Assert.Equal(11, getInt32Result12);
        var evaluatedValue13 = (await client.GetAsync(path + "/missing")).StatusCode;
        Assert.Equal(HttpStatusCode.NotFound, evaluatedValue13);
        var evaluatedValue14 = (await client.PutAsJsonAsync(path + "/count", new { value = 12, valueType = "int32" })).StatusCode;
        Assert.Equal(HttpStatusCode.Conflict, evaluatedValue14);
        Assert.Equal(0, factory.Worker.Calls);
        RunVariableDto resolveevaluatedValue()
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(11);
            RunVariableDto evaluatedValue7 = new("count", "System.Int32", true, serializeToElementResult, ValueJson: "11");
            return evaluatedValue7;
        }
    }

    [Fact]
    public async Task AWorkerCompletionRaceFallsBackToTheSavedEmptySnapshot()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Key);
        RunVariableSnapshotDto evaluatedValue15 = new(factory.Run.Id, [], 0, 0, false, DateTimeOffset.UtcNow, IsFinal: true);
        await factory.SaveFinalAsync(evaluatedValue15, completeRun: false);
        factory.Worker.Inactive = true;
        var snapshot = await client.GetFromJsonAsync<RunVariableSnapshotDto>($"/api/runs/{factory.Run.Id}/variables");
        Assert.True(snapshot!.IsFinal);
        Assert.Empty(snapshot.Variables);
        Assert.Equal(1, factory.Worker.Calls);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string Key = "variable-api-test-bootstrap-key";
        private readonly string _root = CreaterootInitialValue();
        public FlowRun Run { get; } = CreateRunInitialValue();
        public RecordingWorker Worker { get; } = new();
        public TestEventStore Events { get; } = new();

        public async Task SaveFinalAsync(RunVariableSnapshotDto snapshot, bool completeRun = true)
        {
            using var scope = Services.CreateScope();
            var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            IReadOnlyList<FlowRunEvent> evaluatedValue19 = [resolveevaluatedValue17()];
            await getRequiredServiceResult.AppendAsync(evaluatedValue19);
            if (completeRun)
            {
                Run.Complete(FlowRunStatus.Succeeded, DateTimeOffset.UtcNow);
            }

            FlowRunEvent resolveevaluatedValue17()
            {
                var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
                var serializeResult = JsonSerializer.Serialize(new { variables = snapshot }, createWebOptionsResult);
                FlowRunEvent evaluatedValue18 = new(Run.Id, 1, DateTimeOffset.UtcNow, RunErrorCodes.Completed, null, serializeResult);
                return evaluatedValue18;
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Run.MarkRunning(DateTimeOffset.UtcNow);
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                var dictionary = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _root,
                    ["SereinFlow:Mcp:BootstrapAdminKey"] = Key,
                    ["SereinFlow:Api:RequireAuthentication"] = "true",
                };
                configuration.AddInMemoryCollection(dictionary);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFlowRunStore>();
                var testRunStore = new TestRunStore(Run);
                services.AddSingleton<IFlowRunStore>(testRunStore);
                services.RemoveAll<IWorkerVariableRunClient>();
                services.AddSingleton<IWorkerVariableRunClient>(Worker);
                services.RemoveAll<IFlowRunEventStore>();
                services.AddSingleton<IFlowRunEventStore>(Events);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try
            {
                if (disposing && Directory.Exists(_root))
                {
                    Directory.Delete(_root, true);
                }
            }
            catch (IOException)
            { /* SQLite pooled handles may outlive the disposed test host. */
            }
        }

        private static string CreaterootInitialValue()
        {
            var getTempPathResult = Path.GetTempPath();
            var evaluatedValue16 = $"sereinflow-variable-api-{Guid.NewGuid():N}";
            string rootInitialValue = Path.Combine(getTempPathResult, evaluatedValue16);
            return rootInitialValue;
        }

        private static FlowRun CreateRunInitialValue()
        {
            var newGuidResult = Guid.NewGuid();
            var newGuidResult1 = Guid.NewGuid();
            FlowRun runInitialValue = FlowRun.Start(newGuidResult, newGuidResult1, 1, DateTimeOffset.UtcNow);
            return runInitialValue;
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
            if (Inactive)
            {
                var workerVariableResponseDto = new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 409, Code: WorkerErrorCodes.NotActive);
                var fromResultResult = Task.FromResult(workerVariableResponseDto);
                return fromResultResult;
            }

            if (request.Operation == "set")
            {
                _value = new(request.Name!, "System.Int32", true, request.Value);
            }

            if (request.Operation == "list")
            {
                RunVariableSnapshotDto evaluatedValue20 = new(request.RunId, _value is null ? [] : [_value], _value is null ? 0 : 1, request.Offset, false, DateTimeOffset.UtcNow);
                var workerVariableResponseDto21 = new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 200, Snapshot: evaluatedValue20);
                var fromResultResult22 = Task.FromResult(workerVariableResponseDto21);
                return fromResultResult22;
            }

            if (_value?.Name != request.Name)
            {
                var workerVariableResponseDto23 = new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 404, Code: VariableErrorCodes.NotFound);
                var fromResultResult24 = Task.FromResult(workerVariableResponseDto23);
                return fromResultResult24;
            }

            var workerVariableResponseDto25 = new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 200, Variable: _value);
            var fromResultResult26 = Task.FromResult(workerVariableResponseDto25);
            return fromResultResult26;
        }
    }

    private sealed class TestRunStore(FlowRun run) : IFlowRunStore
    {
        public Task<FlowRun?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult27 = Task.FromResult(id == run.Id ? run : null);
            return fromResultResult27;
        }

        public FlowRun? Find(Guid id) => id == run.Id ? run : null;
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
            var fromResultResult28 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult28;
        }

        // The fixture owns this run; exclude it from hosted startup orphan reconciliation.
        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult29 = Task.FromResult<IReadOnlyList<FlowRun>>([]);
            return fromResultResult29;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult30 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult30;
        }

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
        {
            var fromResultResult31 = Task.FromResult(false);
            return fromResultResult31;
        }

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
        {
            throw new NotSupportedException();
        }

        public FlowDefinitionDto? GetSnapshot(Guid id) => null;
        public bool Save(FlowRun value) => false;
    }

    private sealed class TestEventStore : IFlowRunEventStore
    {
        private readonly List<FlowRunEvent> _events = [];
        public Task AppendAsync(IReadOnlyList<FlowRunEvent> items, CancellationToken cancellationToken = default)
        {
            Append(items);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid id, long sequence, CancellationToken cancellationToken = default)
        {
            var getAfterResult = GetAfter(id, sequence);
            var fromResultResult32 = Task.FromResult(getAfterResult);
            return fromResultResult32;
        }

        public Task<long> GetLastSequenceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var maxResult = _events
                .Where(item => item.RunId == id)
                .Select(item => item.Sequence)
                .DefaultIfEmpty()
                .Max();
            var fromResultResult33 = Task.FromResult(maxResult);
            return fromResultResult33;
        }

        public void Append(IReadOnlyList<FlowRunEvent> items)
        {
            _events.AddRange(items);
        }

        public IReadOnlyList<FlowRunEvent> GetAfter(Guid id, long sequence)
        {
            var toArrayResult = _events
                .Where(item => item.RunId == id && item.Sequence > sequence)
                .ToArray();
            return toArrayResult;
        }
    }
}
