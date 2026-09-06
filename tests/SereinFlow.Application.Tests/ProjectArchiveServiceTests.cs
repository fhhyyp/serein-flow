using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class ProjectArchiveServiceTests
{
    [Fact]
    public async Task ArchiveRejectsAProjectReferencedByAnEnvironmentInterface()
    {
        var project = Project.Create("Published project");
        var projects = new InMemoryProjectRepository(project);
        var interfaces = new InMemoryFlowInterfaceRepository(CreateInterface(project.Id));
        var service = new ProjectArchiveService(projects, interfaces);

        var result = await service.ArchiveAsync(project.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ProjectErrorCodes.ArchiveEnvironmentInterfaceExists, result.Code);
        Assert.NotEqual(ProjectStatus.Archived, (await projects.FindAsync(project.Id))!.Status);
    }

    [Fact]
    public async Task ArchivePersistsProjectWhenNoEnvironmentInterfaceReferencesIt()
    {
        var project = Project.Create("Private project");
        var projects = new InMemoryProjectRepository(project);
        var service = new ProjectArchiveService(projects, new InMemoryFlowInterfaceRepository());

        var result = await service.ArchiveAsync(project.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(ProjectStatus.Archived, result.Project!.Status);
        Assert.Equal(ProjectStatus.Archived, (await projects.FindAsync(project.Id))!.Status);
    }

    private static FlowInterfaceDto CreateInterface(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        return new FlowInterfaceDto(
            Guid.NewGuid(),
            projectId,
            Guid.NewGuid(),
            "Published",
            FlowInvocationModeDto.Asynchronous,
            true,
            now,
            now);
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects;

        public InMemoryProjectRepository(params Project[] projects)
            => _projects = projects.ToDictionary(static project => project.Id, Clone);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>(List());

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(id));

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            _projects[project.Id] = Clone(project);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(TryUpdate(project, expectedVersion));

        public IReadOnlyList<Project> List()
            => _projects.Values.Select(Clone).ToArray();

        public Project? Find(Guid id)
            => _projects.TryGetValue(id, out var project) ? Clone(project) : null;

        public void Add(Project project)
            => _projects[project.Id] = Clone(project);

        public bool TryUpdate(Project project, long expectedVersion)
        {
            if (!_projects.TryGetValue(project.Id, out var existing) || existing.Version != expectedVersion)
                return false;
            _projects[project.Id] = Clone(project);
            return true;
        }

        private static Project Clone(Project project)
            => Project.Rehydrate(
                project.Id,
                project.Name,
                project.Version,
                project.Status,
                project.CreatedAt,
                project.UpdatedAt);
    }

    private sealed class InMemoryFlowInterfaceRepository(params FlowInterfaceDto[] interfaces) : IFlowInterfaceRepository
    {
        private readonly Dictionary<Guid, FlowInterfaceDto> _interfaces = interfaces.ToDictionary(static item => item.Id);

        public Task<IReadOnlyList<FlowInterfaceDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowInterfaceDto>>(_interfaces.Values.ToArray());

        public Task<FlowInterfaceDto?> FindAsync(Guid interfaceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_interfaces.GetValueOrDefault(interfaceId));

        public Task<FlowInterfaceDto> AddAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
        {
            _interfaces[flowInterface.Id] = flowInterface;
            return Task.FromResult(flowInterface);
        }

        public Task<bool> UpdateAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
        {
            if (!_interfaces.ContainsKey(flowInterface.Id))
                return Task.FromResult(false);
            _interfaces[flowInterface.Id] = flowInterface;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid interfaceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_interfaces.Remove(interfaceId));
    }
}
