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
        var arguments = JsonSerializer.SerializeToElement(new
        {
            runId = host.Run.Id, name = " count ", value = 42, valueType = "int32", idempotencyKey = "write-1"
        });
        var first = await host.Backend.CallToolAsync("sereinflow_set_run_variable", arguments, CancellationToken.None);
        var replay = await host.Backend.CallToolAsync("sereinflow_set_run_variable", arguments, CancellationToken.None);
        Assert.Equal(1, host.Variables.CallCount);
        Assert.Equal(" count ", Assert.IsType<RunVariableDto>(first.Value).Name);
        Assert.Equal(42, Assert.IsType<JsonElement>(replay.Value).GetProperty("value").GetInt32());
        var read = await host.Backend.CallToolAsync("sereinflow_get_run_variables",
            JsonSerializer.SerializeToElement(new { runId = host.Run.Id, name = " count " }), CancellationToken.None);
        Assert.Equal(42, Assert.IsType<RunVariableDto>(read.Value).Value!.Value.GetInt32());
        var schema = (await host.Backend.ListToolsAsync(CancellationToken.None))
            .Single(tool => tool.Name == "sereinflow_set_run_variable").InputSchema;
        Assert.Contains("valueType", schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("run.variable.write", McpPermissionNames.ToName(McpPermissionDto.RunVariableWrite));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task WrongProjectOrMissingWritePermissionCannotReachVariables(bool wrongProject, bool readOnly)
    {
        using var host = new TestHost();
        host.SetPrincipal(wrongProject ? Guid.NewGuid() : host.Run.ProjectId,
            readOnly ? McpPermissionDto.RunRead : McpPermissionDto.RunVariableWrite);
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => host.Backend.CallToolAsync(
            "sereinflow_set_run_variable", JsonSerializer.SerializeToElement(new
            { runId = host.Run.Id, name = "count", value = 1, valueType = "int32", idempotencyKey = "denied" }), CancellationToken.None));
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
        Assert.Equal(0, host.Variables.CallCount);
    }

    [Fact]
    public async Task ReadsAreProjectScopedAndInactiveRunsReturnTheWorkerError()
    {
        using var host = new TestHost();
        host.SetPrincipal(Guid.NewGuid(), McpPermissionDto.RunRead);
        await Assert.ThrowsAsync<McpProtocolException>(() => host.Backend.CallToolAsync("sereinflow_get_run_variables",
            JsonSerializer.SerializeToElement(new { runId = host.Run.Id }), CancellationToken.None));
        Assert.Equal(0, host.Variables.CallCount);
        host.SetPrincipal(host.Run.ProjectId, McpPermissionDto.RunRead);
        host.Variables.Inactive = true;
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => host.Backend.CallToolAsync("sereinflow_get_run_variables",
            JsonSerializer.SerializeToElement(new { runId = host.Run.Id }), CancellationToken.None));
        Assert.Equal(WorkerErrorCodes.NotActive, JsonSerializer.SerializeToElement(exception.ErrorData).GetProperty("code").GetString());
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"sereinflow-variable-mcp-{Guid.NewGuid():N}");
        private readonly ServiceProvider _services;
        public FlowRun Run { get; } = FlowRun.Start(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        public RecordingVariables Variables { get; } = new();
        public SereinFlowMcpBackend Backend => (SereinFlowMcpBackend)_services.GetRequiredService<ISereinFlowMcpBackend>();

        public TestHost()
        {
            Run.MarkRunning(DateTimeOffset.UtcNow);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["SereinFlow:DataRoot"] = _root }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSereinFlowStorage(configuration, _root);
            services.AddSereinFlowApplication();
            services.AddSereinFlowMcp(configuration);
            services.AddSingleton<IFlowRunStore>(new TestRunStore(Run));
            services.AddSingleton<IRunVariableService>(Variables);
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public void SetPrincipal(Guid projectId, params McpPermissionDto[] permissions)
            => _services.GetRequiredService<IMcpPrincipalAccessor>().Current = new McpPrincipal("caller", projectId, permissions.ToHashSet());
        public void Dispose()
        {
            _services.Dispose();
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
            catch (IOException) { /* SQLite pooled handles may outlive the disposed test host. */ }
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
            if (Inactive) return Task.FromResult(new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 409, Code: WorkerErrorCodes.NotActive));
            if (request.Operation == "set") _variable = new(request.Name!, "System.Int32", true, request.Value);
            return Task.FromResult(new WorkerVariableResponseDto(WorkerProtocol.Version, request.RunId, 200, Variable: _variable));
        }
    }

    private sealed class TestRunStore(FlowRun run) : IFlowRunStore
    {
        public Task<FlowRun?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == run.Id ? run : null);
        public FlowRun? Find(Guid id) => id == run.Id ? run : null;
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowRun>>([run]);
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<FlowDefinitionDto?>(null);
        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow) => throw new NotSupportedException();
        public FlowDefinitionDto? GetSnapshot(Guid id) => null;
        public bool Save(FlowRun value) => false;
    }
}
