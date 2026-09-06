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

        var result = await fixture.Service.PreviewAsync(
            fixture.Project.Id,
            new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ProjectErrorCodes.Archived, result.Code);
    }

    [Fact]
    public async Task PreviewRejectsUnavailableTargetArtifact()
    {
        var fixture = CreateFixture(targetLifecycle: LibraryLifecycleDto.Archived);
        await fixture.References.AddAsync(fixture.Project.Id, SourceArtifactId);

        var result = await fixture.Service.PreviewAsync(
            fixture.Project.Id,
            new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.Archived, result.Code);
    }

    [Fact]
    public async Task PreviewRejectsSourceArtifactNotReferencedByProject()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.PreviewAsync(
            fixture.Project.Id,
            new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.UpgradeSourceNotReferenced, result.Code);
    }

    [Fact]
    public async Task PreviewRejectsFlowThatDoesNotUseSourceArtifact()
    {
        var fixture = CreateFixture(flowLibraryId: "other-artifact");
        await fixture.References.AddAsync(fixture.Project.Id, SourceArtifactId);

        var result = await fixture.Service.PreviewAsync(
            fixture.Project.Id,
            new(SourceArtifactId, TargetArtifactId, [fixture.Flow.Id]));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.UpgradeFlowNotUsingSource, result.Code);
    }

    [Fact]
    public async Task ApplyRechecksSourceReferenceCreatedByPreview()
    {
        var fixture = CreateFixture();
        var plan = new LibraryUpgradePlanDto(
            Guid.NewGuid(),
            fixture.Project.Id,
            SourceArtifactId,
            TargetArtifactId,
            LibraryUpgradePlanStatusDto.Analyzed,
            [new(fixture.Flow.Id, fixture.Flow.Version, true, 1, [])],
            DateTimeOffset.UtcNow);
        await fixture.Store.SavePlanAsync(plan);

        var result = await fixture.Service.ApplyAsync(
            fixture.Project.Id,
            plan.Id,
            new(fixture.Flow.Id, fixture.Flow.Version));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(LibraryErrorCodes.UpgradeSourceNotReferenced, result.Code);
    }

    private static UpgradeFixture CreateFixture(
        LibraryLifecycleDto targetLifecycle = LibraryLifecycleDto.Available,
        string? flowLibraryId = null)
    {
        var project = Project.Create("Library upgrade test project");
        var flow = CreateFlow(flowLibraryId ?? SourceArtifactId);
        var projects = new TestProjectRepository(project);
        var flows = new TestFlowRepository(project.Id, flow);
        var references = new TestProjectLibraryReferenceRepository();
        var store = new TestUpgradeStore();
        var service = new LibraryUpgradeService(
            projects,
            flows,
            references,
            new TestLibraryCatalog(CreateLibrary(SourceArtifactId), CreateLibrary(TargetArtifactId, targetLifecycle)),
            new LibraryCompatibilityAnalyzer(),
            store);
        return new(project, flow, references, store, service);
    }

    private static LibraryDto CreateLibrary(string id, LibraryLifecycleDto lifecycle = LibraryLifecycleDto.Available)
        => new(
            id,
            id,
            "1.0.0",
            $"{id}.zip",
            1,
            id,
            DateTimeOffset.UtcNow,
            [],
            lifecycle,
            FamilyId,
            "1.0.0");

    private static FlowDefinitionDto CreateFlow(string libraryId)
    {
        var node = new NodeDto(
            "library-node",
            NodeTypeDto.Action,
            "Library action",
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                "library-action",
                "library-action",
                string.Empty,
                null,
                "ready",
                true,
                null,
                LibraryId: libraryId));
        return new FlowDefinitionDto(
            Guid.NewGuid(),
            1,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "checksum");
    }

    private sealed record UpgradeFixture(
        Project Project,
        FlowDefinitionDto Flow,
        TestProjectLibraryReferenceRepository References,
        TestUpgradeStore Store,
        LibraryUpgradeService Service);

    private sealed class TestProjectRepository(params Project[] projects) : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = projects.ToDictionary(static item => item.Id);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(List());

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(id));

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            Add(project);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(TryUpdate(project, expectedVersion));

        public IReadOnlyList<Project> List() => _projects.Values.ToArray();

        public Project? Find(Guid id) => _projects.GetValueOrDefault(id);

        public void Add(Project project) => _projects.Add(project.Id, project);

        public bool TryUpdate(Project project, long expectedVersion)
        {
            if (!_projects.TryGetValue(project.Id, out var current) || current.Version != expectedVersion)
                return false;
            _projects[project.Id] = project;
            return true;
        }
    }

    private sealed class TestFlowRepository(Guid projectId, FlowDefinitionDto flow) : IFlowDefinitionRepository
    {
        private readonly Dictionary<(Guid ProjectId, Guid FlowId), FlowDefinitionDto> _flows =
            new() { [(projectId, flow.Id)] = flow };

        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ListByProject(id));

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(id, flowId));

        public Task AddAsync(Guid id, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        {
            Add(id, definition);
            return Task.CompletedTask;
        }

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(TryUpdate(id, definition, expectedVersion));

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id)
            => _flows.Where(item => item.Key.ProjectId == id).Select(static item => item.Value).ToArray();

        public FlowDefinitionDto? Find(Guid id, Guid flowId) => _flows.GetValueOrDefault((id, flowId));

        public void Add(Guid id, FlowDefinitionDto definition) => _flows[(id, definition.Id)] = definition;

        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto definition, long expectedVersion)
        {
            var current = Find(id, definition.Id);
            if (current is null || current.Version != expectedVersion)
                return null;
            _flows[(id, definition.Id)] = definition;
            return definition;
        }
    }

    private sealed class TestProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        private readonly Dictionary<Guid, Dictionary<string, ProjectLibraryReference>> _items = [];

        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(
                _items.TryGetValue(projectId, out var references) ? references.Values.ToArray() : []);

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.TryGetValue(projectId, out var references) && references.ContainsKey(libraryId));

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
            return Task.FromResult(reference);
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.TryGetValue(projectId, out var references) && references.Remove(libraryId));
    }

    private sealed class TestLibraryCatalog(params LibraryDto[] libraries) : ILibraryCatalogService
    {
        private readonly Dictionary<string, LibraryDto> _libraries = libraries.ToDictionary(
            static item => item.Id,
            StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<LibraryDto> List() => _libraries.Values.ToArray();

        public LibraryDto? Find(string libraryId) => _libraries.GetValueOrDefault(libraryId);

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LibraryDto>>(_libraries.Values.ToArray());

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(libraryId));

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool Delete(string libraryId) => _libraries.Remove(libraryId);

        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(libraryId));

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class TestUpgradeStore : IFlowLibraryUpgradeStore
    {
        private readonly Dictionary<(Guid ProjectId, Guid PlanId), LibraryUpgradePlanDto> _plans = [];

        public Task<LibraryUpgradePlanDto> SavePlanAsync(LibraryUpgradePlanDto plan, CancellationToken cancellationToken = default)
        {
            _plans[(plan.ProjectId, plan.Id)] = plan;
            return Task.FromResult(plan);
        }

        public Task<LibraryUpgradePlanDto?> FindPlanAsync(Guid projectId, Guid planId, CancellationToken cancellationToken = default)
            => Task.FromResult(_plans.GetValueOrDefault((projectId, planId)));

        public Task<FlowLibraryUpgradeCommitResult> CommitAsync(
            Guid projectId,
            Guid planId,
            FlowDefinitionDto upgradedDefinition,
            long expectedVersion,
            string targetArtifactId,
            LibraryUpgradePlanFlowResultDto? appliedFlow = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
