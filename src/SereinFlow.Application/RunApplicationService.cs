using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public sealed record RunPreparation(
    FlowRun Run,
    FlowDefinitionDto Definition,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs,
    DateTimeOffset Deadline,
    int MaxSteps,
    int MaxNodeVisits);

public sealed class RunApplicationService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowRunStore _runs;

    public RunApplicationService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        IFlowRunStore runs)
    {
        _projects = projects;
        _flows = flows;
        _runs = runs;
    }

    public async Task<RunPreparationResult> PrepareAsync(
        Guid projectId,
        Guid flowId,
        RunFlowRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return RunPreparationResult.ProjectNotFound;

        var definition = await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (definition is null)
            return RunPreparationResult.FlowNotFound;

        if (request.ExpectedFlowVersion is not null && request.ExpectedFlowVersion != definition.Version)
            return RunPreparationResult.VersionConflict(definition.Version);

        var validation = FlowDefinitionContractValidator.Validate(definition);
        if (!validation.IsValid)
            return RunPreparationResult.Invalid(validation);

        var timeout = Math.Clamp(request.TimeoutSeconds ?? 300, 1, 86_400);
        var maxSteps = Math.Clamp(request.MaxSteps ?? 10_000, 1, 1_000_000);
        var maxNodeVisits = Math.Clamp(request.MaxNodeVisits ?? 1_000, 1, 100_000);
        var run = FlowRun.Start(projectId, flowId, definition.Version, DateTimeOffset.UtcNow);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeout);
        await _runs.CreateWithSnapshotAsync(
            run,
            definition,
            new FlowRunExecutionOptions(request.ProjectInputs, deadline, maxSteps, maxNodeVisits),
            cancellationToken);
        return RunPreparationResult.Success(new RunPreparation(
            run,
            definition,
            request.ProjectInputs,
            deadline,
            maxSteps,
            maxNodeVisits));
    }
}

public sealed record RunPreparationResult(
    RunPreparation? Preparation,
    int StatusCode,
    string? ErrorTitle,
    object? ErrorBody = null,
    long? CurrentVersion = null)
{
    public bool IsSuccess => Preparation is not null;

    public static RunPreparationResult Success(RunPreparation preparation)
        => new(preparation, 202, null);

    public static RunPreparationResult ProjectNotFound { get; } = new(null, 404, "Project not found. 未找到项目。");

    public static RunPreparationResult FlowNotFound { get; } = new(null, 404, "Flow definition not found. 未找到流程定义。");

    public static RunPreparationResult VersionConflict(long currentVersion)
        => new(null, 409, "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。", null, currentVersion);

    public static RunPreparationResult Invalid(object validation)
        => new(null, 400, null, validation);
}
