using System.Collections.Generic;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;
public sealed class FlowDefinitionWriteServiceTests
{
    [Fact]
    public async Task InvalidDefinitionDoesNotReachRepository()
    {
        var project = Project.Create("write-test");
        var current = CreateDefinition();
        var flows = new InMemoryFlowRepository(project.Id, current);
        var writer = CreateWriter(project, flows);
        var invalid = current with
        {
            RunPolicy = null
        };
        var result = await writer.WriteAsync(project.Id, current.Id, invalid, current.Version);
        Assert.Equal(FlowDefinitionWriteStatus.Invalid, result.Status);
        Assert.Equal(0, flows.UpdateCount);
    }

    [Fact]
    public async Task NormalizedNoChangeDoesNotCreateAnotherVersion()
    {
        var project = Project.Create("write-test");
        var current = CreateDefinition();
        var flows = new InMemoryFlowRepository(project.Id, current);
        var writer = CreateWriter(project, flows);
        var result = await writer.WriteAsync(project.Id, current.Id, current, current.Version);
        Assert.Equal(FlowDefinitionWriteStatus.NoChange, result.Status);
        Assert.Equal(current.Version, result.Saved!.Version);
        Assert.Equal(0, flows.UpdateCount);
    }

    [Fact]
    public async Task ArchivedProjectCannotSaveEvenWhenDefinitionIsValid()
    {
        var project = Project.Create("write-test");
        project.Archive();
        var current = CreateDefinition();
        var flows = new InMemoryFlowRepository(project.Id, current);
        var writer = CreateWriter(project, flows);
        var result = await writer.WriteAsync(project.Id, current.Id, current with { Checksum = "changed" }, current.Version);
        Assert.Equal(FlowDefinitionWriteStatus.Archived, result.Status);
        Assert.Equal(0, flows.UpdateCount);
    }

    [Fact]
    public async Task SavedDefinitionPublishesOneCommitEventWithAffectedScopes()
    {
        var project = Project.Create("write-test");
        var current = CreateDefinition();
        var flows = new InMemoryFlowRepository(project.Id, current);
        var publisher = new RecordingWorkspaceChangePublisher();
        var writer = CreateWriter(project, flows, publisher);
        var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("value", "parameter.value", "number", "1", null, null, null, null, Type: "System.Int32", InputMode: "manual");
        var parameter = new NodeParameterDto("value", "1", DataSourceDto.Literal, false, nodeParameterUiMetadataDto);
        var nodeUiMetadataDto = new NodeUiMetadataDto("action", "node.action.title", "node.action.subtitle", null, "ready", false, null);
        var node = new NodeDto("node", NodeTypeDto.Action, "Node", 0, 0, [], [parameter], null, nodeUiMetadataDto);
        var candidate = current with
        {
            EntryNodeId = node.Id,
            Canvases = [current.Canvases[0] with
            {
                Nodes = [node]
            }

            ],
        };
        var result = await writer.WriteAsync(project.Id, current.Id, candidate, current.Version, origin: "mcp");
        Assert.Equal(FlowDefinitionWriteStatus.Saved, result.Status);
        var change = Assert.Single(publisher.Events);
        Assert.Equal("flow.changed", change.ChangeType);
        Assert.Equal("mcp", change.Origin);
        Assert.Equal(["main"], change.CanvasIds);
        Assert.Equal(["node"], change.NodeIds);
        Assert.Equal(["value"], change.ParameterIds);
        Assert.Empty(change.ConnectionIds!);
    }

    private static FlowDefinitionWriteService CreateWriter(Project project, InMemoryFlowRepository flows, IWorkspaceChangePublisher? publisher = null)
    {
        var projects = new InMemoryProjectRepository(project);
        var references = new EmptyProjectLibraryReferenceRepository();
        var catalog = new EmptyLibraryCatalog();
        var projectLibraryService = new ProjectLibraryService(projects, flows, references, catalog);
        FlowDefinitionWriteService evaluatedValue = new(projects, flows, projectLibraryService, publisher);
        return evaluatedValue;
    }

    private static FlowDefinitionDto CreateDefinition()
    {
        var newGuidResult = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue1 = [new CanvasDto("main", CanvasLifecycleDto.Main, [], [], "Main")];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        FlowDefinitionDto evaluatedValue2 = new(newGuidResult, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue1, string.Empty, "checksum", RunPolicy: flowRunPolicyDto);
        return evaluatedValue2;
    }

    private sealed class InMemoryProjectRepository(Project project) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult = Task.FromResult<IReadOnlyList<Project>>([project]);
            return fromResultResult;
        }

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult3 = Task.FromResult(id == project.Id ? project : null);
            return fromResultResult3;
        }

        public Task AddAsync(Project value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryUpdateAsync(Project value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var fromResultResult4 = Task.FromResult(false);
            return fromResultResult4;
        }

        public IReadOnlyList<Project> List() => [project];
        public Project? Find(Guid id) => id == project.Id ? project : null;
        public void Add(Project value)
        {
        }

        public bool TryUpdate(Project value, long expectedVersion) => false;
    }

    private sealed class InMemoryFlowRepository(Guid projectId, FlowDefinitionDto definition) : IFlowDefinitionRepository
    {
        private FlowDefinitionDto _definition = definition;
        public int UpdateCount { get; private set; }

        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult5 = Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(id == projectId ? [_definition] : []);
            return fromResultResult5;
        }

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult6 = Task.FromResult(id == projectId && flowId == _definition.Id ? _definition : null);
            return fromResultResult6;
        }

        public Task AddAsync(Guid id, FlowDefinitionDto value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            if (id != projectId || _definition.Version != expectedVersion)
            {
                var fromResultResult7 = Task.FromResult<FlowDefinitionDto?>(null);
                return fromResultResult7;
            }

            UpdateCount++;
            _definition = value with
            {
                Version = expectedVersion + 1
            };
            var fromResultResult8 = Task.FromResult<FlowDefinitionDto?>(_definition);
            return fromResultResult8;
        }

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id) => id == projectId ? [_definition] : [];
        public FlowDefinitionDto? Find(Guid id, Guid flowId) => id == projectId && flowId == _definition.Id ? _definition : null;
        public void Add(Guid id, FlowDefinitionDto value)
        {
        }

        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto value, long expectedVersion) => null;
    }

    private sealed class EmptyProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var fromResultResult9 = Task.FromResult<IReadOnlyList<ProjectLibraryReference>>([]);
            return fromResultResult9;
        }

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult10 = Task.FromResult(false);
            return fromResultResult10;
        }

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult11 = Task.FromResult(false);
            return fromResultResult11;
        }
    }

    private sealed class EmptyLibraryCatalog : ILibraryCatalogService
    {
        public IReadOnlyList<LibraryDto> List() => [];
        public LibraryDto? Find(string libraryId) => null;
        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var fromResultResult12 = Task.FromResult<IReadOnlyList<LibraryDto>>([]);
            return fromResultResult12;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult13 = Task.FromResult<LibraryDto?>(null);
            return fromResultResult13;
        }

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult14 = Task.FromResult(false);
            return fromResultResult14;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult15 = Task.FromResult<LibraryDto?>(null);
            return fromResultResult15;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult16 = Task.FromResult(0);
            return fromResultResult16;
        }
    }

    private sealed class RecordingWorkspaceChangePublisher : IWorkspaceChangePublisher
    {
        public List<WorkspaceChangeEventDto> Events { get; } = [];

        public Task PublishAsync(WorkspaceChangeEventDto change, CancellationToken cancellationToken = default)
        {
            Events.Add(change);
            return Task.CompletedTask;
        }
    }
}
