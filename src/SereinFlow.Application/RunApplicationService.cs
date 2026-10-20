using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;
public sealed record RunPreparation(FlowRun Run, FlowDefinitionDto Definition, IReadOnlyDictionary<string, JsonElement>? ProjectInputs, int TimeoutSeconds, int MaxSteps, int MaxNodeVisits, bool IsListenerRun);
public sealed class RunApplicationService
{
    private const int MinimumExecutionLimit = 1;
    private const int DefaultRunTimeoutSeconds = 300;
    private const int MaximumRunTimeoutSeconds = 86_400;
    private const int DefaultMaximumSteps = 10_000;
    private const int MaximumAllowedSteps = 1_000_000;
    private const int DefaultMaximumNodeVisits = 1_000;
    private const int MaximumAllowedNodeVisits = 100_000;
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowVersionRepository? _versions;
    private readonly IFlowRunStore _runs;
    private readonly ProjectLibraryService _projectLibraries;
    public RunApplicationService(IProjectRepository projects, IFlowDefinitionRepository flows, IFlowRunStore runs, ProjectLibraryService projectLibraries, IFlowVersionRepository? versions = null)
    {
        _projects = projects;
        _flows = flows;
        _versions = versions;
        _runs = runs;
        _projectLibraries = projectLibraries;
    }

    public async Task<RunPreparationResult> PrepareAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, FlowRunExecutionKind executionKind = FlowRunExecutionKind.Production, Guid? debugSessionId = null, IReadOnlyCollection<string>? breakpointNodeIds = null, FlowDefinitionDto? definitionOverride = null, CancellationToken cancellationToken = default, FlowVersionTrackDto definitionTrack = FlowVersionTrackDto.Development)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return RunPreparationResult.ProjectNotFound;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            return RunPreparationResult.ProjectArchived;
        }

        if (definitionOverride is not null && definitionOverride.Id != flowId)
        {
            return RunPreparationResult.FlowNotFound;
        }

        var persistedDefinition = definitionTrack == FlowVersionTrackDto.Production ? _versions is null ? null : await _versions.FindProductionDefinitionAsync(projectId, flowId, cancellationToken) : await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (persistedDefinition is null)
        {
            return RunPreparationResult.FlowNotFound;
        }

        if (request.ExpectedFlowVersion is not null && request.ExpectedFlowVersion != persistedDefinition!.Version)
        {
            var versionConflictResult = RunPreparationResult.VersionConflict(persistedDefinition!.Version);
            return versionConflictResult;
        }

        if (definitionOverride is not null)
        {
            if (executionKind != FlowRunExecutionKind.Debug)
            {
                return RunPreparationResult.CandidateDefinitionNotAllowed;
            }

            if (definitionOverride.Version != persistedDefinition.Version)
            {
                var versionConflictResult1 = RunPreparationResult.VersionConflict(persistedDefinition.Version);
                return versionConflictResult1;
            }
        }

        var definition = definitionOverride ?? persistedDefinition!;
        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        if (!validation.IsValid)
        {
            var invalidResult = RunPreparationResult.Invalid(validation);
            return invalidResult;
        }

        var whereResult = (breakpointNodeIds ?? []).Where(static nodeId =>
        {
            var evaluatedValue = !string.IsNullOrWhiteSpace(nodeId);
            return evaluatedValue;
        });
        var selectResult4 = whereResult.Select(static nodeId =>
        {
            var trimResult = nodeId.Trim();
            return trimResult;
        });
        var selectManyResult = definition.Canvases.SelectMany(static canvas => canvas.Nodes);
        var selectResult5 = selectManyResult.Select(static node => node.Id);
        var exceptResult = selectResult4.Except(selectResult5, StringComparer.Ordinal);
        var orderByResult = exceptResult.OrderBy(static nodeId => nodeId, StringComparer.Ordinal);
        var unknownBreakpoints = orderByResult.ToArray();
        if (unknownBreakpoints.Length > 0)
        {
            var selectResult = unknownBreakpoints.Select(nodeId =>
            {
                var validationDiagnosticDto = new ValidationDiagnosticDto(DebugErrorCodes.BreakpointNodeMissing, $"Breakpoint node '{nodeId}' does not exist in the saved flow. 断点节点“{nodeId}”不存在于已保存流程中。", $"breakpointNodeIds.{nodeId}");
                return validationDiagnosticDto;
            });
            var toArrayResult = selectResult.ToArray();
            var flowValidationResultDto = new FlowValidationResultDto(false, toArrayResult);
            var invalidResult2 = RunPreparationResult.Invalid(flowValidationResultDto);
            return invalidResult2;
        }

        // The source hash is derived data. Persist the canonical value into the
        // immutable run snapshot so its displayed hash always matches its source.
        // 源代码哈希属于派生数据；写入不可变运行快照前统一规范化，确保展示的哈希始终与源代码一致。
        definition = FlowDefinitionContractNormalizer.NormalizeForExecution(definition);
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        if (!libraryValidation.IsValid)
        {
            var invalidResult3 = RunPreparationResult.Invalid(libraryValidation);
            return invalidResult3;
        }

        var timeout = Math.Clamp(request.TimeoutSeconds ?? DefaultRunTimeoutSeconds, MinimumExecutionLimit, MaximumRunTimeoutSeconds);
        var maxSteps = Math.Clamp(request.MaxSteps ?? DefaultMaximumSteps, MinimumExecutionLimit, MaximumAllowedSteps);
        var maxNodeVisits = Math.Clamp(request.MaxNodeVisits ?? DefaultMaximumNodeVisits, MinimumExecutionLimit, MaximumAllowedNodeVisits);
        var createdAt = DateTimeOffset.UtcNow;
        var isListenerRun = IsListenerRun(definition);
        var concurrencyMode = (FlowConcurrencyMode)definition.RunPolicy!.ConcurrencyMode;
        var run = FlowRun.Start(projectId, flowId, definition.Version, createdAt, concurrencyMode, isListenerRun, executionKind: executionKind, debugSessionId: debugSessionId, definitionChecksum: definition.Checksum);
        var flowRunExecutionOptions = new FlowRunExecutionOptions(request.ProjectInputs, timeout, maxSteps, maxNodeVisits);
        // The execution deadline is bound when the scheduler actually obtains
        // a Worker slot. Before that, QueueWaitTimeoutSeconds governs waiting.
        // 实际执行截止时间在调度器取得 Worker 槽位时绑定；排队等待由独立超时控制。
        var admission = await _runs.TryCreateWithSnapshotAsync(run, definition, flowRunExecutionOptions, cancellationToken);
        if (!admission.IsAdmitted)
        {
            var alreadyActiveResult = RunPreparationResult.AlreadyActive(admission.ActiveRunId);
            return alreadyActiveResult;
        }

        var runPreparation = new RunPreparation(admission.Run!, definition, request.ProjectInputs, timeout, maxSteps, maxNodeVisits, isListenerRun);
        var successResult = RunPreparationResult.Success(runPreparation);
        return successResult;
    }

    private static bool IsListenerRun(FlowDefinitionDto definition)
    {
        var selectManyResult7 = definition.Canvases.SelectMany(static canvas => canvas.Connections);
        var whereResult8 = selectManyResult7.Where(static connection => connection.Kind == ConnectionKindDto.Execution);
        var selectResult9 = whereResult8.Select(static connection => connection.ToNodeId);
        var incomingExecutionNodes = selectResult9.ToHashSet(StringComparer.Ordinal);
        var selectManyResult10 = definition.Canvases.SelectMany(static canvas => canvas.Nodes);
        var anyResult = selectManyResult10.Any(node =>
        {
            var evaluatedValue6 = node.Type == NodeTypeDto.Flipflop && !incomingExecutionNodes.Contains(node.Id);
            return evaluatedValue6;
        });
        return anyResult;
    }
}

public sealed record RunPreparationResult(RunPreparation? Preparation, int StatusCode, string? ErrorTitle, object? ErrorBody = null, long? CurrentVersion = null)
{
    public bool IsSuccess => Preparation is not null;

    public static RunPreparationResult Success(RunPreparation preparation)
    {
        global::SereinFlow.Application.RunPreparationResult evaluatedValue11 = new(preparation, 202, null);
        return evaluatedValue11;
    }

    public static RunPreparationResult ProjectNotFound { get; } = new(null, 404, "Project not found. 未找到项目。");
    public static RunPreparationResult ProjectArchived { get; } = new(null, 409, "Archived projects cannot start new runs. 已归档项目不能启动新的运行实例。", new { code = ProjectErrorCodes.Archived, message = "Archived projects cannot start new runs. 已归档项目不能启动新的运行实例。" });
    public static RunPreparationResult FlowNotFound { get; } = new(null, 404, "Flow definition not found. 未找到流程定义。");

    public static RunPreparationResult VersionConflict(long currentVersion)
    {
        global::SereinFlow.Application.RunPreparationResult evaluatedValue12 = new(null, 409, "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。", null, currentVersion);
        return evaluatedValue12;
    }

    public static RunPreparationResult CandidateDefinitionNotAllowed { get; } = new(null, 409, "Only a saved flow definition may start a formal run. 正式运行只能使用已保存的流程定义。", new { code = RunErrorCodes.CandidateDefinitionNotAllowed, message = "Only a saved flow definition may start a formal run. 正式运行只能使用已保存的流程定义。" });

    public static RunPreparationResult Invalid(object validation)
    {
        global::SereinFlow.Application.RunPreparationResult evaluatedValue13 = new(null, 400, null, validation);
        return evaluatedValue13;
    }

    public static RunPreparationResult AlreadyActive(Guid? activeRunId)
    {
        global::SereinFlow.Application.RunPreparationResult evaluatedValue14 = new(null, 409, "The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。", new { code = FlowErrorCodes.RunAlreadyActive, message = "The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。", activeRunId });
        return evaluatedValue14;
    }
}
