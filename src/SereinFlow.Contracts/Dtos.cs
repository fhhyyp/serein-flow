using System.Text.Json;

namespace SereinFlow.Contracts;

public static class WorkerProtocol
{
    public const int Version = 2;
}

public enum NodeTypeDto
{
    Action = 0,
    Flipflop = 1,
    Script = 2,
    // 3 is intentionally reserved for the removed Condition node.
    // 3 专门保留给已移除的 Condition 节点，不能复用。
    FlowCall = 4,
}

public enum CanvasLifecycleDto
{
    Main,
    Init,
    Loading,
    Exit,
    Custom
}

public enum ConnectionKindDto
{
    Execution,
    Data
}

public enum ExecutionBranchDto
{
    Success,
    Failure,
    Error
}

public enum DataSourceDto
{
    Literal,
    PreviousNode,
    ProjectInput,
    Expression
}

public enum FlowRunStatusDto
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    Interrupted
}

public enum FlowRunExecutionKindDto
{
    Production,
    Debug
}

public enum FlowVersionTrackDto
{
    Development,
    Production
}

public enum FlowVersionOperationDto
{
    Created,
    Saved,
    Published,
    RolledBack,
    LibraryUpgraded,
    Imported
}

public enum FlowDebugSessionStatusDto
{
    Pending,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed
}

public enum FlowConcurrencyModeDto
{
    Parallel,
    ExclusiveReject
}

public enum FlowInvocationModeDto
{
    Asynchronous,
    Synchronous
}

public enum LibraryLifecycleDto
{
    Available,
    Archived
}

/// <summary>
/// States whether a node/parameter identity is stable under the current
/// attribute rules or was derived from legacy metadata. Stable identities may
/// be explicitly declared or deterministically inferred by the PE scanner.
/// 标明节点/参数身份是当前特性规则下稳定的，还是从旧元数据推导而来。稳定身份
/// 可以由类库作者显式声明，也可以由 PE 扫描器确定性推导。
/// </summary>
public enum LibraryContractIdentityConfidenceDto
{
    Explicit,
    Legacy
}

/// <summary>
/// Deterministic compatibility classification used by the library upgrade
/// preview. Values are ordered by neither severity nor UI color.
/// 类库升级预览使用的确定性兼容性分类。这些值不表示严重程度排序，也不等同于 UI 颜色。
/// </summary>
public enum LibraryCompatibilityClassificationDto
{
    Exact,
    Compatible,
    RequiresMapping,
    RequiresRewire,
    Breaking,
    Unknown
}

public enum LibraryUpgradePlanStatusDto
{
    Analyzed,
    Applied,
    Failed,
    Superseded
}

public enum WorkerEventType
{
    RunStarted,
    NodeStarted,
    NodeCompleted,
    NodeFailed,
    NodeErrored,
    Log,
    RunCompleted,
    RunCancelled,
    DebugPaused,
    DebugTriggerReceived,
    DebugTriggerQueued,
    DebugTriggerAdmitted,
    DebugTriggerRejected,
    DebugTriggerCompleted,
    DebugTriggerFailed
}

public sealed record ProjectDto(
    Guid Id,
    string Name,
    long Version,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FlowDefinitionSummaryDto(
    Guid Id,
    long Version,
    string EntryNodeId,
    int CanvasCount = 0,
    int NodeCount = 0,
    long? ProductionVersion = null);

public sealed record ProjectWorkspaceDto(ProjectDto Project, IReadOnlyList<FlowDefinitionSummaryDto> Flows);

public sealed record CreateProjectRequestDto(string Name, FlowDefinitionDto Definition);

public sealed record RenameProjectRequestDto(string Name, long ExpectedVersion);

public sealed record UpdateFlowDefinitionRequestDto(long ExpectedVersion, FlowDefinitionDto Definition);

public sealed record FlowVersionSummaryDto(
    Guid FlowId,
    long Version,
    FlowVersionTrackDto Track,
    FlowVersionOperationDto Operation,
    long? ParentVersion,
    long? SourceVersion,
    string Remark,
    DateTimeOffset? CreatedAt,
    bool IsCurrent);

public sealed record FlowVersionDetailDto(FlowVersionSummaryDto Version, FlowDefinitionDto Definition);

public sealed record PublishFlowVersionRequestDto(long ExpectedDevelopmentVersion, string? Remark = null);

public sealed record RollbackFlowVersionRequestDto(FlowVersionTrackDto Track, long ExpectedHeadVersion);

public sealed record RunFlowRequestDto(
    long? ExpectedFlowVersion,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs,
    int? TimeoutSeconds,
    int? MaxSteps,
    int? MaxNodeVisits = null);

public sealed record CanvasDto(
    string Id,
    CanvasLifecycleDto Lifecycle,
    IReadOnlyList<NodeDto> Nodes,
    IReadOnlyList<ConnectionDto> Connections,
    string? Name = null);

public sealed record NodeDto(
    string Id,
    NodeTypeDto Type,
    string DisplayName,
    double X,
    double Y,
    IReadOnlyList<NodePortDto> Ports,
    IReadOnlyList<NodeParameterDto> Parameters,
    ScriptNodeDataDto? Script,
    NodeUiMetadataDto? Ui = null);

public sealed record NodeUiMetadataDto(
    string Kind,
    string TitleKey,
    string SubtitleKey,
    string? Description,
    string Status,
    bool HasDataOutput,
    double? Width,
    string? Category = null,
    string? LibraryId = null,
    string? ClassName = null,
    string? MethodName = null,
    string? DllName = null,
    string? DllVersion = null,
    string? ReturnType = null,
    string? TargetNodeId = null,
    string? TargetFlowId = null,
    bool? IsAwaitable = null,
    string? StaticReturnType = null,
    bool? IsDynamicReturnType = null,
    string? TargetCanvasId = null,
    bool? IsPublic = null,
    IReadOnlyList<FlowCallParameterBindingDto>? FlowCallParameterBindings = null,
    string? LibraryNodeContractId = null,
    string? FlowLibraryName = null);

public sealed record NodePortDto(string Id, string Name, string Direction, bool Required);

public sealed record NodeParameterDto(
    string Name,
    string? ValueJson,
    DataSourceDto Source,
    bool Required,
    NodeParameterUiMetadataDto? Ui = null);

public sealed record NodeParameterUiMetadataDto(
    string Id,
    string NameKey,
    string ValueKind,
    string? LiteralValue,
    string? ProjectInputKey,
    string? Expression,
    string? SourceNodeId,
    string? SourcePortId,
    string? Type = null,
    string? Description = null,
    string? InputMode = null,
    bool? IsVariadic = null,
    string? VariadicGroupId = null,
    string? ElementType = null,
    string? VariadicMode = null,
    EnumParameterMetadataDto? EnumMetadata = null);

/// <summary>
/// A display-safe enum member extracted from PE metadata. Numeric values are
/// strings so unsigned 64-bit enum values never lose precision in JSON/JS.
/// 从 PE 元数据提取的安全枚举成员。数值以字符串保存，避免无符号 64 位枚举值在 JSON/JS 中丢失精度。
/// </summary>
public sealed record EnumValueOptionDto(string Name, string NumericValue);

/// <summary>
/// Enum metadata travels with a parameter definition rather than resolving a
/// potentially newer library catalog at run/snapshot read time.
/// 枚举元数据随参数定义保存，不会在读取运行快照时改用可能已更新的类库目录。
/// </summary>
public sealed record EnumParameterMetadataDto(
    string TypeName,
    bool IsFlags,
    string UnderlyingType,
    IReadOnlyList<EnumValueOptionDto> Options);

public sealed record ConnectionDto(
    string Id,
    string FromNodeId,
    string FromPortId,
    string ToNodeId,
    string ToPortId,
    ConnectionKindDto Kind,
    ExecutionBranchDto? Branch,
    DataSourceDto? DataSource,
    int Priority);

public sealed record ScriptNodeDataDto(
    string NodeId,
    string Source,
    string LanguageVersion,
    string SourceHash,
    IReadOnlyList<ScriptValueContractDto> Inputs,
    IReadOnlyList<ScriptValueContractDto> Outputs);

public sealed record ScriptValueContractDto(
    string Name,
    string ValueKind,
    bool Required,
    string? Id = null,
    string? Description = null);

public sealed record FlowCallParameterBindingDto(string CallParameterId, string TargetParameterId);

public sealed record BuiltinNodeCatalogDto(IReadOnlyList<NodeCreationDescriptorDto> Nodes);

public sealed record NodeCreationDescriptorDto(
    string Id,
    NodeTypeDto Type,
    string DisplayName,
    string? Description,
    NodeUiMetadataDto Ui,
    ScriptNodeDataDto? Script = null,
    IReadOnlyList<NodeParameterDto>? Parameters = null);

public sealed record FlowDefinitionDto(
    Guid Id,
    int SchemaVersion,
    long Version,
    IReadOnlyList<CanvasDto> Canvases,
    string EntryNodeId,
    string Checksum,
    FlowUiMetadataDto? Ui = null,
    FlowRunPolicyDto? RunPolicy = null);

public sealed record FlowRunPolicyDto(FlowConcurrencyModeDto ConcurrencyMode);

public sealed record FlowUiMetadataDto(
    FlowConnectionLineTypesDto? ConnectionLineTypes = null);

public sealed record FlowConnectionLineTypesDto(
    string? Execution = null,
    string? Data = null);

public sealed record PluginManifestDto(
    string AssemblyName,
    string AssemblyVersion,
    string Sha256,
    IReadOnlyList<string> TargetRids,
    string ApiVersion,
    IReadOnlyList<string> NodeTypes);

/// <summary>
/// A server-owned class library package. The package path is intentionally not
/// exposed to clients; only immutable metadata and the safe node catalog cross
/// the API boundary.
/// 服务端管理的类库包。包路径不会暴露给客户端，只有不可变元数据和安全节点目录会跨越 API 边界。
/// </summary>
public sealed record LibraryDto(
    string Id,
    string Name,
    string Version,
    string FileName,
    long SizeBytes,
    string Sha256,
    DateTimeOffset UploadedAt,
    IReadOnlyList<LibraryNodeDto> Nodes,
    LibraryLifecycleDto Lifecycle = LibraryLifecycleDto.Available,
    string? FamilyId = null,
    string? SemanticVersion = null,
    LibraryArtifactManifestDto? CompatibilityManifest = null,
    string? FamilyName = null,
    string? DllSha256 = null);

public sealed record LibraryNodeDto(
    string Id,
    NodeTypeDto Type,
    string DisplayName,
    string? Description,
    string LibraryId,
    string ClassName,
    string MethodName,
    string DllName,
    string DllVersion,
    string ReturnType,
    IReadOnlyList<LibraryParameterDto> Parameters,
    bool IsAwaitable = false,
    string? ContractId = null,
    string? OverloadSignature = null,
    LibraryContractIdentityConfidenceDto IdentityConfidence = LibraryContractIdentityConfidenceDto.Legacy,
    string? FlowLibraryName = null);

public sealed record LibraryParameterDto(
    string Id,
    string Name,
    string Type,
    string? Description,
    bool Required,
    bool IsVariadic = false,
    string? VariadicGroupId = null,
    string? ElementType = null,
    EnumParameterMetadataDto? EnumMetadata = null,
    string? DefaultValue = null,
    IReadOnlyList<string>? Aliases = null,
    LibraryContractIdentityConfidenceDto IdentityConfidence = LibraryContractIdentityConfidenceDto.Legacy);

/// <summary>
/// Immutable compatibility snapshot generated from PE metadata at upload time.
/// It is separate from the presentation catalog so later UI changes cannot
/// alter the ABI used by a class-library upgrade analysis.
/// 上传时由 PE 元数据生成的不可变兼容性快照。它与展示目录分离，后续 UI
/// 调整不会改变类库升级分析所依据的 ABI。
/// </summary>
public sealed record LibraryArtifactManifestDto(
    string ArtifactId,
    string AssemblyName,
    string AssemblyVersion,
    string SemanticVersion,
    IReadOnlyList<LibraryManifestNodeDto> Nodes);

/// <summary>
/// Contract-level compatibility information produced while a completed ZIP
/// package is being previewed. It does not imply that the target artifact has
/// been imported or attached to a project.
/// 类库 ZIP 预览期间生成的契约级兼容性信息。不表示目标工件已经导入或接入项目。
/// </summary>
public sealed record LibraryArtifactCompatibilityDto(
    string BaselineArtifactId,
    string BaselineVersion,
    string TargetArtifactId,
    string TargetVersion,
    bool IsCompatible,
    IReadOnlyList<LibraryArtifactCompatibilityIssueDto> Issues);

public sealed record LibraryArtifactCompatibilityIssueDto(
    string Id,
    LibraryCompatibilityClassificationDto Classification,
    string Code,
    string Message,
    string? SourceNodeContractId = null,
    string? TargetNodeContractId = null,
    string? SourceParameterId = null,
    string? TargetParameterId = null,
    bool BlocksApplication = false);

/// <summary>
/// Bounded impact counts for a package replacement candidate. The package is
/// not persisted when this information is produced.
/// 候选类库替换包的有界影响统计。生成该信息时不会持久化类库包。
/// </summary>
public sealed record LibraryPackageProjectImpactDto(
    Guid ProjectId,
    string? BaselineArtifactId,
    int AffectedFlowCount,
    int AffectedNodeCount,
    IReadOnlyList<Guid> AffectedFlowIds);

/// <summary>
/// A logical product line containing immutable library artifacts. The family
/// never replaces an artifact identity; flows and runs continue to bind SHA
/// artifact IDs directly.
/// 逻辑上的类库产品线，包含多个不可变类库工件。类库族不会替代工件身份；
/// 流程和运行仍直接绑定 SHA 工件 ID。
/// </summary>
public sealed record LibraryFamilyDto(
    string Id,
    string Name,
    string? Description,
    string? LatestArtifactId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<LibraryDto>? Artifacts = null);

/// <summary>
/// Read-only impact counts for one immutable library artifact. Current flows
/// are counted from the latest saved definition of each flow; version and run
/// counts retain the historical audit footprint.
/// 单个不可变类库工件的只读影响统计。当前流程按每个流程最新保存的定义统计；
/// 流程版本和运行次数则保留历史审计范围。
/// </summary>
public sealed record LibraryArtifactUsageDto(
    string LibraryArtifactId,
    int CurrentFlowCount,
    int FlowVersionCount,
    int RunSnapshotCount);

/// <summary>
/// Assigns an immutable artifact to an existing family, or creates a new
/// family from Name when FamilyId is absent. Neither option changes the ZIP.
/// 当提供 FamilyId 时把不可变工件归入现有类库族；缺少 FamilyId 时按 Name
/// 创建新类库族。两种操作都不会更改 ZIP 工件。
/// </summary>
public sealed record AssignLibraryFamilyRequestDto(
    string? FamilyId,
    string? Name,
    string? Description = null);

public sealed record UpdateLibraryLifecycleRequestDto(LibraryLifecycleDto Lifecycle);

public sealed record LibraryUpgradePreviewRequestDto(
    string SourceArtifactId,
    string TargetArtifactId,
    IReadOnlyList<Guid> FlowIds);

public sealed record LibraryUpgradeIssueDto(
    string Id,
    LibraryCompatibilityClassificationDto Classification,
    string Code,
    string Message,
    string? CanvasId = null,
    string? NodeId = null,
    string? SourceNodeContractId = null,
    string? TargetNodeContractId = null,
    string? SourceParameterId = null,
    string? TargetParameterId = null,
    string? ConnectionId = null,
    bool RequiresAcknowledgement = false,
    bool BlocksApplication = false);

public sealed record FlowLibraryUpgradePreviewDto(
    Guid FlowId,
    long FlowVersion,
    bool CanApply,
    int AffectedNodeCount,
    IReadOnlyList<LibraryUpgradeIssueDto> Issues);

public sealed record LibraryUpgradePlanDto(
    Guid Id,
    Guid ProjectId,
    string SourceArtifactId,
    string TargetArtifactId,
    LibraryUpgradePlanStatusDto Status,
    IReadOnlyList<FlowLibraryUpgradePreviewDto> Flows,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt = null,
    string? FailureMessage = null,
    IReadOnlyList<LibraryUpgradePlanFlowResultDto>? AppliedFlows = null);

/// <summary>
/// Immutable audit entry for one successfully applied flow within a library
/// upgrade plan. A plan may apply several flows independently.
/// 类库升级计划中单个流程成功应用的不可变审计记录。一个计划可以独立应用多个流程。
/// </summary>
public sealed record LibraryUpgradePlanFlowResultDto(
    Guid FlowId,
    long PreviousVersion,
    long NewVersion,
    DateTimeOffset AppliedAt);

/// <summary>
/// Phase 3 applies one selected flow at a time. The expected version prevents
/// a preview from overwriting an editor's newer definition.
/// 第三阶段一次只应用一个选定流程。期望版本可防止旧预览覆盖编辑器中的新定义。
/// </summary>
public sealed record ApplyLibraryUpgradeRequestDto(
    Guid FlowId,
    long ExpectedFlowVersion,
    IReadOnlyList<string>? AcknowledgedItemIds = null);

public sealed record LibraryUpgradeApplyResultDto(
    Guid FlowId,
    long PreviousVersion,
    long NewVersion,
    string SourceArtifactId,
    string TargetArtifactId,
    int MigratedNodeCount,
    IReadOnlyList<LibraryUpgradeIssueDto> Issues);

/// <summary>
/// A batch keeps every flow result explicit: success on one flow never hides
/// a conflict or validation failure on another flow.
/// 批量操作会明确保留每个流程的结果：一个流程成功不会掩盖其它流程的冲突或校验失败。
/// </summary>
public sealed record ApplyLibraryUpgradeBatchRequestDto(
    IReadOnlyList<ApplyLibraryUpgradeRequestDto> Flows);

public sealed record LibraryUpgradeApplyFailureDto(
    Guid FlowId,
    int StatusCode,
    string? Code,
    string? Message,
    long? CurrentVersion = null);

public sealed record LibraryUpgradeBatchApplyResultDto(
    Guid PlanId,
    IReadOnlyList<LibraryUpgradeApplyResultDto> Succeeded,
    IReadOnlyList<LibraryUpgradeApplyFailureDto> Failed);

public sealed record LibraryManifestNodeDto(
    string ContractId,
    LibraryContractIdentityConfidenceDto IdentityConfidence,
    NodeTypeDto Type,
    string DeclaringType,
    string MethodName,
    string OverloadSignature,
    string ReturnType,
    bool IsAwaitable,
    IReadOnlyList<LibraryManifestParameterDto> Parameters);

public sealed record LibraryManifestParameterDto(
    string ContractId,
    LibraryContractIdentityConfidenceDto IdentityConfidence,
    IReadOnlyList<string> Aliases,
    string ClrName,
    string DisplayName,
    string Type,
    bool Required,
    string? DefaultValue,
    bool IsVariadic,
    string? ElementType,
    EnumParameterMetadataDto? EnumMetadata,
    bool IsInjectedFlowContext = false);

public sealed record LibraryUploadResultDto(LibraryDto Library, bool AlreadyExists);

public sealed record ProjectLibraryReferenceDto(
    Guid ProjectId,
    string LibraryId,
    DateTimeOffset ReferencedAt,
    LibraryDto Library);

public sealed record FlowValidationResultDto(
    bool IsValid,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics);

public sealed record ValidationDiagnosticDto(string Code, string Message, string? Path);

public sealed record FlowRunDto(
    Guid Id,
    Guid FlowId,
    long FlowVersion,
    FlowRunStatusDto Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string? ErrorSummary,
    Guid? ProjectId = null,
    DateTimeOffset? CreatedAt = null,
    string? CancellationReason = null,
    FlowConcurrencyModeDto? ConcurrencyMode = null,
    bool IsListenerRun = false,
    DateTimeOffset? QueuedAt = null,
    FlowRunExecutionKindDto ExecutionKind = FlowRunExecutionKindDto.Production,
    Guid? DebugSessionId = null);

public sealed record StartFlowDebugSessionRequestDto(
    IReadOnlyList<string>? BreakpointNodeIds,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null,
    int? TimeoutSeconds = null,
    int? MaxSteps = null,
    int? MaxNodeVisits = null,
    long? ExpectedFlowVersion = null,
    int? MaxQueuedFlipflopTriggers = null);

public sealed record FlowDebugSessionDto(
    Guid Id,
    Guid RunId,
    Guid ProjectId,
    Guid FlowId,
    FlowDebugSessionStatusDto Status,
    IReadOnlyList<string> BreakpointNodeIds,
    string? CurrentNodeId,
    Guid? ActiveInvocationId,
    string? ActiveFlipflopNodeId,
    int QueuedTriggerCount,
    long LastCommandSequence,
    string? FailureMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long StateRevision = 0,
    FlowDebugPauseStateDto? PauseState = null,
    FlowDebugNodeResultDto? LastNodeResult = null);

public sealed record FlowDebugPauseStateDto(
    string NodeId,
    string NodeType,
    int Step,
    int FrameDepth,
    Guid? InvocationId,
    long BoundarySequence,
    JsonElement Inputs,
    DateTimeOffset PausedAt);

public sealed record FlowDebugNodeResultDto(
    string NodeId,
    long Sequence,
    DateTimeOffset CompletedAt,
    string Outcome,
    string? Branch,
    JsonElement Inputs,
    JsonElement Outputs,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record FlowDebugWaitResultDto(
    bool HasChanged,
    bool TimedOut,
    FlowDebugSessionDto Session);

public sealed record FlowDebugCommandRequestDto(long CommandSequence);

public sealed record FlowRunOverviewDto(
    int QueueCapacity,
    int QueuedCount,
    int ActiveRunCount,
    int ActiveListenerRunCount,
    int MaxConcurrentRuns,
    int MaxConcurrentListenerRuns,
    int MaxConcurrentRunsPerProject,
    IReadOnlyList<FlowRunDto> QueuedRuns,
    IReadOnlyList<FlowRunDto> ActiveRuns,
    IReadOnlyList<FlowRunDto> RecentRuns);

public sealed record RunExecutionSettingsDto(
    int QueueCapacity,
    int MaxConcurrentRuns,
    int MaxConcurrentListenerRuns,
    int MaxConcurrentRunsPerProject,
    int QueueWaitTimeoutSeconds = 60,
    int ShutdownGracePeriodSeconds = 10,
    int SynchronousInvocationTimeoutSeconds = 30);

public sealed record FlowInterfaceDto(
    Guid Id,
    Guid ProjectId,
    Guid FlowId,
    string Name,
    FlowInvocationModeDto InvocationMode,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long? ProductionVersion = null);

public sealed record CreateFlowInterfaceRequestDto(
    Guid ProjectId,
    Guid FlowId,
    string Name,
    FlowInvocationModeDto InvocationMode,
    bool IsEnabled = true);

public sealed record UpdateFlowInterfaceRequestDto(
    string Name,
    FlowInvocationModeDto InvocationMode,
    bool IsEnabled);

public sealed record PublicFlowInvocationRequestDto(
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null,
    int? TimeoutSeconds = null,
    int? MaxSteps = null,
    int? MaxNodeVisits = null);

public sealed record FlowNodeDataDto(string NodeId, JsonElement Outputs);

public sealed record PublicFlowInvocationResponseDto(
    Guid TaskId,
    FlowRunStatusDto Status,
    bool IsCompleted,
    IReadOnlyList<FlowNodeDataDto> FlowData);

public sealed record FlowRunEventDto(
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string? NodeId,
    string PayloadJson);

public sealed record FlowRunOutputDto(
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    string NodeId,
    string Outcome,
    string? Branch,
    JsonElement Inputs,
    JsonElement Outputs,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record WorkerEventEnvelopeDto(
    int ProtocolVersion,
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    WorkerEventType EventType,
    string? NodeId,
    string PayloadJson);

public sealed record WorkerRunRequestDto(
    int ProtocolVersion,
    Guid RunId,
    Guid FlowId,
    long FlowVersion,
    string DefinitionJson,
    DateTimeOffset Deadline,
    string? ProjectId = null,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null,
    int MaxSteps = 10_000,
    string? ScriptArtifactRootPath = null,
    string? LibraryPackageRootPath = null,
    int MaxNodeVisits = 1_000,
    IReadOnlyList<string>? AllowedLibraryIds = null,
    WorkerDebugOptionsDto? Debug = null);

/// <summary>
/// Immutable debug settings captured when a worker run starts. Breakpoints are
/// node IDs from the submitted flow snapshot, never CLR code locations.
/// Worker 运行启动时捕获的不可变调试设置。断点是已提交流程快照中的节点 ID，
/// 绝不是 CLR 代码位置。
/// </summary>
public sealed record WorkerDebugOptionsDto(
    Guid DebugSessionId,
    IReadOnlyList<string> BreakpointNodeIds,
    int MaxQueuedFlipflopTriggers = 64);

/// <summary>
/// A strictly increasing control command scoped to one debug session.
/// 严格递增且仅作用于一个调试会话的控制命令。
/// </summary>
public sealed record WorkerDebugCommandDto(
    int ProtocolVersion,
    Guid RunId,
    Guid DebugSessionId,
    long CommandSequence);

/// <summary>
/// Safe snapshot emitted before the paused node enters its executor.
/// 节点进入执行器前发出的安全快照。
/// </summary>
public sealed record WorkerDebugPauseDto(
    Guid DebugSessionId,
    Guid RunId,
    string NodeId,
    string NodeType,
    int Step,
    object? Inputs,
    int FrameDepth,
    Guid? TriggerInvocationId = null);

public sealed record WorkerCancelRequestDto(
    int ProtocolVersion,
    Guid RunId,
    string Reason);

public sealed record WorkerRunResultDto(
    int ProtocolVersion,
    Guid RunId,
    FlowRunStatusDto Status,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record ProblemDetailsDto(
    string Type,
    string Title,
    int Status,
    string Detail,
    string? Instance,
    IReadOnlyDictionary<string, string[]>? Errors);
