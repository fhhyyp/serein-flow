using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

/// <summary>
/// Represents run preparation data used by SereinFlow.
/// 表示 SereinFlow 使用的运行准备信息数据。
/// </summary>
public sealed record RunPreparation(FlowRun Run, FlowDefinitionDto Definition, IReadOnlyDictionary<string, JsonElement>? ProjectInputs, int TimeoutSeconds, int MaxSteps, int MaxNodeVisits, bool IsListenerRun);
/// <summary>
/// Prepares flow runs by resolving the project, definition, and run limits.
/// 解析项目、流程定义和运行限制，为流程运行做好准备。
/// </summary>
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

    /// <summary>
    /// Validates a run request and prepares the flow execution inputs.
    /// 校验运行请求并准备流程执行参数。
    /// </summary>
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
            return RunPreparationResult.VersionConflict(persistedDefinition!.Version);
        }

        if (definitionOverride is not null)
        {
            if (executionKind != FlowRunExecutionKind.Debug)
            {
                return RunPreparationResult.CandidateDefinitionNotAllowed;
            }

            if (definitionOverride.Version != persistedDefinition.Version)
            {
                return RunPreparationResult.VersionConflict(persistedDefinition.Version);
            }
        }

        var definition = definitionOverride ?? persistedDefinition!;
        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        if (!validation.IsValid)
        {
            return RunPreparationResult.Invalid(validation);
        }

        var configuredBreakpointIds = (breakpointNodeIds ?? [])
            .Where(static nodeId =>
            {
                return !string.IsNullOrWhiteSpace(nodeId);
            })
            .Select(static nodeId =>
            {
                return nodeId.Trim();
            });
        var definedNodeIds = definition.Canvases
            .SelectMany(static canvas => canvas.Nodes)
            .Select(static node => node.Id);
        var unknownBreakpoints = configuredBreakpointIds
            .Except(definedNodeIds, StringComparer.Ordinal)
            .OrderBy(static nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        if (unknownBreakpoints.Length > 0)
        {
            var breakpointDiagnostics = unknownBreakpoints
                .Select(nodeId =>
                {
                    return new ValidationDiagnosticDto(DebugErrorCodes.BreakpointNodeMissing, $"Breakpoint node '{nodeId}' does not exist in the saved flow. 断点节点“{nodeId}”不存在于已保存流程中。", $"breakpointNodeIds.{nodeId}");
                })
                .ToArray();
            var flowValidationResultDto = new FlowValidationResultDto(false, breakpointDiagnostics);
            return RunPreparationResult.Invalid(flowValidationResultDto);
        }

        // The source hash is derived data. Persist the canonical value into the
        // immutable run snapshot so its displayed hash always matches its source.
        // 源代码哈希属于派生数据；写入不可变运行快照前统一规范化，确保展示的哈希始终与源代码一致。
        definition = FlowDefinitionContractNormalizer.NormalizeForExecution(definition);
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        if (!libraryValidation.IsValid)
        {
            return RunPreparationResult.Invalid(libraryValidation);
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
            return RunPreparationResult.AlreadyActive(admission.ActiveRunId);
        }

        var runPreparation = new RunPreparation(admission.Run!, definition, request.ProjectInputs, timeout, maxSteps, maxNodeVisits, isListenerRun);
        return RunPreparationResult.Success(runPreparation);
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
            .Any(node =>
            {
                return node.Type == NodeTypeDto.Flipflop && !incomingExecutionNodes.Contains(node.Id);
            });
    }
}

/// <summary>
/// Captures the outcome of run preparation operations.
/// 记录运行准备信息操作的结果。
/// </summary>
public sealed record RunPreparationResult(RunPreparation? Preparation, int StatusCode, string? ErrorTitle, object? ErrorBody = null, long? CurrentVersion = null)
{
    public bool IsSuccess => Preparation is not null;

    public static RunPreparationResult Success(RunPreparation preparation)
    {
        return new(preparation, 202, null);
    }

    public static RunPreparationResult ProjectNotFound { get; } = new(null, 404, "Project not found. 未找到项目。");
    public static RunPreparationResult ProjectArchived { get; } = new(null, 409, "Archived projects cannot start new runs. 已归档项目不能启动新的运行实例。", new { code = ProjectErrorCodes.Archived, message = "Archived projects cannot start new runs. 已归档项目不能启动新的运行实例。" });
    public static RunPreparationResult FlowNotFound { get; } = new(null, 404, "Flow definition not found. 未找到流程定义。");

    public static RunPreparationResult VersionConflict(long currentVersion)
    {
        return new(null, 409, "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。", null, currentVersion);
    }

    public static RunPreparationResult CandidateDefinitionNotAllowed { get; } = new(null, 409, "Only a saved flow definition may start a formal run. 正式运行只能使用已保存的流程定义。", new { code = RunErrorCodes.CandidateDefinitionNotAllowed, message = "Only a saved flow definition may start a formal run. 正式运行只能使用已保存的流程定义。" });

    public static RunPreparationResult Invalid(object validation)
    {
        return new(null, 400, null, validation);
    }

    public static RunPreparationResult AlreadyActive(Guid? activeRunId)
    {
        return new(null, 409, "The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。", new { code = FlowErrorCodes.RunAlreadyActive, message = "The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。", activeRunId });
    }
}
