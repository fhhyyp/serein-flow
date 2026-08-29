using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class ProjectLibraryServiceTests
{
    private const string LibraryId = "e7b5f2a9c4d8";

    [Fact]
    public async Task ArchivedArtifactCannotBeNewlyReferencedButExistingReferenceRemainsValid()
    {
        var project = Project.Create("Archived library project");
        var projects = new InMemoryProjectRepository(project);
        var references = new InMemoryProjectLibraryReferenceRepository();
        var service = CreateService(projects, new InMemoryFlowDefinitionRepository(), references, CreateLibrary(LibraryLifecycleDto.Archived));

        var add = await service.AddAsync(project.Id, LibraryId);

        Assert.False(add.IsSuccess);
        Assert.Equal(409, add.StatusCode);
        Assert.Equal("library.archived", add.Code);

        await references.AddAsync(project.Id, LibraryId);
        var validation = await service.ValidateFlowLibrariesAsync(project.Id, CreateExternalLibraryFlow());

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task RemoveRejectsArtifactUsedByCurrentProjectFlow()
    {
        var project = Project.Create("Referenced library project");
        var projects = new InMemoryProjectRepository(project);
        var flows = new InMemoryFlowDefinitionRepository();
        var references = new InMemoryProjectLibraryReferenceRepository();
        var service = CreateService(projects, flows, references, CreateLibrary());
        var definition = CreateExternalLibraryFlow();

        await flows.AddAsync(project.Id, definition);
        await service.AddAsync(project.Id, LibraryId);

        var remove = await service.RemoveAsync(project.Id, LibraryId);

        Assert.False(remove.IsSuccess);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal("project_library.in_use", remove.Code);
    }

    [Fact]
    public async Task RemoveRejectsArtifactUsedByProductionHistory()
    {
        var project = Project.Create("Production history library project");
        var projects = new InMemoryProjectRepository(project);
        var flows = new InMemoryFlowDefinitionRepository();
        var references = new InMemoryProjectLibraryReferenceRepository();
        var service = CreateService(
            projects,
            flows,
            references,
            CreateLibrary(),
            new ProductionHistoryVersionRepository(isReferenced: true));
        await service.AddAsync(project.Id, LibraryId);

        var remove = await service.RemoveAsync(project.Id, LibraryId);

        Assert.False(remove.IsSuccess);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal("project_library.in_use_by_production_history", remove.Code);
    }

    [Fact]
    public async Task ValidationRejectsArtifactNotReferencedByCurrentProject()
    {
        var projectA = Project.Create("Project A");
        var projectB = Project.Create("Project B");
        var projects = new InMemoryProjectRepository(projectA, projectB);
        var service = CreateService(
            projects,
            new InMemoryFlowDefinitionRepository(),
            new InMemoryProjectLibraryReferenceRepository(),
            CreateLibrary());

        var add = await service.AddAsync(projectA.Id, LibraryId);
        var validation = await service.ValidateFlowLibrariesAsync(projectB.Id, CreateExternalLibraryFlow());

        Assert.True(add.IsSuccess);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == "project_library.not_referenced");
    }

    [Fact]
    public async Task ValidationRejectsTamperedLibraryNodeMetadata()
    {
        var project = Project.Create("Metadata validation project");
        var projects = new InMemoryProjectRepository(project);
        var service = CreateService(
            projects,
            new InMemoryFlowDefinitionRepository(),
            new InMemoryProjectLibraryReferenceRepository(),
            CreateLibrary());
        await service.AddAsync(project.Id, LibraryId);

        var validation = await service.ValidateFlowLibrariesAsync(
            project.Id,
            CreateExternalLibraryFlow(dllVersion: "999.0.0"));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == "project_library.node_metadata_invalid");
    }

    [Fact]
    public async Task ValidationRejectsTamperedLibraryContractId()
    {
        var project = Project.Create("Contract validation project");
        var projects = new InMemoryProjectRepository(project);
        var references = new InMemoryProjectLibraryReferenceRepository();
        var service = CreateService(
            projects,
            new InMemoryFlowDefinitionRepository(),
            references,
            CreateLibrary());
        await service.AddAsync(project.Id, LibraryId);

        var flow = CreateExternalLibraryFlow() with
        {
            Canvases =
            [
                CreateExternalLibraryFlow().Canvases[0] with
                {
                    Nodes =
                    [
                        CreateExternalLibraryFlow().Canvases[0].Nodes[0] with
                        {
                            Ui = CreateExternalLibraryFlow().Canvases[0].Nodes[0].Ui! with
                            {
                                LibraryNodeContractId = "different-contract"
                            }
                        }
                    ]
                }
            ]
        };

        var validation = await service.ValidateFlowLibrariesAsync(project.Id, flow);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == "project_library.node_contract_invalid");
    }

    [Fact]
    public void NewProjectValidationRejectsExternalLibraryNodesUntilTheyAreExplicitlyReferenced()
    {
        var validation = ProjectLibraryService.ValidateNewProjectFlowLibraries(CreateExternalLibraryFlow());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == "project_library.not_referenced");
    }

    private static ProjectLibraryService CreateService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        IProjectLibraryReferenceRepository references,
        LibraryDto library,
        IFlowVersionRepository? versions = null)
        => new(projects, flows, references, new InMemoryLibraryCatalogService(library), versions);

    private static LibraryDto CreateLibrary(LibraryLifecycleDto lifecycle = LibraryLifecycleDto.Available)
        => new(
            LibraryId,
            "Production line library",
            "1.0.0",
            "SereinFlow.TestLibrary-1.0.0.zip",
            1024,
            LibraryId,
            DateTimeOffset.UtcNow,
            [CreateLibraryNode()],
            lifecycle);

    private static LibraryNodeDto CreateLibraryNode()
        => new(
            "quality-rate",
            NodeTypeDto.Action,
            "Calculate pass rate",
            null,
            LibraryId,
            "SereinFlow.TestLibrary.生产线节点",
            "计算合格率",
            "SereinFlow.TestLibrary.dll",
            "1.0.0",
            "System.Decimal",
            [],
            false,
            ContractId: "quality-rate");

    private static FlowDefinitionDto CreateExternalLibraryFlow(string dllVersion = "1.0.0")
    {
        var node = new NodeDto(
            "library-action",
            NodeTypeDto.Action,
            "Calculate pass rate",
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                "library-action",
                "node.libraryAction.title",
                "node.libraryAction.subtitle",
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: LibraryId,
                ClassName: "SereinFlow.TestLibrary.生产线节点",
                MethodName: "计算合格率",
                DllName: "SereinFlow.TestLibrary.dll",
                DllVersion: dllVersion,
                ReturnType: "System.Decimal"));
        return new FlowDefinitionDto(
            Guid.NewGuid(),
            4,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "test-checksum");
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects;

        public InMemoryProjectRepository(params Project[] projects)
            => _projects = projects.ToDictionary(static project => project.Id);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>(List());

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(id));

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            Add(project);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(TryUpdate(project, expectedVersion));

        public IReadOnlyList<Project> List()
            => _projects.Values.ToArray();

        public Project? Find(Guid id)
            => _projects.GetValueOrDefault(id);

        public void Add(Project project)
            => _projects.Add(project.Id, project);

        public bool TryUpdate(Project project, long expectedVersion)
        {
            if (!_projects.TryGetValue(project.Id, out var current) || current.Version != expectedVersion)
                return false;
            _projects[project.Id] = project;
            return true;
        }
    }

    private sealed class InMemoryFlowDefinitionRepository : IFlowDefinitionRepository
    {
        private readonly Dictionary<(Guid ProjectId, Guid FlowId), FlowDefinitionDto> _definitions = [];

        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(ListByProject(projectId));

        public Task<FlowDefinitionDto?> FindAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(projectId, flowId));

        public Task AddAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        {
            Add(projectId, definition);
            return Task.CompletedTask;
        }

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid projectId, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(TryUpdate(projectId, definition, expectedVersion));

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId)
            => _definitions
                .Where(pair => pair.Key.ProjectId == projectId)
                .Select(static pair => pair.Value)
                .ToArray();

        public FlowDefinitionDto? Find(Guid projectId, Guid flowId)
            => _definitions.GetValueOrDefault((projectId, flowId));

        public void Add(Guid projectId, FlowDefinitionDto definition)
            => _definitions[(projectId, definition.Id)] = definition;

        public FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion)
        {
            var current = Find(projectId, definition.Id);
            if (current is null || current.Version != expectedVersion)
                return null;
            _definitions[(projectId, definition.Id)] = definition;
            return definition;
        }
    }

    private sealed class InMemoryProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        private readonly Dictionary<Guid, Dictionary<string, ProjectLibraryReference>> _references = [];

        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(
                _references.TryGetValue(projectId, out var references)
                    ? references.Values.OrderBy(static item => item.LibraryId, StringComparer.OrdinalIgnoreCase).ToArray()
                    : []);

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                _references.TryGetValue(projectId, out var references)
                && references.ContainsKey(libraryId));

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            if (!_references.TryGetValue(projectId, out var references))
            {
                references = new Dictionary<string, ProjectLibraryReference>(StringComparer.OrdinalIgnoreCase);
                _references[projectId] = references;
            }

            if (!references.TryGetValue(libraryId, out var reference))
            {
                reference = new ProjectLibraryReference(projectId, libraryId, DateTimeOffset.UtcNow);
                references[libraryId] = reference;
            }

            return Task.FromResult(reference);
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                _references.TryGetValue(projectId, out var references)
                && references.Remove(libraryId));
    }

    private sealed class ProductionHistoryVersionRepository(bool isReferenced) : IFlowVersionRepository
    {
        public Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowVersionSummaryDto>>([]);

        public Task<FlowVersionDetailDto?> FindVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowVersionDetailDto?>(null);

        public Task<FlowDefinitionDto?> FindProductionDefinitionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowDefinitionDto?>(null);

        public Task<long?> FindProductionVersionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);

        public Task<FlowVersionMutationResult> PublishAsync(Guid projectId, Guid flowId, long expectedDevelopmentVersion, string? remark, long? expectedProductionVersion = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new FlowVersionMutationResult(null, null));

        public Task<FlowVersionMutationResult> RollbackAsync(Guid projectId, Guid flowId, long sourceVersion, FlowVersionTrackDto track, long expectedHeadVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(new FlowVersionMutationResult(null, null));

        public Task<bool> IsLibraryReferencedByProductionHistoryAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(isReferenced);
    }

    private sealed class InMemoryLibraryCatalogService(params LibraryDto[] libraries) : ILibraryCatalogService
    {
        private readonly Dictionary<string, LibraryDto> _libraries = libraries.ToDictionary(
            static library => library.Id,
            StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<LibraryDto> List()
            => _libraries.Values.Where(static library => library.Lifecycle == LibraryLifecycleDto.Available).ToArray();

        public LibraryDto? Find(string libraryId)
            => _libraries.GetValueOrDefault(libraryId);

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LibraryDto>>(
                _libraries.Values
                    .Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available)
                    .ToArray());

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(libraryId));

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool Delete(string libraryId)
            => _libraries.Remove(libraryId);

        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            if (!_libraries.TryGetValue(libraryId, out var library))
                return Task.FromResult(false);
            _libraries[libraryId] = library with { Lifecycle = LibraryLifecycleDto.Archived };
            return Task.FromResult(true);
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(libraryId));

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
