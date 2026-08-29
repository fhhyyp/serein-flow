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
        var invalid = current with { RunPolicy = null };

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

    private static FlowDefinitionWriteService CreateWriter(Project project, InMemoryFlowRepository flows)
    {
        var projects = new InMemoryProjectRepository(project);
        var references = new EmptyProjectLibraryReferenceRepository();
        var catalog = new EmptyLibraryCatalog();
        return new(
            projects,
            flows,
            new ProjectLibraryService(projects, flows, references, catalog));
    }

    private static FlowDefinitionDto CreateDefinition()
        => new(
            Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [], [], "Main")],
            string.Empty,
            "checksum",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));

    private sealed class InMemoryProjectRepository(Project project) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([project]);
        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == project.Id ? project : null);
        public Task AddAsync(Project value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryUpdateAsync(Project value, long expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public IReadOnlyList<Project> List() => [project];
        public Project? Find(Guid id) => id == project.Id ? project : null;
        public void Add(Project value) { }
        public bool TryUpdate(Project value, long expectedVersion) => false;
    }

    private sealed class InMemoryFlowRepository(Guid projectId, FlowDefinitionDto definition) : IFlowDefinitionRepository
    {
        private FlowDefinitionDto _definition = definition;
        public int UpdateCount { get; private set; }
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(id == projectId ? [_definition] : []);
        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default) => Task.FromResult(id == projectId && flowId == _definition.Id ? _definition : null);
        public Task AddAsync(Guid id, FlowDefinitionDto value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto value, long expectedVersion, CancellationToken cancellationToken = default)
        {
            if (id != projectId || _definition.Version != expectedVersion)
                return Task.FromResult<FlowDefinitionDto?>(null);
            UpdateCount++;
            _definition = value with { Version = expectedVersion + 1 };
            return Task.FromResult<FlowDefinitionDto?>(_definition);
        }
        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id) => id == projectId ? [_definition] : [];
        public FlowDefinitionDto? Find(Guid id, Guid flowId) => id == projectId && flowId == _definition.Id ? _definition : null;
        public void Add(Guid id, FlowDefinitionDto value) { }
        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto value, long expectedVersion) => null;
    }

    private sealed class EmptyProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectLibraryReference>>([]);
        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
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
}
