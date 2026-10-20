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
        var newGuidResult = Guid.NewGuid();
        var project = Project.Create("AI project", id: newGuidResult);
        var definition = CreateDefinition(project.Id);
        var service = CreateService(project, definition);
        var topology = await service.GetFlowTopologyAsync(project.Id, definition.Id);
        Assert.NotNull(topology);
        var evaluatedValue = Assert.Single(topology.Canvases).Nodes;
        var node = Assert.Single(evaluatedValue);
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
        var newGuidResult1 = Guid.NewGuid();
        var project = Project.Create("AI project", id: newGuidResult1);
        var definition = CreateDefinition(project.Id);
        var service = CreateService(project, definition);
        var aiReadModelOptions = new AiReadModelOptions(IncludeFlowLiteralValues: true, IncludeScriptSource: true);
        var topology = await service.GetFlowTopologyAsync(project.Id, definition.Id, options: aiReadModelOptions);
        Assert.NotNull(topology);
        var evaluatedValue2 = Assert.Single(topology.Canvases).Nodes;
        var node = Assert.Single(evaluatedValue2);
        var evaluatedValue3 = Assert.Single(node.Parameters).ValueJson;
        Assert.Equal("42", evaluatedValue3);
        var evaluatedValue4 = Assert.Single(node.Parameters).ProjectInputKey;
        Assert.Equal("secret-input", evaluatedValue4);
        var evaluatedValue5 = Assert.Single(node.Parameters).Expression;
        Assert.Equal("secret-expression", evaluatedValue5);
        Assert.Equal("return amount", node.Script!.Source);
    }

    [Fact]
    public async Task FlowTopologyTreatsIncomingDataConnectionAsNodeInputForDiagnostics()
    {
        var newGuidResult6 = Guid.NewGuid();
        var project = Project.Create("AI project", id: newGuidResult6);
        var baseDefinition = CreateDefinition(project.Id);
        var definition = baseDefinition with
        {
            Canvases = [baseDefinition.Canvases[0] with
            {
                Connections = [new ConnectionDto("data", "upstream", "result", "node", "amount", ConnectionKindDto.Data, null, null, 0)]
            }

            ]
        };
        var service = CreateService(project, definition);
        var topology = await service.GetFlowTopologyAsync(project.Id, definition.Id);
        var canvas = Assert.Single(topology!.Canvases);
        var evaluatedValue7 = Assert.Single(canvas.Nodes).Parameters;
        var parameter = Assert.Single(evaluatedValue7);
        var connection = Assert.Single(canvas.Connections);
        var toStringResult = DataSourceDto.PreviousNode.ToString();
        Assert.Equal(toStringResult, parameter.Source);
        Assert.Equal("upstream", parameter.SourceNodeId);
        Assert.Equal("result", parameter.SourcePortId);
        Assert.Null(parameter.ValueJson);
        var toStringResult8 = DataSourceDto.PreviousNode.ToString();
        Assert.Equal(toStringResult8, connection.DataSource);
    }

    [Fact]
    public async Task RunInspectionParsesEventsAndMarksMalformedOrTruncatedPayloads()
    {
        var newGuidResult9 = Guid.NewGuid();
        var project = Project.Create("AI project", id: newGuidResult9);
        var definition = CreateDefinition(project.Id);
        var run = FlowRun.Start(project.Id, definition.Id, definition.Version, DateTimeOffset.UtcNow, FlowConcurrencyMode.Parallel, isListenerRun: false);
        var flowRunEvent = new FlowRunEvent(run.Id, 1, DateTimeOffset.UtcNow, "run.started", null, "{\"ok\":true}");
        var flowRunEvent10 = new FlowRunEvent(run.Id, 2, DateTimeOffset.UtcNow, "node.completed", "node", "not-json");
        var events = new InMemoryEventStore(flowRunEvent, flowRunEvent10);
        var stringValue = new string ('x', 1_400);
        var evaluatedValue11 = new
        {
            value = stringValue
        };
        var largeOutput = JsonSerializer.Serialize(evaluatedValue11);
        var flowRunOutput = new FlowRunOutput(run.Id, 3, DateTimeOffset.UtcNow, "node", "completed", "Success", largeOutput, null, null, "{\"amount\":42}");
        var outputs = new InMemoryOutputStore(flowRunOutput);
        var service = CreateService(project, definition, run, events, outputs);
        var aiReadModelOptions12 = new AiReadModelOptions(MaxItems: 10, MaxJsonBytes: 1_024);
        var inspection = await service.GetRunInspectionAsync(run.Id, options: aiReadModelOptions12);
        Assert.NotNull(inspection);
        Assert.Equal(2, inspection.Events.Items.Count);
        Assert.False(inspection.Events.Items[0].PayloadWasMalformed);
        Assert.True(inspection.Events.Items[1].PayloadWasMalformed);
        Assert.Equal("node.completed", inspection.Events.Items[1].Type);
        var output = Assert.Single(inspection.Outputs.Items);
        Assert.False(output.OutputsWereMalformed);
        Assert.True(output.OutputsWereTruncated);
        var getPropertyResult = output.Inputs.GetProperty("amount");
        var getInt32Result = getPropertyResult.GetInt32();
        Assert.Equal(42, getInt32Result);
    }

    [Fact]
    public async Task DebugStateExposesStructuredPauseAndLastResult()
    {
        var newGuidResult13 = Guid.NewGuid();
        var project = Project.Create("AI project", id: newGuidResult13);
        var definition = CreateDefinition(project.Id);
        var newGuidResult14 = Guid.NewGuid();
        var session = FlowDebugSession.Create(newGuidResult14, project.Id, definition.Id, ["node"], DateTimeOffset.UtcNow);
        session.MarkRunning(DateTimeOffset.UtcNow);
        var flowDebugPauseState = new FlowDebugPauseState("node", "Script", 4, 0, null, 9, "{\"amount\":42}", DateTimeOffset.UtcNow);
        session.Pause(flowDebugPauseState, DateTimeOffset.UtcNow);
        var inMemoryDebugSessionStore = new InMemoryDebugSessionStore(session);
        var service = CreateService(project, definition, debugStore: inMemoryDebugSessionStore);
        var debug = await service.GetDebugStateAsync(session.Id);
        Assert.NotNull(debug);
        Assert.Equal(2, debug.StateRevision);
        Assert.Equal("Script", debug.PauseState!.NodeType);
        Assert.Equal(4, debug.PauseState.Step);
        var getPropertyResult15 = debug.PauseState.Inputs.GetProperty("amount");
        var getInt32Result16 = getPropertyResult15.GetInt32();
        Assert.Equal(42, getInt32Result16);
    }

    [Fact]
    public async Task ProjectCatalogSeparatesArchivedItemsBeforePagination()
    {
        var parseResult = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var archived = Project.Create("Archived project", id: parseResult);
        archived.Archive();
        var parseResult17 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var firstActive = Project.Create("First active project", id: parseResult17);
        var parseResult18 = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var secondActive = Project.Create("Second active project", id: parseResult18);
        var service = CreateCatalogService([archived, firstActive, secondActive], []);
        var aiReadModelOptions19 = new AiReadModelOptions(MaxItems: 1);
        var activePage = await service.ListProjectsAsync(aiReadModelOptions19);
        var aiReadModelOptions20 = new AiReadModelOptions(MaxItems: 1);
        var archivedPage = await service.ListArchivedProjectsAsync(aiReadModelOptions20);
        var evaluatedValue21 = Assert.Single(activePage.Items).Id;
        Assert.Equal(firstActive.Id, evaluatedValue21);
        Assert.True(activePage.HasMore);
        var toStringResult22 = firstActive.Id.ToString("D");
        Assert.Equal(toStringResult22, activePage.NextCursor);
        var evaluatedValue23 = Assert.Single(archivedPage.Items).Id;
        Assert.Equal(archived.Id, evaluatedValue23);
        Assert.False(archivedPage.HasMore);
        Assert.Null(archivedPage.NextCursor);
    }

    [Fact]
    public async Task LibraryCatalogSeparatesLifecycleBeforePaginationAndPreservesAllView()
    {
        var archived = CreateLibrary("000-archived", LibraryLifecycleDto.Archived);
        var firstAvailable = CreateLibrary("100-available", LibraryLifecycleDto.Available);
        var secondAvailable = CreateLibrary("200-available", LibraryLifecycleDto.Available);
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Domain.Project> evaluatedValue26 = [Project.Create("Catalog project")];
        var service = CreateCatalogService(evaluatedValue26, [archived, firstAvailable, secondAvailable]);
        var aiReadModelOptions27 = new AiReadModelOptions(MaxItems: 1);
        var activePage = await service.ListLibrariesAsync(options: aiReadModelOptions27);
        var aiReadModelOptions28 = new AiReadModelOptions(MaxItems: 1);
        var archivedPage = await service.ListArchivedLibrariesAsync(aiReadModelOptions28);
        var aiReadModelOptions29 = new AiReadModelOptions(MaxItems: 10);
        var allPage = await service.ListLibrariesAsync(includeArchived: true, options: aiReadModelOptions29);
        var evaluatedValue30 = Assert.Single(activePage.Items).Id;
        Assert.Equal(firstAvailable.Id, evaluatedValue30);
        Assert.True(activePage.HasMore);
        Assert.Equal(firstAvailable.Id, activePage.NextCursor);
        var evaluatedValue31 = Assert.Single(archivedPage.Items).Id;
        Assert.Equal(archived.Id, evaluatedValue31);
        Assert.False(archivedPage.HasMore);
        Assert.Equal(3, allPage.Items.Count);
        Assert.Contains(allPage.Items, item =>
        {
            var evaluatedValue24 = item.Lifecycle == LibraryLifecycleDto.Available.ToString();
            return evaluatedValue24;
        });
        Assert.Contains(allPage.Items, item =>
        {
            var evaluatedValue25 = item.Lifecycle == LibraryLifecycleDto.Archived.ToString();
            return evaluatedValue25;
        });
    }

    private static AiReadModelService CreateService(Project project, FlowDefinitionDto definition, FlowRun? run = null, InMemoryEventStore? events = null, InMemoryOutputStore? outputs = null, IFlowDebugSessionStore? debugStore = null)
    {
        var singleProjectRepository = new SingleProjectRepository(project);
        var singleFlowRepository = new SingleFlowRepository(project.Id, definition);
        var singleVersionRepository = new SingleVersionRepository();
        var emptyLibraryCatalog = new EmptyLibraryCatalog();
        var inMemoryRunStore = new InMemoryRunStore(run, definition);
        var evaluatedValue32 = events ?? new InMemoryEventStore();
        var evaluatedValue33 = outputs ?? new InMemoryOutputStore();
        var evaluatedValue34 = debugStore ?? new EmptyDebugSessionStore();
        global::SereinFlow.Application.AiReadModelService evaluatedValue35 = new(singleProjectRepository, singleFlowRepository, singleVersionRepository, emptyLibraryCatalog, inMemoryRunStore, evaluatedValue32, evaluatedValue33, evaluatedValue34);
        return evaluatedValue35;
    }

    private static AiReadModelService CreateCatalogService(IReadOnlyList<Project> projects, IReadOnlyList<LibraryDto> libraries)
    {
        var placeholderDefinition = CreateDefinition(projects[0].Id);
        var projectListRepository = new ProjectListRepository(projects);
        var emptyFlowRepository = new EmptyFlowRepository();
        var singleVersionRepository36 = new SingleVersionRepository();
        var testLibraryCatalog = new TestLibraryCatalog(libraries);
        var inMemoryRunStore37 = new InMemoryRunStore(null, placeholderDefinition);
        var inMemoryEventStore = new InMemoryEventStore();
        var inMemoryOutputStore = new InMemoryOutputStore();
        var emptyDebugSessionStore = new EmptyDebugSessionStore();
        global::SereinFlow.Application.AiReadModelService evaluatedValue38 = new(projectListRepository, emptyFlowRepository, singleVersionRepository36, testLibraryCatalog, inMemoryRunStore37, inMemoryEventStore, inMemoryOutputStore, emptyDebugSessionStore);
        return evaluatedValue38;
    }

    private static LibraryDto CreateLibrary(string id, LibraryLifecycleDto lifecycle)
    {
        global::SereinFlow.Contracts.LibraryDto evaluatedValue39 = new(id, id, "1.0.0", $"{id}.zip", 1, id, DateTimeOffset.UtcNow, [], lifecycle);
        return evaluatedValue39;
    }

    private static FlowDefinitionDto CreateDefinition(Guid projectId)
    {
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ScriptValueContractDto> evaluatedValue40 = [new ScriptValueContractDto("amount", "number", true, "amount")];
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ScriptValueContractDto> evaluatedValue41 = [new ScriptValueContractDto("result", "number", true, "result")];
        var script = new ScriptNodeDataDto("node", "return amount", "1", "source-hash", evaluatedValue40, evaluatedValue41);
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeParameterDto> evaluatedValue42 = [resolveNodeParameterDto()];
        var node = new NodeDto("node", NodeTypeDto.Script, "Calculate", 0, 0, [], evaluatedValue42, script);
        var newGuidResult43 = Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.CanvasDto> evaluatedValue44 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [], "Main")];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var flowDefinitionDto = new FlowDefinitionDto(newGuidResult43, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue44, node.Id, "checksum", RunPolicy: flowRunPolicyDto);
        return flowDefinitionDto;
        global::SereinFlow.Contracts.NodeParameterDto resolveNodeParameterDto()
        {
            var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("amount", "amount", "number", "42", "secret-input", "secret-expression", null, null, Type: "System.Int32");
            var nodeParameterDto = new NodeParameterDto("amount", "42", DataSourceDto.Literal, true, nodeParameterUiMetadataDto);
            return nodeParameterDto;
        }
    }

    private sealed class SingleProjectRepository(Project project) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult = Task.FromResult<IReadOnlyList<Project>>([project]);
            return fromResultResult;
        }

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult45 = Task.FromResult(id == project.Id ? project : null);
            return fromResultResult45;
        }

        public Task AddAsync(Project value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryUpdateAsync(Project value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<Project> List() => [project];
        public Project? Find(Guid id) => id == project.Id ? project : null;
        public void Add(Project value)
        {
            throw new NotSupportedException();
        }

        public bool TryUpdate(Project value, long expectedVersion)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ProjectListRepository(IReadOnlyList<Project> projects) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult46 = Task.FromResult(projects);
            return fromResultResult46;
        }

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var singleOrDefaultResult = projects.SingleOrDefault(project => project.Id == id);
            var fromResultResult47 = Task.FromResult(singleOrDefaultResult);
            return fromResultResult47;
        }

        public Task AddAsync(Project value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryUpdateAsync(Project value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<Project> List() => projects;
        public Project? Find(Guid id)
        {
            var singleOrDefaultResult48 = projects.SingleOrDefault(project => project.Id == id);
            return singleOrDefaultResult48;
        }

        public void Add(Project value)
        {
            throw new NotSupportedException();
        }

        public bool TryUpdate(Project value, long expectedVersion)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SingleFlowRepository(Guid projectId, FlowDefinitionDto definition) : IFlowDefinitionRepository
    {
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult49 = Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(id == projectId ? [definition] : []);
            return fromResultResult49;
        }

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult50 = Task.FromResult(id == projectId && flowId == definition.Id ? definition : null);
            return fromResultResult50;
        }

        public Task AddAsync(Guid id, FlowDefinitionDto value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id) => id == projectId ? [definition] : [];
        public FlowDefinitionDto? Find(Guid id, Guid flowId) => id == projectId && flowId == definition.Id ? definition : null;
        public void Add(Guid id, FlowDefinitionDto value)
        {
            throw new NotSupportedException();
        }

        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto value, long expectedVersion)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class EmptyFlowRepository : IFlowDefinitionRepository
    {
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult51 = Task.FromResult<IReadOnlyList<FlowDefinitionDto>>([]);
            return fromResultResult51;
        }

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult52 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult52;
        }

        public Task AddAsync(Guid id, FlowDefinitionDto value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id) => [];
        public FlowDefinitionDto? Find(Guid id, Guid flowId) => null;
        public void Add(Guid id, FlowDefinitionDto value)
        {
            throw new NotSupportedException();
        }

        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto value, long expectedVersion)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SingleVersionRepository : IFlowVersionRepository
    {
        public Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default)
        {
            var fromResultResult53 = Task.FromResult<IReadOnlyList<FlowVersionSummaryDto>>([]);
            return fromResultResult53;
        }

        public Task<FlowVersionDetailDto?> FindVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default)
        {
            var fromResultResult54 = Task.FromResult<FlowVersionDetailDto?>(null);
            return fromResultResult54;
        }

        public Task<FlowDefinitionDto?> FindProductionDefinitionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult55 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult55;
        }

        public Task<long?> FindProductionVersionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult56 = Task.FromResult<long?>(null);
            return fromResultResult56;
        }

        public Task<FlowVersionMutationResult> PublishAsync(Guid projectId, Guid flowId, long expectedDevelopmentVersion, string? remark, long? expectedProductionVersion = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowVersionMutationResult> RollbackAsync(Guid projectId, Guid flowId, long sourceVersion, FlowVersionTrackDto track, long expectedHeadVersion, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsLibraryReferencedByProductionHistoryAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult57 = Task.FromResult(false);
            return fromResultResult57;
        }
    }

    private sealed class EmptyLibraryCatalog : ILibraryCatalogService
    {
        public IReadOnlyList<LibraryDto> List() => [];
        public LibraryDto? Find(string libraryId) => null;
        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var fromResultResult58 = Task.FromResult<IReadOnlyList<LibraryDto>>([]);
            return fromResultResult58;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult59 = Task.FromResult<LibraryDto?>(null);
            return fromResultResult59;
        }

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult60 = Task.FromResult(false);
            return fromResultResult60;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult61 = Task.FromResult<LibraryDto?>(null);
            return fromResultResult61;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult62 = Task.FromResult(0);
            return fromResultResult62;
        }
    }

    private sealed class TestLibraryCatalog(IReadOnlyList<LibraryDto> libraries) : ILibraryCatalogService
    {
        public IReadOnlyList<LibraryDto> List() => libraries;
        public LibraryDto? Find(string libraryId)
        {
            var singleOrDefaultResult63 = libraries.SingleOrDefault(library => library.Id == libraryId);
            return singleOrDefaultResult63;
        }

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var whereResult = libraries.Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available);
            var toArrayResult = whereResult.ToArray();
            var fromResultResult64 = Task.FromResult<IReadOnlyList<LibraryDto>>(toArrayResult);
            return fromResultResult64;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult = Find(libraryId);
            var fromResultResult65 = Task.FromResult(findResult);
            return fromResultResult65;
        }

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult66 = Task.FromResult(false);
            return fromResultResult66;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult67 = Find(libraryId);
            var fromResultResult68 = Task.FromResult(findResult67);
            return fromResultResult68;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult69 = Task.FromResult(0);
            return fromResultResult69;
        }
    }

    private sealed class InMemoryRunStore(FlowRun? run, FlowDefinitionDto definition) : IFlowRunStore
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
            var fromResultResult70 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult70;
        }

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult71 = Task.FromResult<IReadOnlyList<FlowRun>>(run is null ? [] : [run]);
            return fromResultResult71;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult72 = Task.FromResult(run?.Id == runId ? run : null);
            return fromResultResult72;
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult73 = Task.FromResult(run?.Id == runId ? definition : null);
            return fromResultResult73;
        }

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
        {
            var fromResultResult74 = Task.FromResult(false);
            return fromResultResult74;
        }

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
        {
            throw new NotSupportedException();
        }

        public FlowRun? Find(Guid runId) => run?.Id == runId ? run : null;
        public FlowDefinitionDto? GetSnapshot(Guid runId) => run?.Id == runId ? definition : null;
        public bool Save(FlowRun value) => false;
    }

    private sealed class InMemoryEventStore(params FlowRunEvent[] events) : IFlowRunEventStore
    {
        public Task AppendAsync(IReadOnlyList<FlowRunEvent> value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid runId, long sequenceExclusive, CancellationToken cancellationToken = default)
        {
            var whereResult75 = events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive);
            var toArrayResult76 = whereResult75.ToArray();
            var fromResultResult77 = Task.FromResult<IReadOnlyList<FlowRunEvent>>(toArrayResult76);
            return fromResultResult77;
        }

        public Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var whereResult78 = events.Where(item => item.RunId == runId);
            var selectResult = whereResult78.Select(item => item.Sequence);
            var defaultIfEmptyResult = selectResult.DefaultIfEmpty();
            var maxResult = defaultIfEmptyResult.Max();
            var fromResultResult79 = Task.FromResult(maxResult);
            return fromResultResult79;
        }

        public void Append(IReadOnlyList<FlowRunEvent> value)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
        {
            var whereResult80 = events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive);
            var toArrayResult81 = whereResult80.ToArray();
            return toArrayResult81;
        }
    }

    private sealed class InMemoryOutputStore(params FlowRunOutput[] outputs) : IFlowRunOutputStore
    {
        public Task AppendAsync(IReadOnlyList<FlowRunOutput> value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<FlowRunOutput>> ListAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var whereResult82 = outputs.Where(item => item.RunId == runId);
            var toArrayResult83 = whereResult82.ToArray();
            var fromResultResult84 = Task.FromResult<IReadOnlyList<FlowRunOutput>>(toArrayResult83);
            return fromResultResult84;
        }
    }

    private sealed class EmptyDebugSessionStore : IFlowDebugSessionStore
    {
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var fromResultResult85 = Task.FromResult<FlowDebugSession?>(null);
            return fromResultResult85;
        }

        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult86 = Task.FromResult<FlowDebugSession?>(null);
            return fromResultResult86;
        }

        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult87 = Task.FromResult<IReadOnlyList<FlowDebugSession>>([]);
            return fromResultResult87;
        }

        public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
        {
            var fromResultResult88 = Task.FromResult(false);
            return fromResultResult88;
        }
    }

    private sealed class InMemoryDebugSessionStore(FlowDebugSession session) : IFlowDebugSessionStore
    {
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession value, CancellationToken cancellationToken = default)
        {
            var fromResultResult89 = Task.FromResult(value);
            return fromResultResult89;
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var fromResultResult90 = Task.FromResult<FlowDebugSession?>(session.Id == sessionId ? session : null);
            return fromResultResult90;
        }

        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult91 = Task.FromResult<FlowDebugSession?>(session.RunId == runId ? session : null);
            return fromResultResult91;
        }

        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult92 = Task.FromResult<IReadOnlyList<FlowDebugSession>>(session.IsTerminal ? [] : [session]);
            return fromResultResult92;
        }

        public Task<bool> SaveAsync(FlowDebugSession value, CancellationToken cancellationToken = default)
        {
            var fromResultResult93 = Task.FromResult(value.Id == session.Id);
            return fromResultResult93;
        }
    }
}
