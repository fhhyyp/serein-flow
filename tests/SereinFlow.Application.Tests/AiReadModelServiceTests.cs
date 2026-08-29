using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class AiReadModelServiceTests
{
    [Fact]
    public async Task FlowTopologyHidesLiteralValuesAndScriptSourceByDefault()
    {
        var project = Project.Create("AI project", id: Guid.NewGuid());
        var definition = CreateDefinition(project.Id);
        var service = CreateService(project, definition);

        var topology = await service.GetFlowTopologyAsync(project.Id, definition.Id);

        Assert.NotNull(topology);
        var node = Assert.Single(Assert.Single(topology.Canvases).Nodes);
        var parameter = Assert.Single(node.Parameters);
        Assert.Equal("amount", parameter.Id);
        Assert.True(parameter.IsConfigured);
        Assert.Null(parameter.ValueJson);
        Assert.Null(parameter.ProjectInputKey);
        Assert.Null(parameter.Expression);
        Assert.NotNull(node.Script);
        Assert.Null(node.Script.Source);
        Assert.Equal("source-hash", node.Script.SourceHash);
    }

    [Fact]
    public async Task FlowTopologyCanExplicitlyIncludeBoundedValuesAndScriptSource()
    {
        var project = Project.Create("AI project", id: Guid.NewGuid());
        var definition = CreateDefinition(project.Id);
        var service = CreateService(project, definition);

        var topology = await service.GetFlowTopologyAsync(
            project.Id,
            definition.Id,
            options: new AiReadModelOptions(IncludeFlowLiteralValues: true, IncludeScriptSource: true));

        Assert.NotNull(topology);
        var node = Assert.Single(Assert.Single(topology.Canvases).Nodes);
        Assert.Equal("42", Assert.Single(node.Parameters).ValueJson);
        Assert.Equal("secret-input", Assert.Single(node.Parameters).ProjectInputKey);
        Assert.Equal("secret-expression", Assert.Single(node.Parameters).Expression);
        Assert.Equal("return amount", node.Script!.Source);
    }

    [Fact]
    public async Task RunInspectionParsesEventsAndMarksMalformedOrTruncatedPayloads()
    {
        var project = Project.Create("AI project", id: Guid.NewGuid());
        var definition = CreateDefinition(project.Id);
        var run = FlowRun.Start(
            project.Id,
            definition.Id,
            definition.Version,
            DateTimeOffset.UtcNow,
            FlowConcurrencyMode.Parallel,
            isListenerRun: false);
        var events = new InMemoryEventStore(
            new FlowRunEvent(run.Id, 1, DateTimeOffset.UtcNow, "run.started", null, "{\"ok\":true}"),
            new FlowRunEvent(run.Id, 2, DateTimeOffset.UtcNow, "node.completed", "node", "not-json"));
        var largeOutput = JsonSerializer.Serialize(new { value = new string('x', 1_400) });
        var outputs = new InMemoryOutputStore(new FlowRunOutput(
            run.Id,
            3,
            DateTimeOffset.UtcNow,
            "node",
            "completed",
            "Success",
            largeOutput,
            null,
            null,
            "{\"amount\":42}"));
        var service = CreateService(project, definition, run, events, outputs);

        var inspection = await service.GetRunInspectionAsync(
            run.Id,
            options: new AiReadModelOptions(MaxItems: 10, MaxJsonBytes: 1_024));

        Assert.NotNull(inspection);
        Assert.Equal(2, inspection.Events.Items.Count);
        Assert.False(inspection.Events.Items[0].PayloadWasMalformed);
        Assert.True(inspection.Events.Items[1].PayloadWasMalformed);
        Assert.Equal("node.completed", inspection.Events.Items[1].Type);
        var output = Assert.Single(inspection.Outputs.Items);
        Assert.False(output.OutputsWereMalformed);
        Assert.True(output.OutputsWereTruncated);
        Assert.Equal(42, output.Inputs.GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task DebugStateExposesStructuredPauseAndLastResult()
    {
        var project = Project.Create("AI project", id: Guid.NewGuid());
        var definition = CreateDefinition(project.Id);
        var session = FlowDebugSession.Create(
            Guid.NewGuid(),
            project.Id,
            definition.Id,
            ["node"],
            DateTimeOffset.UtcNow);
        session.MarkRunning(DateTimeOffset.UtcNow);
        session.Pause(
            new FlowDebugPauseState(
                "node",
                "Script",
                4,
                0,
                null,
                9,
                "{\"amount\":42}",
                DateTimeOffset.UtcNow),
            DateTimeOffset.UtcNow);
        var service = CreateService(
            project,
            definition,
            debugStore: new InMemoryDebugSessionStore(session));

        var debug = await service.GetDebugStateAsync(session.Id);

        Assert.NotNull(debug);
        Assert.Equal(2, debug.StateRevision);
        Assert.Equal("Script", debug.PauseState!.NodeType);
        Assert.Equal(4, debug.PauseState.Step);
        Assert.Equal(42, debug.PauseState.Inputs.GetProperty("amount").GetInt32());
    }

    private static AiReadModelService CreateService(
        Project project,
        FlowDefinitionDto definition,
        FlowRun? run = null,
        InMemoryEventStore? events = null,
        InMemoryOutputStore? outputs = null,
        IFlowDebugSessionStore? debugStore = null)
        => new(
            new SingleProjectRepository(project),
            new SingleFlowRepository(project.Id, definition),
            new SingleVersionRepository(),
            new EmptyLibraryCatalog(),
            new InMemoryRunStore(run, definition),
            events ?? new InMemoryEventStore(),
            outputs ?? new InMemoryOutputStore(),
            debugStore ?? new EmptyDebugSessionStore());

    private static FlowDefinitionDto CreateDefinition(Guid projectId)
    {
        var script = new ScriptNodeDataDto(
            "node",
            "return amount",
            "1",
            "source-hash",
            [new ScriptValueContractDto("amount", "number", true, "amount")],
            [new ScriptValueContractDto("result", "number", true, "result")]);
        var node = new NodeDto(
            "node",
            NodeTypeDto.Script,
            "Calculate",
            0,
            0,
            [],
            [new NodeParameterDto(
                "amount",
                "42",
                DataSourceDto.Literal,
                true,
                new NodeParameterUiMetadataDto("amount", "amount", "number", "42", "secret-input", "secret-expression", null, null, Type: "System.Int32"))],
            script);
        return new FlowDefinitionDto(
            Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [], "Main")],
            node.Id,
            "checksum",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
    }

    private sealed class SingleProjectRepository(Project project) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([project]);
        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == project.Id ? project : null);
        public Task AddAsync(Project value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryUpdateAsync(Project value, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IReadOnlyList<Project> List() => [project];
        public Project? Find(Guid id) => id == project.Id ? project : null;
        public void Add(Project value) => throw new NotSupportedException();
        public bool TryUpdate(Project value, long expectedVersion) => throw new NotSupportedException();
    }

    private sealed class SingleFlowRepository(Guid projectId, FlowDefinitionDto definition) : IFlowDefinitionRepository
    {
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(id == projectId ? [definition] : []);
        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default) => Task.FromResult(id == projectId && flowId == definition.Id ? definition : null);
        public Task AddAsync(Guid id, FlowDefinitionDto value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto value, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id) => id == projectId ? [definition] : [];
        public FlowDefinitionDto? Find(Guid id, Guid flowId) => id == projectId && flowId == definition.Id ? definition : null;
        public void Add(Guid id, FlowDefinitionDto value) => throw new NotSupportedException();
        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto value, long expectedVersion) => throw new NotSupportedException();
    }

    private sealed class SingleVersionRepository : IFlowVersionRepository
    {
        public Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowVersionSummaryDto>>([]);
        public Task<FlowVersionDetailDto?> FindVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default) => Task.FromResult<FlowVersionDetailDto?>(null);
        public Task<FlowDefinitionDto?> FindProductionDefinitionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDefinitionDto?>(null);
        public Task<long?> FindProductionVersionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
        public Task<FlowVersionMutationResult> PublishAsync(Guid projectId, Guid flowId, long expectedDevelopmentVersion, string? remark, long? expectedProductionVersion = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowVersionMutationResult> RollbackAsync(Guid projectId, Guid flowId, long sourceVersion, FlowVersionTrackDto track, long expectedHeadVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsLibraryReferencedByProductionHistoryAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class EmptyLibraryCatalog : ILibraryCatalogService
    {
        public IReadOnlyList<LibraryDto> List() => [];
        public LibraryDto? Find(string libraryId) => null;
        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LibraryDto>>([]);
        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default) => Task.FromResult<LibraryDto?>(null);
        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default) => Task.FromResult<LibraryDto?>(null);
        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class InMemoryRunStore(FlowRun? run, FlowDefinitionDto definition) : IFlowRunStore
    {
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowRun>>(run is null ? [] : [run]);
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult(run?.Id == runId ? run : null);
        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult(run?.Id == runId ? definition : null);
        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow) => throw new NotSupportedException();
        public FlowRun? Find(Guid runId) => run?.Id == runId ? run : null;
        public FlowDefinitionDto? GetSnapshot(Guid runId) => run?.Id == runId ? definition : null;
        public bool Save(FlowRun value) => false;
    }

    private sealed class InMemoryEventStore(params FlowRunEvent[] events) : IFlowRunEventStore
    {
        public Task AppendAsync(IReadOnlyList<FlowRunEvent> value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid runId, long sequenceExclusive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowRunEvent>>(events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive).ToArray());
        public Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult(events.Where(item => item.RunId == runId).Select(item => item.Sequence).DefaultIfEmpty().Max());
        public void Append(IReadOnlyList<FlowRunEvent> value) => throw new NotSupportedException();
        public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive) => events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive).ToArray();
    }

    private sealed class InMemoryOutputStore(params FlowRunOutput[] outputs) : IFlowRunOutputStore
    {
        public Task AppendAsync(IReadOnlyList<FlowRunOutput> value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<FlowRunOutput>> ListAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowRunOutput>>(outputs.Where(item => item.RunId == runId).ToArray());
    }

    private sealed class EmptyDebugSessionStore : IFlowDebugSessionStore
    {
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDebugSession?>(null);
        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDebugSession?>(null);
        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowDebugSession>>([]);
        public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class InMemoryDebugSessionStore(FlowDebugSession session) : IFlowDebugSessionStore
    {
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession value, CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDebugSession?>(session.Id == sessionId ? session : null);
        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDebugSession?>(session.RunId == runId ? session : null);
        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowDebugSession>>(session.IsTerminal ? [] : [session]);
        public Task<bool> SaveAsync(FlowDebugSession value, CancellationToken cancellationToken = default) => Task.FromResult(value.Id == session.Id);
    }
}
