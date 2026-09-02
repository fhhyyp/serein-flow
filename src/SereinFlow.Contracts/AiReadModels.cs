using System.Text.Json;

namespace SereinFlow.Contracts;

/// <summary>
/// Machine-readable, read-only contracts for MCP Resources and AI analysis.
/// These contracts are intentionally separate from the editor DTOs so UI
/// metadata changes do not become an AI compatibility break.
/// 面向 MCP Resource 和 AI 分析的机器可读只读契约。该契约与编辑器 DTO 分离，
/// 避免 UI 元数据变化直接变成 AI 兼容性变化。
/// </summary>
public static class AiReadModelContract
{
    public const int SchemaVersion = 1;
}

public sealed record AiReadModelOptions(
    int MaxItems = 200,
    int MaxJsonBytes = 64 * 1024,
    bool IncludeFlowLiteralValues = false,
    bool IncludeScriptSource = false)
{
    public AiReadModelOptions Normalize()
        => this with
        {
            MaxItems = Math.Clamp(MaxItems, 1, 1_000),
            MaxJsonBytes = Math.Clamp(MaxJsonBytes, 1_024, 1_048_576),
        };
}

public sealed record AiPageDto<T>(
    int SchemaVersion,
    IReadOnlyList<T> Items,
    bool HasMore,
    string? NextCursor);

public sealed record AiProjectSummaryDto(
    Guid Id,
    string Name,
    long Version,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AiFlowSummaryDto> Flows);

public sealed record AiFlowSummaryDto(
    Guid Id,
    long DevelopmentVersion,
    long? ProductionVersion,
    string EntryNodeId,
    int CanvasCount,
    int NodeCount);

public sealed record AiFlowTopologyDto(
    Guid ProjectId,
    Guid FlowId,
    FlowVersionTrackDto? Track,
    long Version,
    int SchemaVersion,
    string EntryNodeId,
    string Checksum,
    string ConcurrencyMode,
    IReadOnlyList<AiCanvasDto> Canvases);

public sealed record AiFlowEditModelDto(
    Guid ProjectId,
    Guid FlowId,
    AiFlowTopologyDto Flow,
    IReadOnlyList<NodeCreationDescriptorDto> BuiltinNodes,
    IReadOnlyList<AiLibrarySummaryDto> Libraries);

public sealed record AiCanvasDto(
    string Id,
    string Lifecycle,
    string? Name,
    IReadOnlyList<AiNodeContractDto> Nodes,
    IReadOnlyList<AiConnectionDto> Connections,
    bool IsTruncated = false);

public sealed record AiNodeContractDto(
    string Id,
    string Type,
    string DisplayName,
    string CanvasId,
    IReadOnlyList<AiPortContractDto> Ports,
    IReadOnlyList<AiParameterContractDto> Parameters,
    AiNodeRuntimeDto? Runtime,
    AiScriptContractDto? Script,
    double X = 0,
    double Y = 0);

public sealed record AiPortContractDto(
    string Id,
    string Name,
    string Direction,
    bool Required);

public sealed record AiParameterContractDto(
    string Id,
    string Name,
    string? Type,
    string Source,
    bool Required,
    bool IsConfigured,
    string? ValueJson,
    string? ProjectInputKey,
    string? Expression,
    string? SourceNodeId,
    string? SourcePortId,
    bool IsVariadic,
    string? VariadicGroupId,
    string? ElementType,
    string? Description,
    EnumParameterMetadataDto? EnumMetadata);

public sealed record AiNodeRuntimeDto(
    string? LibraryId,
    string? LibraryNodeContractId,
    string? FlowLibraryName,
    string? ClassName,
    string? MethodName,
    string? DllName,
    string? DllVersion,
    string? ReturnType,
    bool IsAwaitable,
    Guid? TargetFlowId,
    string? TargetNodeId,
    string? TargetCanvasId,
    bool IsPublic);

public sealed record AiScriptContractDto(
    string NodeId,
    string LanguageVersion,
    string SourceHash,
    string? Source,
    IReadOnlyList<AiScriptValueContractDto> Inputs,
    IReadOnlyList<AiScriptValueContractDto> Outputs);

public sealed record AiScriptValueContractDto(
    string Id,
    string Name,
    string ValueKind,
    bool Required,
    string? Description);

public sealed record AiConnectionDto(
    string Id,
    string FromNodeId,
    string FromPortId,
    string ToNodeId,
    string ToPortId,
    string Kind,
    string? Branch,
    string? DataSource,
    int Priority);

public sealed record AiLibrarySummaryDto(
    string Id,
    string Name,
    string Version,
    string? SemanticVersion,
    string Sha256,
    string Lifecycle,
    string? FamilyId,
    string? FamilyName,
    int NodeCount,
    IReadOnlyList<AiLibraryNodeContractDto>? Nodes = null);

public sealed record AiLibraryNodeContractDto(
    string Id,
    string ContractId,
    string Type,
    string DisplayName,
    string? Description,
    string LibraryId,
    string ClassName,
    string MethodName,
    string DllName,
    string DllVersion,
    string ReturnType,
    bool IsAwaitable,
    string? FlowLibraryName,
    IReadOnlyList<AiLibraryParameterContractDto> Parameters,
    LibraryContractIdentityConfidenceDto IdentityConfidence);

public sealed record AiLibraryParameterContractDto(
    string Id,
    string Name,
    string Type,
    string? Description,
    bool Required,
    bool IsVariadic,
    string? ElementType,
    string? DefaultValue,
    IReadOnlyList<string>? Aliases,
    LibraryContractIdentityConfidenceDto IdentityConfidence,
    EnumParameterMetadataDto? EnumMetadata);

public sealed record AiRunSummaryDto(
    Guid Id,
    Guid ProjectId,
    Guid FlowId,
    long FlowVersion,
    string Status,
    string ExecutionKind,
    bool IsListenerRun,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string? ErrorSummary,
    string? CancellationReason,
    Guid? DebugSessionId);

public sealed record AiRunEventDto(
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string? NodeId,
    JsonElement Payload,
    bool PayloadWasMalformed = false,
    bool PayloadWasTruncated = false);

public sealed record AiNodeExecutionRecordDto(
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    string NodeId,
    string Outcome,
    string? Branch,
    JsonElement Inputs,
    JsonElement Outputs,
    string? ErrorCode,
    string? ErrorMessage,
    bool InputsWereMalformed = false,
    bool OutputsWereMalformed = false,
    bool InputsWereTruncated = false,
    bool OutputsWereTruncated = false);

public sealed record AiDebugStateDto(
    Guid Id,
    Guid RunId,
    Guid ProjectId,
    Guid FlowId,
    string Status,
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
    AiDebugPauseStateDto? PauseState = null,
    AiDebugNodeResultDto? LastNodeResult = null);

/// <summary>
/// Bounded discovery projection for active debug sessions. Detailed pause
/// inputs and node outputs remain available only through the single-session
/// debug state projection.
/// </summary>
public sealed record AiDebugSessionSummaryDto(
    Guid Id,
    Guid RunId,
    Guid ProjectId,
    Guid FlowId,
    string Status,
    IReadOnlyList<string> BreakpointNodeIds,
    string? CurrentNodeId,
    Guid? ActiveInvocationId,
    string? ActiveFlipflopNodeId,
    int QueuedTriggerCount,
    long LastCommandSequence,
    long StateRevision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AiDebugPauseStateDto(
    string NodeId,
    string NodeType,
    int Step,
    int FrameDepth,
    Guid? InvocationId,
    long BoundarySequence,
    JsonElement Inputs,
    DateTimeOffset PausedAt);

public sealed record AiDebugNodeResultDto(
    string NodeId,
    long Sequence,
    DateTimeOffset CompletedAt,
    string Outcome,
    string? Branch,
    JsonElement Inputs,
    JsonElement Outputs,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record AiDebugStateWaitResultDto(
    bool HasChanged,
    bool TimedOut,
    AiDebugStateDto State);

public sealed record AiRunInspectionDto(
    AiRunSummaryDto Run,
    AiFlowTopologyDto? DefinitionSnapshot,
    AiPageDto<AiRunEventDto> Events,
    AiPageDto<AiNodeExecutionRecordDto> Outputs,
    AiDebugStateDto? DebugSession);
