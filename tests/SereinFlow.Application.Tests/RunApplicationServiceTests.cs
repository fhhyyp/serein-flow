using System.Collections.Generic;
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
        var newGuidResult = Guid.NewGuid();
        var project = Project.Create("Debug validation", id: newGuidResult);
        var definition = CreateDefinition();
        var singleProjectRepository = new SingleProjectRepository(project);
        var singleFlowDefinitionRepository = new SingleFlowDefinitionRepository(project.Id, definition);
        var projectLibraryService = new ProjectLibraryService(null!, null!, null!, null!);
        var service = new RunApplicationService(singleProjectRepository, singleFlowDefinitionRepository, null!, projectLibraryService);
        var runFlowRequestDto = new RunFlowRequestDto(null, null, null, null, null);
        var newGuidResult1 = Guid.NewGuid();
        var result = await service.PrepareAsync(project.Id, definition.Id, runFlowRequestDto, FlowRunExecutionKind.Debug, newGuidResult1, ["missing-node"]);
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        var validation = Assert.IsType<FlowValidationResultDto>(result.ErrorBody);
        var diagnostic = Assert.Single(validation.Diagnostics);
        Assert.Equal(DebugErrorCodes.BreakpointNodeMissing, diagnostic.Code);
        Assert.Equal("breakpointNodeIds.missing-node", diagnostic.Path);
    }

    [Fact]
    public async Task LocalDebugDefinitionStillChecksThePersistedFlowVersion()
    {
        var newGuidResult2 = Guid.NewGuid();
        var project = Project.Create("Debug version check", id: newGuidResult2);
        var definition = CreateDefinition();
        var singleProjectRepository3 = new SingleProjectRepository(project);
        var singleFlowDefinitionRepository4 = new SingleFlowDefinitionRepository(project.Id, definition);
        var projectLibraryService5 = new ProjectLibraryService(null!, null!, null!, null!);
        var service = new RunApplicationService(singleProjectRepository3, singleFlowDefinitionRepository4, null!, projectLibraryService5);
        var localDefinition = definition with
        {
            EntryNodeId = "local-entry-node"
        };
        var runFlowRequestDto6 = new RunFlowRequestDto(2, null, null, null, null);
        var newGuidResult7 = Guid.NewGuid();
        var result = await service.PrepareAsync(project.Id, definition.Id, runFlowRequestDto6, FlowRunExecutionKind.Debug, newGuidResult7, definitionOverride: localDefinition);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(definition.Version, result.CurrentVersion);
    }

    [Fact]
    public async Task FormalRunsRejectDefinitionCandidates()
    {
        var newGuidResult8 = Guid.NewGuid();
        var project = Project.Create("Formal run candidate", id: newGuidResult8);
        var definition = CreateDefinition();
        var singleProjectRepository9 = new SingleProjectRepository(project);
        var singleFlowDefinitionRepository10 = new SingleFlowDefinitionRepository(project.Id, definition);
        var projectLibraryService11 = new ProjectLibraryService(null!, null!, null!, null!);
        var service = new RunApplicationService(singleProjectRepository9, singleFlowDefinitionRepository10, null!, projectLibraryService11);
        var runFlowRequestDto12 = new RunFlowRequestDto(definition.Version, null, null, null, null);
        var result = await service.PrepareAsync(project.Id, definition.Id, runFlowRequestDto12, FlowRunExecutionKind.Production, definitionOverride: definition with { EntryNodeId = "candidate-entry" });
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.NotNull(result.ErrorBody);
        var getTypeResult = result.ErrorBody!.GetType();
        var optionalReceiver = getTypeResult.GetProperty("code");
        object? optionalResult = default(object);
        if (optionalReceiver is not null)
        {
            optionalResult = optionalReceiver.GetValue(result.ErrorBody);
        }

        var evaluatedValue = optionalResult;
        Assert.Equal(RunErrorCodes.CandidateDefinitionNotAllowed, evaluatedValue);
    }

    private static FlowDefinitionDto CreateDefinition()
    {
        var node = new NodeDto("entry-node", NodeTypeDto.Action, "Entry", 0, 0, [], [], null);
        var newGuidResult13 = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue14 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var flowDefinitionDto = new FlowDefinitionDto(newGuidResult13, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue14, node.Id, "checksum", RunPolicy: flowRunPolicyDto);
        return flowDefinitionDto;
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
            var fromResultResult15 = Task.FromResult(id == project.Id ? project : null);
            return fromResultResult15;
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

    private sealed class SingleFlowDefinitionRepository(Guid projectId, FlowDefinitionDto definition) : IFlowDefinitionRepository
    {
        public Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult16 = Task.FromResult<IReadOnlyList<FlowDefinitionDto>>(id == projectId ? [definition] : []);
            return fromResultResult16;
        }

        public Task<FlowDefinitionDto?> FindAsync(Guid id, Guid flowId, CancellationToken cancellationToken = default)
        {
            var fromResultResult17 = Task.FromResult(id == projectId && flowId == definition.Id ? definition : null);
            return fromResultResult17;
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
}
