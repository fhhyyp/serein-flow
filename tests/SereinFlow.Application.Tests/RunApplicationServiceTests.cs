using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class RunApplicationServiceTests
{
    [Fact]
    public async Task DebugBreakpointsMustExistInSavedFlowSnapshot()
    {
        var project = Project.Create("Debug validation", id: Guid.NewGuid());
        var definition = CreateDefinition();
        var service = new RunApplicationService(
            new SingleProjectRepository(project),
            new SingleFlowDefinitionRepository(project.Id, definition),
            null!,
            new ProjectLibraryService(null!, null!, null!, null!));

        var result = await service.PrepareAsync(
            project.Id,
            definition.Id,
            new RunFlowRequestDto(null, null, null, null, null),
            FlowRunExecutionKind.Debug,
            Guid.NewGuid(),
            ["missing-node"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        var validation = Assert.IsType<FlowValidationResultDto>(result.ErrorBody);
        var diagnostic = Assert.Single(validation.Diagnostics);
        Assert.Equal("debug.breakpoint_node_missing", diagnostic.Code);
        Assert.Equal("breakpointNodeIds.missing-node", diagnostic.Path);
    }

    [Fact]
    public async Task LocalDebugDefinitionStillChecksThePersistedFlowVersion()
    {
        var project = Project.Create("Debug version check", id: Guid.NewGuid());
        var definition = CreateDefinition();
        var service = new RunApplicationService(
            new SingleProjectRepository(project),
            new SingleFlowDefinitionRepository(project.Id, definition),
            null!,
            new ProjectLibraryService(null!, null!, null!, null!));

        var localDefinition = definition with { EntryNodeId = "local-entry-node" };
        var result = await service.PrepareAsync(
            project.Id,
            definition.Id,
            new RunFlowRequestDto(2, null, null, null, null),
            FlowRunExecutionKind.Debug,
            Guid.NewGuid(),
            definitionOverride: localDefinition);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(definition.Version, result.CurrentVersion);
    }

    private static FlowDefinitionDto CreateDefinition()
    {
        var node = new NodeDto(
            "entry-node",
            NodeTypeDto.Action,
            "Entry",
            0,
            0,
            [],
            [],
            null);
        return new FlowDefinitionDto(
            Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "checksum",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
    }

    private sealed class SingleProjectRepository(Project project) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([project]);

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == project.Id ? project : null);

        public Task AddAsync(Project value, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> TryUpdateAsync(Project value, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IReadOnlyList<Project> List() => [project];

        public Project? Find(Guid id) => id == project.Id ? project : null;

        public void Add(Project value) => throw new NotSupportedException();

        public bool TryUpdate(Project value, long expectedVersion) => throw new NotSupportedException();
    }

    private sealed class SingleFlowDefinitionRepository(Guid projectId, FlowDefinitionDto definition) : IFlowDefinitionRepository
    {
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(id == projectId ? [definition] : []);

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
            => Task.FromResult(id == projectId && flowId == definition.Id ? definition : null);

        public Task AddAsync(Guid id, FlowDefinitionDto value, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FlowDefinitionDto?> TryUpdateAsync(Guid id, FlowDefinitionDto value, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid id) => id == projectId ? [definition] : [];

        public FlowDefinitionDto? Find(Guid id, Guid flowId) => id == projectId && flowId == definition.Id ? definition : null;

        public void Add(Guid id, FlowDefinitionDto value) => throw new NotSupportedException();

        public FlowDefinitionDto? TryUpdate(Guid id, FlowDefinitionDto value, long expectedVersion) => throw new NotSupportedException();
    }
}
