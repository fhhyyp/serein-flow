using System;
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

public sealed class McpRunVariableToolTests
{
    [Fact]
    public async Task ToolsReadValuesAndReplayWritesWithoutCallingTheWorkerAgain()
    {
        using var host = new TestHost();
        host.SetPrincipal(host.Run.ProjectId, McpPermissionDto.RunRead, McpPermissionDto.RunVariableWrite);
        var arguments = JsonSerializer.SerializeToElement(new { runId = host.Run.Id, name = " count ", value = 42, valueType = "int32", idempotencyKey = "write-1" });
        var first = await host.Backend.CallToolAsync("sereinflow_set_run_variable", arguments, CancellationToken.None);
        var replay = await host.Backend.CallToolAsync("sereinflow_set_run_variable", arguments, CancellationToken.None);
        Assert.Equal(1, host.Variables.CallCount);
        var evaluatedValue = Assert.IsType<RunVariableDto>(first.Value).Name;
        Assert.Equal(" count ", evaluatedValue);
        var isTypeResult = Assert.IsType<JsonElement>(replay.Value);
        var getPropertyResult = isTypeResult.GetProperty("value");
        var getInt32Result = getPropertyResult.GetInt32();
        Assert.Equal(42, getInt32Result);
        var serializeToElementResult = JsonSerializer.SerializeToElement(new { runId = host.Run.Id, name = " count " });
        var read = await host.Backend.CallToolAsync("sereinflow_get_run_variables", serializeToElementResult, CancellationToken.None);
        var evaluatedValue1 = Assert.IsType<RunVariableDto>(read.Value).Value!.Value;
        var getInt32Result2 = evaluatedValue1.GetInt32();
        Assert.Equal(42, getInt32Result2);
        var evaluatedValue3 = (await host.Backend.ListToolsAsync(CancellationToken.None));
        var schema = evaluatedValue3.Single(tool => tool.Name == "sereinflow_set_run_variable").InputSchema;
        var getPropertyResult4 = schema.GetProperty("required");
        var selectResult = getPropertyResult4.EnumerateArray()
            .Select(item =>
            {
                var getStringResult = item.GetString();
                return getStringResult;
            });
        Assert.Contains("valueType", selectResult);
        var toNameResult = McpPermissionNames.ToName(McpPermissionDto.RunVariableWrite);
        Assert.Equal("run.variable.write", toNameResult);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task WrongProjectOrMissingWritePermissionCannotReachVariables(bool wrongProject, bool readOnly)
    {
        using var host = new TestHost();
        Guid evaluatedValue6 = wrongProject ? Guid.NewGuid() : host.Run.ProjectId;
        host.SetPrincipal(evaluatedValue6, readOnly ? McpPermissionDto.RunRead : McpPermissionDto.RunVariableWrite);
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult5 = JsonSerializer.SerializeToElement(new { runId = host.Run.Id, name = "count", value = 1, valueType = "int32", idempotencyKey = "denied" });
            var callToolAsyncResult = host.Backend.CallToolAsync("sereinflow_set_run_variable", serializeToElementResult5, CancellationToken.None);
            return callToolAsyncResult;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
        Assert.Equal(0, host.Variables.CallCount);
    }

    [Fact]
    public async Task ReadsAreProjectScopedAndInactiveRunsReturnTheWorkerError()
    {
        using var host = new TestHost();
        var newGuidResult = Guid.NewGuid();
        host.SetPrincipal(newGuidResult, McpPermissionDto.RunRead);
        await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult7 = JsonSerializer.SerializeToElement(new { runId = host.Run.Id });
            var callToolAsyncResult8 = host.Backend.CallToolAsync("sereinflow_get_run_variables", serializeToElementResult7, CancellationToken.None);
            return callToolAsyncResult8;
        });
        Assert.Equal(0, host.Variables.CallCount);
        host.SetPrincipal(host.Run.ProjectId, McpPermissionDto.RunRead);
        host.Variables.Inactive = true;
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult9 = JsonSerializer.SerializeToElement(new { runId = host.Run.Id });
            var callToolAsyncResult10 = host.Backend.CallToolAsync("sereinflow_get_run_variables", serializeToElementResult9, CancellationToken.None);
            return callToolAsyncResult10;
        });
        var serializeToElementResult11 = JsonSerializer.SerializeToElement(exception.ErrorData);
        var getPropertyResult12 = serializeToElementResult11.GetProperty("code");
        var getStringResult13 = getPropertyResult12.GetString();
        Assert.Equal(WorkerErrorCodes.NotActive, getStringResult13);
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string _root = createrootInitialValue();
        private readonly ServiceProvider _services;
        public FlowRun Run { get; } = createRunInitialValue();
        public RecordingVariables Variables { get; } = new();

        public SereinFlowMcpBackend Backend
        {
            get
            {
                var evaluatedValue15 = (SereinFlowMcpBackend)_services.GetRequiredService<ISereinFlowMcpBackend>();
                return evaluatedValue15;
            }
        }

        public TestHost()
        {
            Run.MarkRunning(DateTimeOffset.UtcNow);
            var configurationBuilder = new ConfigurationBuilder();
            var dictionary = new Dictionary<string, string?>
            {
                ["SereinFlow:DataRoot"] = _root
            };
            var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection(dictionary);
            var configuration = addInMemoryCollectionResult.Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSereinFlowStorage(configuration, _root);
            services.AddSereinFlowApplication();
            services.AddSereinFlowMcp(configuration);
            var testRunStore = new TestRunStore(Run);
            services.AddSingleton<IFlowRunStore>(testRunStore);
            services.AddSingleton<IRunVariableService>(Variables);
            var serviceProviderOptions = new ServiceProviderOptions
            {
                ValidateScopes = true
            };
            _services = services.BuildServiceProvider(serviceProviderOptions);
        }

        public void SetPrincipal(Guid projectId, params McpPermissionDto[] permissions)
        {
            var toHashSetResult = permissions.ToHashSet();
            _services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("caller", projectId, toHashSetResult);
        }

        public void Dispose()
        {
            _services.Dispose();
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, true);
                }
            }
            catch (IOException)
            { /* SQLite pooled handles may outlive the disposed test host. */
            }
        }

        private static string createrootInitialValue()
        {
            var getTempPathResult = Path.GetTempPath();
            var evaluatedValue14 = $"sereinflow-variable-mcp-{Guid.NewGuid():N}";
            string rootInitialValue = Path.Combine(getTempPathResult, evaluatedValue14);
            return rootInitialValue;
        }

        private static FlowRun createRunInitialValue()
        {
            var newGuidResult1 = Guid.NewGuid();
            var newGuidResult2 = Guid.NewGuid();
            FlowRun runInitialValue = FlowRun.Start(newGuidResult1, newGuidResult2, 1, DateTimeOffset.UtcNow);
            return runInitialValue;
        }
    }

    private sealed class RecordingVariables : IRunVariableService
    {
        public int CallCount { get; private set; }
        public bool Inactive { get; set; }

        private RunVariableDto? _variable;
        public Task<WorkerVariableResponseDto> AccessAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Inactive)
            {
                var workerVariableResponseDto = new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 409, Code: WorkerErrorCodes.NotActive);
                var fromResultResult = Task.FromResult(workerVariableResponseDto);
                return fromResultResult;
            }

            if (request.Operation == "set")
            {
                _variable = new(request.Name!, "System.Int32", true, request.Value);
            }

            var workerVariableResponseDto16 = new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 200, Variable: _variable);
            var fromResultResult17 = Task.FromResult(workerVariableResponseDto16);
            return fromResultResult17;
        }
    }

    private sealed class TestRunStore(FlowRun run) : IFlowRunStore
    {
        public Task<FlowRun?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult18 = Task.FromResult(id == run.Id ? run : null);
            return fromResultResult18;
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
            var fromResultResult19 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult19;
        }

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult20 = Task.FromResult<IReadOnlyList<FlowRun>>([run]);
            return fromResultResult20;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult21 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult21;
        }

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
        {
            var fromResultResult22 = Task.FromResult(false);
            return fromResultResult22;
        }

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
        {
            throw new NotSupportedException();
        }

        public FlowDefinitionDto? GetSnapshot(Guid id) => null;
        public bool Save(FlowRun value) => false;
    }
}
