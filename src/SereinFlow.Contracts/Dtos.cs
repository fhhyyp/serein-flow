using System.Text.Json;

namespace SereinFlow.Contracts;

public static class WorkerProtocol
{
    public const int Version = 1;
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

public enum WorkerEventType
{
    RunStarted,
    NodeStarted,
    NodeCompleted,
    NodeFailed,
    NodeErrored,
    Log,
    RunCompleted,
    RunCancelled
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
    int NodeCount = 0);

public sealed record ProjectWorkspaceDto(ProjectDto Project, IReadOnlyList<FlowDefinitionSummaryDto> Flows);

public sealed record CreateProjectRequestDto(string Name, FlowDefinitionDto Definition);

public sealed record RenameProjectRequestDto(string Name, long ExpectedVersion);

public sealed record UpdateFlowDefinitionRequestDto(long ExpectedVersion, FlowDefinitionDto Definition);

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
    IReadOnlyList<FlowCallParameterBindingDto>? FlowCallParameterBindings = null);

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
    LibraryLifecycleDto Lifecycle = LibraryLifecycleDto.Available);

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
    bool IsAwaitable = false);

public sealed record LibraryParameterDto(
    string Id,
    string Name,
    string Type,
    string? Description,
    bool Required,
    bool IsVariadic = false,
    string? VariadicGroupId = null,
    string? ElementType = null,
    EnumParameterMetadataDto? EnumMetadata = null);

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
    DateTimeOffset? QueuedAt = null);

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
    DateTimeOffset UpdatedAt);

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
    IReadOnlyList<string>? AllowedLibraryIds = null);

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
