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
        var inMemoryFlowDefinitionRepository = new InMemoryFlowDefinitionRepository();
        var createLibraryResult = CreateLibrary(LibraryLifecycleDto.Archived);
        var service = CreateService(projects, inMemoryFlowDefinitionRepository, references, createLibraryResult);
        var add = await service.AddAsync(project.Id, LibraryId);
        Assert.False(add.IsSuccess);
        Assert.Equal(409, add.StatusCode);
        Assert.Equal(LibraryErrorCodes.Archived, add.Code);
        await references.AddAsync(project.Id, LibraryId);
        var createExternalLibraryFlowResult = CreateExternalLibraryFlow();
        var validation = await service.ValidateFlowLibrariesAsync(project.Id, createExternalLibraryFlowResult);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task RemoveRejectsArtifactUsedByCurrentProjectFlow()
    {
        var project = Project.Create("Referenced library project");
        var projects = new InMemoryProjectRepository(project);
        var flows = new InMemoryFlowDefinitionRepository();
        var references = new InMemoryProjectLibraryReferenceRepository();
        var createLibraryResult1 = CreateLibrary();
        var service = CreateService(projects, flows, references, createLibraryResult1);
        var definition = CreateExternalLibraryFlow();
        await flows.AddAsync(project.Id, definition);
        await service.AddAsync(project.Id, LibraryId);
        var remove = await service.RemoveAsync(project.Id, LibraryId);
        Assert.False(remove.IsSuccess);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal(ProjectLibraryErrorCodes.InUse, remove.Code);
    }

    [Fact]
    public async Task RemoveRejectsArtifactUsedByProductionHistory()
    {
        var project = Project.Create("Production history library project");
        var projects = new InMemoryProjectRepository(project);
        var flows = new InMemoryFlowDefinitionRepository();
        var references = new InMemoryProjectLibraryReferenceRepository();
        var createLibraryResult2 = CreateLibrary();
        var productionHistoryVersionRepository = new ProductionHistoryVersionRepository(isReferenced: true);
        var service = CreateService(projects, flows, references, createLibraryResult2, productionHistoryVersionRepository);
        await service.AddAsync(project.Id, LibraryId);
        var remove = await service.RemoveAsync(project.Id, LibraryId);
        Assert.False(remove.IsSuccess);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal(ProjectLibraryErrorCodes.InUseByProductionHistory, remove.Code);
    }

    [Fact]
    public async Task ValidationRejectsArtifactNotReferencedByCurrentProject()
    {
        var projectA = Project.Create("Project A");
        var projectB = Project.Create("Project B");
        var projects = new InMemoryProjectRepository(projectA, projectB);
        var inMemoryFlowDefinitionRepository3 = new InMemoryFlowDefinitionRepository();
        var inMemoryProjectLibraryReferenceRepository = new InMemoryProjectLibraryReferenceRepository();
        var createLibraryResult4 = CreateLibrary();
        var service = CreateService(projects, inMemoryFlowDefinitionRepository3, inMemoryProjectLibraryReferenceRepository, createLibraryResult4);
        var add = await service.AddAsync(projectA.Id, LibraryId);
        var createExternalLibraryFlowResult5 = CreateExternalLibraryFlow();
        var validation = await service.ValidateFlowLibrariesAsync(projectB.Id, createExternalLibraryFlowResult5);
        Assert.True(add.IsSuccess);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ProjectLibraryErrorCodes.NotReferenced);
    }

    [Fact]
    public async Task ValidationRejectsTamperedLibraryNodeMetadata()
    {
        var project = Project.Create("Metadata validation project");
        var projects = new InMemoryProjectRepository(project);
        var inMemoryFlowDefinitionRepository6 = new InMemoryFlowDefinitionRepository();
        var inMemoryProjectLibraryReferenceRepository7 = new InMemoryProjectLibraryReferenceRepository();
        var createLibraryResult8 = CreateLibrary();
        var service = CreateService(projects, inMemoryFlowDefinitionRepository6, inMemoryProjectLibraryReferenceRepository7, createLibraryResult8);
        await service.AddAsync(project.Id, LibraryId);
        var createExternalLibraryFlowResult9 = CreateExternalLibraryFlow(dllVersion: "999.0.0");
        var validation = await service.ValidateFlowLibrariesAsync(project.Id, createExternalLibraryFlowResult9);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ProjectLibraryErrorCodes.NodeMetadataInvalid);
    }

    [Fact]
    public async Task ValidationRejectsTamperedLibraryContractId()
    {
        var project = Project.Create("Contract validation project");
        var projects = new InMemoryProjectRepository(project);
        var references = new InMemoryProjectLibraryReferenceRepository();
        var inMemoryFlowDefinitionRepository10 = new InMemoryFlowDefinitionRepository();
        var createLibraryResult11 = CreateLibrary();
        var service = CreateService(projects, inMemoryFlowDefinitionRepository10, references, createLibraryResult11);
        await service.AddAsync(project.Id, LibraryId);
        var flow = CreateExternalLibraryFlow()with
        {
            Canvases = [CreateExternalLibraryFlow().Canvases[0] with
            {
                Nodes = [CreateExternalLibraryFlow().Canvases[0].Nodes[0] with
                {
                    Ui = CreateExternalLibraryFlow().Canvases[0].Nodes[0].Ui!with
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
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ProjectLibraryErrorCodes.NodeContractInvalid);
    }

    [Fact]
    public void NewProjectValidationRejectsExternalLibraryNodesUntilTheyAreExplicitlyReferenced()
    {
        var createExternalLibraryFlowResult12 = CreateExternalLibraryFlow();
        var validation = ProjectLibraryService.ValidateNewProjectFlowLibraries(createExternalLibraryFlowResult12);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ProjectLibraryErrorCodes.NotReferenced);
    }

    private static ProjectLibraryService CreateService(IProjectRepository projects, IFlowDefinitionRepository flows, IProjectLibraryReferenceRepository references, LibraryDto library, IFlowVersionRepository? versions = null)
    {
        var inMemoryLibraryCatalogService = new InMemoryLibraryCatalogService(library);
        global::SereinFlow.Application.ProjectLibraryService evaluatedValue = new(projects, flows, references, inMemoryLibraryCatalogService, versions);
        return evaluatedValue;
    }

    private static LibraryDto CreateLibrary(LibraryLifecycleDto lifecycle = LibraryLifecycleDto.Available)
    {
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.LibraryNodeDto> evaluatedValue13 = [CreateLibraryNode()];
        global::SereinFlow.Contracts.LibraryDto evaluatedValue14 = new(LibraryId, "Production line library", "1.0.0", "SereinFlow.TestLibrary-1.0.0.zip", 1024, LibraryId, DateTimeOffset.UtcNow, evaluatedValue13, lifecycle);
        return evaluatedValue14;
    }

    private static LibraryNodeDto CreateLibraryNode()
    {
        global::SereinFlow.Contracts.LibraryNodeDto evaluatedValue15 = new("quality-rate", NodeTypeDto.Action, "Calculate pass rate", null, LibraryId, "SereinFlow.TestLibrary.生产线节点", "计算合格率", "SereinFlow.TestLibrary.dll", "1.0.0", "System.Decimal", [], false, ContractId: "quality-rate");
        return evaluatedValue15;
    }

    private static FlowDefinitionDto CreateExternalLibraryFlow(string dllVersion = "1.0.0")
    {
        var nodeUiMetadataDto = new NodeUiMetadataDto("library-action", "node.libraryAction.title", "node.libraryAction.subtitle", null, "ready", true, null, Category: "library", LibraryId: LibraryId, ClassName: "SereinFlow.TestLibrary.生产线节点", MethodName: "计算合格率", DllName: "SereinFlow.TestLibrary.dll", DllVersion: dllVersion, ReturnType: "System.Decimal");
        var node = new NodeDto("library-action", NodeTypeDto.Action, "Calculate pass rate", 0, 0, [], [], null, nodeUiMetadataDto);
        var newGuidResult = Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.CanvasDto> evaluatedValue16 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowDefinitionDto = new FlowDefinitionDto(newGuidResult, 4, 1, evaluatedValue16, node.Id, "test-checksum");
        return flowDefinitionDto;
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects;
        public InMemoryProjectRepository(params Project[] projects)
        {
            _projects = projects.ToDictionary(static project => project.Id);
        }

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            var listResult = List();
            var fromResultResult = Task.FromResult<IReadOnlyList<Project>>(listResult);
            return fromResultResult;
        }

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var findResult = Find(id);
            var fromResultResult17 = Task.FromResult(findResult);
            return fromResultResult17;
        }

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            Add(project);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var tryUpdateResult = TryUpdate(project, expectedVersion);
            var fromResultResult18 = Task.FromResult(tryUpdateResult);
            return fromResultResult18;
        }

        public IReadOnlyList<Project> List()
        {
            var toArrayResult = _projects.Values.ToArray();
            return toArrayResult;
        }

        public Project? Find(Guid id)
        {
            var getValueOrDefaultResult = _projects.GetValueOrDefault(id);
            return getValueOrDefaultResult;
        }

        public void Add(Project project)
        {
            _projects.Add(project.Id, project);
        }

        public bool TryUpdate(Project project, long expectedVersion)
        {
            if (!_projects.TryGetValue(project.Id, out var current) || current.Version != expectedVersion)
            {
                return false;
            }

            _projects[project.Id] = project;
            return true;
        }
    }

    private sealed class InMemoryFlowDefinitionRepository : IFlowDefinitionRepository
    {
        private readonly Dictionary<(Guid ProjectId, Guid FlowId), FlowDefinitionDto> _definitions = [];
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var listByProjectResult = ListByProject(projectId);
            var fromResultResult19 = Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(listByProjectResult);
            return fromResultResult19;
        }

        public Task<FlowDefinitionDto?> FindAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
        {
            var findResult20 = Find(projectId, flowId);
            var fromResultResult21 = Task.FromResult(findResult20);
            return fromResultResult21;
        }

        public Task AddAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        {
            Add(projectId, definition);
            return Task.CompletedTask;
        }

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid projectId, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var tryUpdateResult22 = TryUpdate(projectId, definition, expectedVersion);
            var fromResultResult23 = Task.FromResult(tryUpdateResult22);
            return fromResultResult23;
        }

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId)
        {
            var whereResult = _definitions.Where(pair => pair.Key.ProjectId == projectId);
            var selectResult = whereResult.Select(static pair => pair.Value);
            var toArrayResult24 = selectResult.ToArray();
            return toArrayResult24;
        }

        public FlowDefinitionDto? Find(Guid projectId, Guid flowId)
        {
            var getValueOrDefaultResult25 = _definitions.GetValueOrDefault((projectId, flowId));
            return getValueOrDefaultResult25;
        }

        public void Add(Guid projectId, FlowDefinitionDto definition) => _definitions[(projectId, definition.Id)] = definition;
        public FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion)
        {
            var current = Find(projectId, definition.Id);
            if (current is null || current.Version != expectedVersion)
            {
                return null;
            }

            _definitions[(projectId, definition.Id)] = definition;
            return definition;
        }
    }

    private sealed class InMemoryProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        private readonly Dictionary<Guid, Dictionary<string, ProjectLibraryReference>> _references = [];
        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Application.Persistence.ProjectLibraryReference> evaluatedValue27 = _references.TryGetValue(projectId, out var references) ? resolveToArrayResult() : [];
            var fromResultResult28 = Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(evaluatedValue27);
            return fromResultResult28;
            global::SereinFlow.Application.Persistence.ProjectLibraryReference[] resolveToArrayResult()
            {
                var orderByResult = references.Values.OrderBy(static item => item.LibraryId, StringComparer.OrdinalIgnoreCase);
                var toArrayResult26 = orderByResult.ToArray();
                return toArrayResult26;
            }
        }

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var evaluatedValue29 = _references.TryGetValue(projectId, out var references) && references.ContainsKey(libraryId);
            var fromResultResult30 = Task.FromResult(evaluatedValue29);
            return fromResultResult30;
        }

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

            var fromResultResult31 = Task.FromResult(reference);
            return fromResultResult31;
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var evaluatedValue32 = _references.TryGetValue(projectId, out var references) && references.Remove(libraryId);
            var fromResultResult33 = Task.FromResult(evaluatedValue32);
            return fromResultResult33;
        }
    }

    private sealed class ProductionHistoryVersionRepository(bool isReferenced) : IFlowVersionRepository
    {
        public Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default)
        {
            var fromResultResult34 = Task.FromResult<IReadOnlyList<FlowVersionSummaryDto>>([]);
            return fromResultResult34;
        }

        public Task<FlowVersionDetailDto?> FindVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default)
        {
            var fromResultResult35 = Task.FromResult<FlowVersionDetailDto?>(null);
            return fromResultResult35;
        }

        public Task<FlowDefinitionDto?> FindProductionDefinitionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult36 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult36;
        }

        public Task<long?> FindProductionVersionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult37 = Task.FromResult<long?>(null);
            return fromResultResult37;
        }

        public Task<FlowVersionMutationResult> PublishAsync(Guid projectId, Guid flowId, long expectedDevelopmentVersion, string? remark, long? expectedProductionVersion = null, CancellationToken cancellationToken = default)
        {
            var flowVersionMutationResult = new FlowVersionMutationResult(null, null);
            var fromResultResult38 = Task.FromResult(flowVersionMutationResult);
            return fromResultResult38;
        }

        public Task<FlowVersionMutationResult> RollbackAsync(Guid projectId, Guid flowId, long sourceVersion, FlowVersionTrackDto track, long expectedHeadVersion, CancellationToken cancellationToken = default)
        {
            var flowVersionMutationResult39 = new FlowVersionMutationResult(null, null);
            var fromResultResult40 = Task.FromResult(flowVersionMutationResult39);
            return fromResultResult40;
        }

        public Task<bool> IsLibraryReferencedByProductionHistoryAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult41 = Task.FromResult(isReferenced);
            return fromResultResult41;
        }
    }

    private sealed class InMemoryLibraryCatalogService(params LibraryDto[] libraries) : ILibraryCatalogService
    {
        private readonly Dictionary<string, LibraryDto> _libraries = libraries.ToDictionary(static library => library.Id, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<LibraryDto> List()
        {
            var whereResult42 = _libraries.Values.Where(static library => library.Lifecycle == LibraryLifecycleDto.Available);
            var toArrayResult43 = whereResult42.ToArray();
            return toArrayResult43;
        }

        public LibraryDto? Find(string libraryId)
        {
            var getValueOrDefaultResult44 = _libraries.GetValueOrDefault(libraryId);
            return getValueOrDefaultResult44;
        }

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var whereResult45 = _libraries.Values.Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available);
            var toArrayResult46 = whereResult45.ToArray();
            var fromResultResult47 = Task.FromResult<IReadOnlyList<LibraryDto>>(toArrayResult46);
            return fromResultResult47;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult48 = Find(libraryId);
            var fromResultResult49 = Task.FromResult(findResult48);
            return fromResultResult49;
        }

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool Delete(string libraryId)
        {
            var removeResult = _libraries.Remove(libraryId);
            return removeResult;
        }

        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            if (!_libraries.TryGetValue(libraryId, out var library))
            {
                var fromResultResult50 = Task.FromResult(false);
                return fromResultResult50;
            }

            _libraries[libraryId] = library with
            {
                Lifecycle = LibraryLifecycleDto.Archived
            };
            var fromResultResult51 = Task.FromResult(true);
            return fromResultResult51;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult52 = Find(libraryId);
            var fromResultResult53 = Task.FromResult(findResult52);
            return fromResultResult53;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult54 = Task.FromResult(0);
            return fromResultResult54;
        }
    }
}
