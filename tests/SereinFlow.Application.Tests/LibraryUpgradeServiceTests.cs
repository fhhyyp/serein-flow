using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;
public sealed class LibraryUpgradeServiceTests
{
    private const string SourceArtifactId = "source-artifact";
    private const string TargetArtifactId = "target-artifact";
    private const string FamilyId = "test-family";
    [Fact]
    public async Task PreviewRejectsArchivedProject()
    {
        var fixture = CreateFixture();
        fixture.Project.Archive();
        global::SereinFlow.Contracts.LibraryUpgradePreviewRequestDto evaluatedValue = new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]);
        var result = await fixture.Service.PreviewAsync(fixture.Project.Id, evaluatedValue);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ProjectErrorCodes.Archived, result.Code);
    }

    [Fact]
    public async Task PreviewRejectsUnavailableTargetArtifact()
    {
        var fixture = CreateFixture(targetLifecycle: LibraryLifecycleDto.Archived);
        await fixture.References.AddAsync(fixture.Project.Id, SourceArtifactId);
        global::SereinFlow.Contracts.LibraryUpgradePreviewRequestDto evaluatedValue1 = new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]);
        var result = await fixture.Service.PreviewAsync(fixture.Project.Id, evaluatedValue1);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.Archived, result.Code);
    }

    [Fact]
    public async Task PreviewRejectsSourceArtifactNotReferencedByProject()
    {
        var fixture = CreateFixture();
        global::SereinFlow.Contracts.LibraryUpgradePreviewRequestDto evaluatedValue2 = new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]);
        var result = await fixture.Service.PreviewAsync(fixture.Project.Id, evaluatedValue2);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.UpgradeSourceNotReferenced, result.Code);
    }

    [Fact]
    public async Task PreviewRejectsFlowThatDoesNotUseSourceArtifact()
    {
        var fixture = CreateFixture(flowLibraryId: "other-artifact");
        await fixture.References.AddAsync(fixture.Project.Id, SourceArtifactId);
        global::SereinFlow.Contracts.LibraryUpgradePreviewRequestDto evaluatedValue3 = new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]);
        var result = await fixture.Service.PreviewAsync(fixture.Project.Id, evaluatedValue3);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.UpgradeFlowNotUsingSource, result.Code);
    }

    [Fact]
    public async Task ApplyRechecksSourceReferenceCreatedByPreview()
    {
        var fixture = CreateFixture();
        var newGuidResult = Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowLibraryUpgradePreviewDto> evaluatedValue4 = [new(fixture.Flow.Id, fixture.Flow.Version, true, 1, [])];
        var plan = new LibraryUpgradePlanDto(newGuidResult, fixture.Project.Id, SourceArtifactId, TargetArtifactId, LibraryUpgradePlanStatusDto.Analyzed, evaluatedValue4, DateTimeOffset.UtcNow);
        await fixture.Store.SavePlanAsync(plan);
        global::SereinFlow.Contracts.ApplyLibraryUpgradeRequestDto evaluatedValue5 = new(fixture.Flow.Id, fixture.Flow.Version);
        var result = await fixture.Service.ApplyAsync(fixture.Project.Id, plan.Id, evaluatedValue5);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.UpgradeSourceNotReferenced, result.Code);
    }

    private static UpgradeFixture CreateFixture(LibraryLifecycleDto targetLifecycle = LibraryLifecycleDto.Available, string? flowLibraryId = null)
    {
        var project = Project.Create("Library upgrade test project");
        var flow = CreateFlow(flowLibraryId ?? SourceArtifactId);
        var projects = new TestProjectRepository(project);
        var flows = new TestFlowRepository(project.Id, flow);
        var references = new TestProjectLibraryReferenceRepository();
        var store = new TestUpgradeStore();
        var createLibraryResult = CreateLibrary(SourceArtifactId);
        var createLibraryResult6 = CreateLibrary(TargetArtifactId, targetLifecycle);
        var testLibraryCatalog = new TestLibraryCatalog(createLibraryResult, createLibraryResult6);
        var libraryCompatibilityAnalyzer = new LibraryCompatibilityAnalyzer();
        var service = new LibraryUpgradeService(projects, flows, references, testLibraryCatalog, libraryCompatibilityAnalyzer, store);
        global::SereinFlow.Application.Tests.LibraryUpgradeServiceTests.UpgradeFixture evaluatedValue7 = new(project, flow, references, store, service);
        return evaluatedValue7;
    }

    private static LibraryDto CreateLibrary(string id, LibraryLifecycleDto lifecycle = LibraryLifecycleDto.Available)
    {
        global::SereinFlow.Contracts.LibraryDto evaluatedValue8 = new(id, id, "1.0.0", $"{id}.zip", 1, id, DateTimeOffset.UtcNow, [], lifecycle, FamilyId, "1.0.0");
        return evaluatedValue8;
    }

    private static FlowDefinitionDto CreateFlow(string libraryId)
    {
        var nodeUiMetadataDto = new NodeUiMetadataDto("library-action", "library-action", string.Empty, null, "ready", true, null, LibraryId: libraryId);
        var node = new NodeDto("library-node", NodeTypeDto.Action, "Library action", 0, 0, [], [], null, nodeUiMetadataDto);
        var newGuidResult9 = Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.CanvasDto> evaluatedValue10 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowDefinitionDto = new FlowDefinitionDto(newGuidResult9, 1, 1, evaluatedValue10, node.Id, "checksum");
        return flowDefinitionDto;
    }

    private sealed record UpgradeFixture(Project Project, FlowDefinitionDto Flow, TestProjectLibraryReferenceRepository References, TestUpgradeStore Store, LibraryUpgradeService Service);
    private sealed class TestProjectRepository(params Project[] projects) : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = projects.ToDictionary(static item => item.Id);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            var listResult = List();
            var fromResultResult = Task.FromResult(listResult);
            return fromResultResult;
        }

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var findResult = Find(id);
            var fromResultResult11 = Task.FromResult(findResult);
            return fromResultResult11;
        }

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            Add(project);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var tryUpdateResult = TryUpdate(project, expectedVersion);
            var fromResultResult12 = Task.FromResult(tryUpdateResult);
            return fromResultResult12;
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

    private sealed class TestFlowRepository(Guid projectId, FlowDefinitionDto flow) : IFlowDefinitionRepository
    {
        private readonly Dictionary<(Guid ProjectId, Guid FlowId), FlowDefinitionDto> _flows = new()
        {
            [(projectId, flow.Id)] = flow
        };
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var listByProjectResult = ListByProject(id);
            var fromResultResult13 = Task.FromResult(listByProjectResult);
            return fromResultResult13;
        }

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
        {
            var findResult14 = Find(id, flowId);
            var fromResultResult15 = Task.FromResult(findResult14);
            return fromResultResult15;
        }

        public Task AddAsync(Guid id, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        {
            Add(id, definition);
            return Task.CompletedTask;
        }

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var tryUpdateResult16 = TryUpdate(id, definition, expectedVersion);
            var fromResultResult17 = Task.FromResult(tryUpdateResult16);
            return fromResultResult17;
        }

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id)
        {
            var whereResult = _flows.Where(item => item.Key.ProjectId == id);
            var selectResult = whereResult.Select(static item => item.Value);
            var toArrayResult18 = selectResult.ToArray();
            return toArrayResult18;
        }

        public FlowDefinitionDto? Find(Guid id, Guid flowId)
        {
            var getValueOrDefaultResult19 = _flows.GetValueOrDefault((id, flowId));
            return getValueOrDefaultResult19;
        }

        public void Add(Guid id, FlowDefinitionDto definition) => _flows[(id, definition.Id)] = definition;
        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto definition, long expectedVersion)
        {
            var current = Find(id, definition.Id);
            if (current is null || current.Version != expectedVersion)
            {
                return null;
            }

            _flows[(id, definition.Id)] = definition;
            return definition;
        }
    }

    private sealed class TestProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        private readonly Dictionary<Guid, Dictionary<string, ProjectLibraryReference>> _items = [];
        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Application.Persistence.ProjectLibraryReference> evaluatedValue20 = _items.TryGetValue(projectId, out var references) ? references.Values.ToArray() : [];
            var fromResultResult21 = Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(evaluatedValue20);
            return fromResultResult21;
        }

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var evaluatedValue22 = _items.TryGetValue(projectId, out var references) && references.ContainsKey(libraryId);
            var fromResultResult23 = Task.FromResult(evaluatedValue22);
            return fromResultResult23;
        }

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            if (!_items.TryGetValue(projectId, out var references))
            {
                references = new(StringComparer.OrdinalIgnoreCase);
                _items[projectId] = references;
            }

            if (!references.TryGetValue(libraryId, out var reference))
            {
                reference = new(projectId, libraryId, DateTimeOffset.UtcNow);
                references[libraryId] = reference;
            }

            var fromResultResult24 = Task.FromResult(reference);
            return fromResultResult24;
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var evaluatedValue25 = _items.TryGetValue(projectId, out var references) && references.Remove(libraryId);
            var fromResultResult26 = Task.FromResult(evaluatedValue25);
            return fromResultResult26;
        }
    }

    private sealed class TestLibraryCatalog(params LibraryDto[] libraries) : ILibraryCatalogService
    {
        private readonly Dictionary<string, LibraryDto> _libraries = libraries.ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<LibraryDto> List()
        {
            var toArrayResult27 = _libraries.Values.ToArray();
            return toArrayResult27;
        }

        public LibraryDto? Find(string libraryId)
        {
            var getValueOrDefaultResult28 = _libraries.GetValueOrDefault(libraryId);
            return getValueOrDefaultResult28;
        }

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var toArrayResult29 = _libraries.Values.ToArray();
            var fromResultResult30 = Task.FromResult<IReadOnlyList<LibraryDto>>(toArrayResult29);
            return fromResultResult30;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult31 = Find(libraryId);
            var fromResultResult32 = Task.FromResult(findResult31);
            return fromResultResult32;
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
            var fromResultResult33 = Task.FromResult(false);
            return fromResultResult33;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult34 = Find(libraryId);
            var fromResultResult35 = Task.FromResult(findResult34);
            return fromResultResult35;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult36 = Task.FromResult(0);
            return fromResultResult36;
        }
    }

    private sealed class TestUpgradeStore : IFlowLibraryUpgradeStore
    {
        private readonly Dictionary<(Guid ProjectId, Guid PlanId), LibraryUpgradePlanDto> _plans = [];
        public Task<LibraryUpgradePlanDto> SavePlanAsync(LibraryUpgradePlanDto plan, CancellationToken cancellationToken = default)
        {
            _plans[(plan.ProjectId, plan.Id)] = plan;
            var fromResultResult37 = Task.FromResult(plan);
            return fromResultResult37;
        }

        public Task<LibraryUpgradePlanDto?> FindPlanAsync(Guid projectId, Guid planId, CancellationToken cancellationToken = default)
        {
            var getValueOrDefaultResult38 = _plans.GetValueOrDefault((projectId, planId));
            var fromResultResult39 = Task.FromResult(getValueOrDefaultResult38);
            return fromResultResult39;
        }

        public Task<FlowLibraryUpgradeCommitResult> CommitAsync(Guid projectId, Guid planId, FlowDefinitionDto upgradedDefinition, long expectedVersion, string targetArtifactId, LibraryUpgradePlanFlowResultDto? appliedFlow = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
