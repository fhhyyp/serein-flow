using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public sealed record RunPreparation(
    FlowRun Run,
    FlowDefinitionDto Definition,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs,
    int TimeoutSeconds,
    int MaxSteps,
    int MaxNodeVisits,
    bool IsListenerRun);

public sealed class RunApplicationService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowRunStore _runs;
    private readonly ProjectLibraryService _projectLibraries;

    public RunApplicationService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        IFlowRunStore runs,
        ProjectLibraryService projectLibraries)
    {
        _projects = projects;
        _flows = flows;
        _runs = runs;
        _projectLibraries = projectLibraries;
    }

    public async Task<RunPreparationResult> PrepareAsync(
        Guid projectId,
        Guid flowId,
        RunFlowRequestDto request,
        FlowRunExecutionKind executionKind = FlowRunExecutionKind.Production,
        Guid? debugSessionId = null,
        IReadOnlyCollection<string>? breakpointNodeIds = null,
        FlowDefinitionDto? definitionOverride = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
            return RunPreparationResult.ProjectNotFound;
        if (project.Status == ProjectStatus.Archived)
            return RunPreparationResult.ProjectArchived;

        if (definitionOverride is not null && definitionOverride.Id != flowId)
            return RunPreparationResult.FlowNotFound;

        var persistedDefinition = definitionOverride is null || request.ExpectedFlowVersion is not null
            ? await _flows.FindAsync(projectId, flowId, cancellationToken)
            : null;
        if (definitionOverride is null && persistedDefinition is null)
            return RunPreparationResult.FlowNotFound;

        if (request.ExpectedFlowVersion is not null && persistedDefinition is null)
            return RunPreparationResult.FlowNotFound;
        if (request.ExpectedFlowVersion is not null && request.ExpectedFlowVersion != persistedDefinition!.Version)
            return RunPreparationResult.VersionConflict(persistedDefinition!.Version);

        var definition = definitionOverride ?? persistedDefinition!;

        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        if (!validation.IsValid)
            return RunPreparationResult.Invalid(validation);

        var unknownBreakpoints = (breakpointNodeIds ?? [])
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Except(
                definition.Canvases.SelectMany(static canvas => canvas.Nodes).Select(static node => node.Id),
                StringComparer.Ordinal)
            .OrderBy(static nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        if (unknownBreakpoints.Length > 0)
        {
            return RunPreparationResult.Invalid(new FlowValidationResultDto(
                false,
                unknownBreakpoints.Select(nodeId => new ValidationDiagnosticDto(
                    DebugErrorCodes.BreakpointNodeMissing,
                    $"Breakpoint node '{nodeId}' does not exist in the saved flow. 断点节点“{nodeId}”不存在于已保存流程中。",
                    $"breakpointNodeIds.{nodeId}"))
                    .ToArray()));
        }

        // The source hash is derived data. Persist the canonical value into the
        // immutable run snapshot so its displayed hash always matches its source.
        // 源代码哈希属于派生数据；写入不可变运行快照前统一规范化，确保展示的哈希始终与源代码一致。
        definition = FlowDefinitionContractNormalizer.NormalizeForExecution(definition);

        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        if (!libraryValidation.IsValid)
            return RunPreparationResult.Invalid(libraryValidation);

        var timeout = Math.Clamp(request.TimeoutSeconds ?? 300, 1, 86_400);
        var maxSteps = Math.Clamp(request.MaxSteps ?? 10_000, 1, 1_000_000);
        var maxNodeVisits = Math.Clamp(request.MaxNodeVisits ?? 1_000, 1, 100_000);
        var createdAt = DateTimeOffset.UtcNow;
        var isListenerRun = IsListenerRun(definition);
        var concurrencyMode = (FlowConcurrencyMode)definition.RunPolicy!.ConcurrencyMode;
        var run = FlowRun.Start(
            projectId,
            flowId,
            definition.Version,
            createdAt,
            concurrencyMode,
            isListenerRun,
            executionKind: executionKind,
            debugSessionId: debugSessionId);
        // The execution deadline is bound when the scheduler actually obtains
        // a Worker slot. Before that, QueueWaitTimeoutSeconds governs waiting.
        // 实际执行截止时间在调度器取得 Worker 槽位时绑定；排队等待由独立超时控制。
        var admission = await _runs.TryCreateWithSnapshotAsync(
            run,
            definition,
            new FlowRunExecutionOptions(request.ProjectInputs, timeout, maxSteps, maxNodeVisits),
            cancellationToken);
        if (!admission.IsAdmitted)
            return RunPreparationResult.AlreadyActive(admission.ActiveRunId);
        return RunPreparationResult.Success(new RunPreparation(
            admission.Run!,
            definition,
            request.ProjectInputs,
            timeout,
            maxSteps,
            maxNodeVisits,
            isListenerRun));
    }

    private static bool IsListenerRun(FlowDefinitionDto definition)
    {
        var incomingExecutionNodes = definition.Canvases
            .SelectMany(static canvas => canvas.Connections)
            .Where(static connection => connection.Kind == ConnectionKindDto.Execution)
            .Select(static connection => connection.ToNodeId)
            .ToHashSet(StringComparer.Ordinal);
        return definition.Canvases
            .SelectMany(static canvas => canvas.Nodes)
            .Any(node => node.Type == NodeTypeDto.Flipflop && !incomingExecutionNodes.Contains(node.Id));
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

    public static RunPreparationResult ProjectArchived { get; } = new(
        null,
        409,
        "Archived projects cannot start new runs. 已归档项目不能启动新的运行实例。",
        new
        {
            code = ProjectErrorCodes.Archived,
            message = "Archived projects cannot start new runs. 已归档项目不能启动新的运行实例。"
        });

    public static RunPreparationResult FlowNotFound { get; } = new(null, 404, "Flow definition not found. 未找到流程定义。");

    public static RunPreparationResult VersionConflict(long currentVersion)
        => new(null, 409, "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。", null, currentVersion);

    public static RunPreparationResult Invalid(object validation)
        => new(null, 400, null, validation);

    public static RunPreparationResult AlreadyActive(Guid? activeRunId)
        => new(
            null,
            409,
            "The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。",
            new
            {
                code = FlowErrorCodes.RunAlreadyActive,
                message = "The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。",
                activeRunId
            });
}
