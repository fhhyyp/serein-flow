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
        var createInterfaceResult = CreateInterface(project.Id);
        var interfaces = new InMemoryFlowInterfaceRepository(createInterfaceResult);
        var service = new ProjectArchiveService(projects, interfaces);
        var result = await service.ArchiveAsync(project.Id);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ProjectErrorCodes.ArchiveEnvironmentInterfaceExists, result.Code);
        var evaluatedValue = (await projects.FindAsync(project.Id))!.Status;
        Assert.NotEqual(ProjectStatus.Archived, evaluatedValue);
    }

    [Fact]
    public async Task ArchivePersistsProjectWhenNoEnvironmentInterfaceReferencesIt()
    {
        var project = Project.Create("Private project");
        var projects = new InMemoryProjectRepository(project);
        var inMemoryFlowInterfaceRepository = new InMemoryFlowInterfaceRepository();
        var service = new ProjectArchiveService(projects, inMemoryFlowInterfaceRepository);
        var result = await service.ArchiveAsync(project.Id);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(ProjectStatus.Archived, result.Project!.Status);
        var evaluatedValue1 = (await projects.FindAsync(project.Id))!.Status;
        Assert.Equal(ProjectStatus.Archived, evaluatedValue1);
    }

    private static FlowInterfaceDto CreateInterface(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        var newGuidResult = Guid.NewGuid();
        var newGuidResult2 = Guid.NewGuid();
        var flowInterfaceDto = new FlowInterfaceDto(newGuidResult, projectId, newGuidResult2, "Published", FlowInvocationModeDto.Asynchronous, true, now, now);
        return flowInterfaceDto;
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects;
        public InMemoryProjectRepository(params Project[] projects)
        {
            _projects = projects.ToDictionary(static project => project.Id, Clone);
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
            var fromResultResult3 = Task.FromResult(findResult);
            return fromResultResult3;
        }

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        {
            _projects[project.Id] = Clone(project);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var tryUpdateResult = TryUpdate(project, expectedVersion);
            var fromResultResult4 = Task.FromResult(tryUpdateResult);
            return fromResultResult4;
        }

        public IReadOnlyList<Project> List()
        {
            var selectResult = _projects.Values.Select(Clone);
            var toArrayResult = selectResult.ToArray();
            return toArrayResult;
        }

        public Project? Find(Guid id)
        {
            global::SereinFlow.Domain.Project? evaluatedValue5 = _projects.TryGetValue(id, out var project) ? Clone(project) : null;
            return evaluatedValue5;
        }

        public void Add(Project project)
        {
            _projects[project.Id] = Clone(project);
        }

        public bool TryUpdate(Project project, long expectedVersion)
        {
            if (!_projects.TryGetValue(project.Id, out var existing) || existing.Version != expectedVersion)
            {
                return false;
            }

            _projects[project.Id] = Clone(project);
            return true;
        }

        private static Project Clone(Project project)
        {
            var rehydrateResult = Project.Rehydrate(project.Id, project.Name, project.Version, project.Status, project.CreatedAt, project.UpdatedAt);
            return rehydrateResult;
        }
    }

    private sealed class InMemoryFlowInterfaceRepository(params FlowInterfaceDto[] interfaces) : IFlowInterfaceRepository
    {
        private readonly Dictionary<Guid, FlowInterfaceDto> _interfaces = interfaces.ToDictionary(static item => item.Id);
        public Task<IReadOnlyList<FlowInterfaceDto>> ListAsync(CancellationToken cancellationToken = default)
        {
            var toArrayResult6 = _interfaces.Values.ToArray();
            var fromResultResult7 = Task.FromResult<IReadOnlyList<FlowInterfaceDto>>(toArrayResult6);
            return fromResultResult7;
        }

        public Task<FlowInterfaceDto?> FindAsync(Guid interfaceId, CancellationToken cancellationToken = default)
        {
            var getValueOrDefaultResult = _interfaces.GetValueOrDefault(interfaceId);
            var fromResultResult8 = Task.FromResult(getValueOrDefaultResult);
            return fromResultResult8;
        }

        public Task<FlowInterfaceDto> AddAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
        {
            _interfaces[flowInterface.Id] = flowInterface;
            var fromResultResult9 = Task.FromResult(flowInterface);
            return fromResultResult9;
        }

        public Task<bool> UpdateAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
        {
            if (!_interfaces.ContainsKey(flowInterface.Id))
            {
                var fromResultResult10 = Task.FromResult(false);
                return fromResultResult10;
            }

            _interfaces[flowInterface.Id] = flowInterface;
            var fromResultResult11 = Task.FromResult(true);
            return fromResultResult11;
        }

        public Task<bool> DeleteAsync(Guid interfaceId, CancellationToken cancellationToken = default)
        {
            var removeResult = _interfaces.Remove(interfaceId);
            var fromResultResult12 = Task.FromResult(removeResult);
            return fromResultResult12;
        }
    }
}
