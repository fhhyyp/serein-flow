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

/// <summary>
/// Configures AI read model behavior.
/// 配置AIReadModel行为。
/// </summary>
public sealed record AiReadModelOptions(int MaxItems = 200, int MaxJsonBytes = 64 * 1024, bool IncludeFlowLiteralValues = false, bool IncludeScriptSource = false)
{
    private const int MinimumItemCount = 1;
    private const int MaximumItemCount = 1_000;
    private const int MinimumJsonBytes = 1_024;
    private const int MaximumJsonBytes = 1_048_576;
    public AiReadModelOptions Normalize()
    {
        var evaluatedValue = this with
        {
            MaxItems = Math.Clamp(MaxItems, MinimumItemCount, MaximumItemCount),
            MaxJsonBytes = Math.Clamp(MaxJsonBytes, MinimumJsonBytes, MaximumJsonBytes),
        };
        return evaluatedValue;
    }
}

/// <summary>
/// Defines the API data contract for AI page.
/// 定义AI分页的 API 数据契约。
/// </summary>
public sealed record AiPageDto<T>(int SchemaVersion, IReadOnlyList<T> Items, bool HasMore, string? NextCursor);
/// <summary>
/// Defines the API data contract for AI project summary.
/// 定义AI项目摘要的 API 数据契约。
/// </summary>
public sealed record AiProjectSummaryDto(Guid Id, string Name, long Version, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<AiFlowSummaryDto> Flows);
/// <summary>
/// Defines the API data contract for AI flow summary.
/// 定义AI流程摘要的 API 数据契约。
/// </summary>
public sealed record AiFlowSummaryDto(Guid Id, long DevelopmentVersion, long? ProductionVersion, string EntryNodeId, int CanvasCount, int NodeCount);
/// <summary>
/// Defines the API data contract for AI flow topology.
/// 定义AI流程拓扑的 API 数据契约。
/// </summary>
public sealed record AiFlowTopologyDto(Guid ProjectId, Guid FlowId, FlowVersionTrackDto? Track, long Version, int SchemaVersion, string EntryNodeId, string Checksum, string ConcurrencyMode, IReadOnlyList<AiCanvasDto> Canvases);
/// <summary>
/// Defines the API data contract for AI flow edit model.
/// 定义AI流程EditModel的 API 数据契约。
/// </summary>
public sealed record AiFlowEditModelDto(Guid ProjectId, Guid FlowId, AiFlowTopologyDto Flow, IReadOnlyList<NodeCreationDescriptorDto> BuiltinNodes, IReadOnlyList<AiLibrarySummaryDto> Libraries);
/// <summary>
/// Defines the API data contract for AI canvas.
/// 定义AI画布的 API 数据契约。
/// </summary>
public sealed record AiCanvasDto(string Id, string Lifecycle, string? Name, IReadOnlyList<AiNodeContractDto> Nodes, IReadOnlyList<AiConnectionDto> Connections, bool IsTruncated = false);
/// <summary>
/// Defines the API data contract for AI node contract.
/// 定义AI节点契约的 API 数据契约。
/// </summary>
public sealed record AiNodeContractDto(string Id, string Type, string DisplayName, string CanvasId, IReadOnlyList<AiPortContractDto> Ports, IReadOnlyList<AiParameterContractDto> Parameters, AiNodeRuntimeDto? Runtime, AiScriptContractDto? Script, double X = 0, double Y = 0);
/// <summary>
/// Defines the API data contract for AI port contract.
/// 定义AI端口契约的 API 数据契约。
/// </summary>
public sealed record AiPortContractDto(string Id, string Name, string Direction, bool Required);
/// <summary>
/// Defines the API data contract for AI parameter contract.
/// 定义AI参数契约的 API 数据契约。
/// </summary>
public sealed record AiParameterContractDto(string Id, string Name, string? Type, string Source, bool Required, bool IsConfigured, string? ValueJson, string? ProjectInputKey, string? Expression, string? SourceNodeId, string? SourcePortId, bool IsVariadic, string? VariadicGroupId, string? ElementType, string? Description, EnumParameterMetadataDto? EnumMetadata);
/// <summary>
/// Defines the API data contract for AI node runtime.
/// 定义AI节点Runtime的 API 数据契约。
/// </summary>
public sealed record AiNodeRuntimeDto(string? LibraryId, string? LibraryNodeContractId, string? FlowLibraryName, string? ClassName, string? MethodName, string? DllName, string? DllVersion, string? ReturnType, bool IsAwaitable, Guid? TargetFlowId, string? TargetNodeId, string? TargetCanvasId, bool IsPublic);
/// <summary>
/// Defines the API data contract for AI script contract.
/// 定义AI脚本契约的 API 数据契约。
/// </summary>
public sealed record AiScriptContractDto(string NodeId, string LanguageVersion, string SourceHash, string? Source, IReadOnlyList<AiScriptValueContractDto> Inputs, IReadOnlyList<AiScriptValueContractDto> Outputs);
/// <summary>
/// Defines the API data contract for AI script value contract.
/// 定义AI脚本值契约的 API 数据契约。
/// </summary>
public sealed record AiScriptValueContractDto(string Id, string Name, string ValueKind, bool Required, string? Description);
/// <summary>
/// Defines the API data contract for AI connection.
/// 定义AI连线的 API 数据契约。
/// </summary>
public sealed record AiConnectionDto(string Id, string FromNodeId, string FromPortId, string ToNodeId, string ToPortId, string Kind, string? Branch, string? DataSource, int Priority);
/// <summary>
/// Defines the API data contract for AI library summary.
/// 定义AI库摘要的 API 数据契约。
/// </summary>
public sealed record AiLibrarySummaryDto(string Id, string Name, string Version, string? SemanticVersion, string Sha256, string Lifecycle, string? FamilyId, string? FamilyName, int NodeCount, IReadOnlyList<AiLibraryNodeContractDto>? Nodes = null);
/// <summary>
/// Defines the API data contract for AI library node contract.
/// 定义AI库节点契约的 API 数据契约。
/// </summary>
public sealed record AiLibraryNodeContractDto(string Id, string ContractId, string Type, string DisplayName, string? Description, string LibraryId, string ClassName, string MethodName, string DllName, string DllVersion, string ReturnType, bool IsAwaitable, string? FlowLibraryName, IReadOnlyList<AiLibraryParameterContractDto> Parameters, LibraryContractIdentityConfidenceDto IdentityConfidence);
/// <summary>
/// Defines the API data contract for AI library parameter contract.
/// 定义AI库参数契约的 API 数据契约。
/// </summary>
public sealed record AiLibraryParameterContractDto(string Id, string Name, string Type, string? Description, bool Required, bool IsVariadic, string? ElementType, string? DefaultValue, IReadOnlyList<string>? Aliases, LibraryContractIdentityConfidenceDto IdentityConfidence, EnumParameterMetadataDto? EnumMetadata);
/// <summary>
/// Defines the API data contract for AI run summary.
/// 定义AI运行摘要的 API 数据契约。
/// </summary>
public sealed record AiRunSummaryDto(Guid Id, Guid ProjectId, Guid FlowId, long FlowVersion, string Status, string ExecutionKind, bool IsListenerRun, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, string? ErrorSummary, string? CancellationReason, Guid? DebugSessionId, string? DefinitionChecksum = null);
/// <summary>
/// Describes a AI run event.
/// 描述AI运行事件。
/// </summary>
public sealed record AiRunEventDto(Guid RunId, long Sequence, DateTimeOffset Timestamp, string Type, string? NodeId, JsonElement Payload, bool PayloadWasMalformed = false, bool PayloadWasTruncated = false);
/// <summary>
/// Carries a persisted AI node execution record.
/// 承载持久化的AI节点执行记录。
/// </summary>
public sealed record AiNodeExecutionRecordDto(Guid RunId, long Sequence, DateTimeOffset Timestamp, string NodeId, string Outcome, string? Branch, JsonElement Inputs, JsonElement Outputs, string? ErrorCode, string? ErrorMessage, bool InputsWereMalformed = false, bool OutputsWereMalformed = false, bool InputsWereTruncated = false, bool OutputsWereTruncated = false);
/// <summary>
/// Defines the API data contract for AI debug state.
/// 定义AI调试状态的 API 数据契约。
/// </summary>
public sealed record AiDebugStateDto(Guid Id, Guid RunId, Guid ProjectId, Guid FlowId, string Status, IReadOnlyList<string> BreakpointNodeIds, string? CurrentNodeId, Guid? ActiveInvocationId, string? ActiveFlipflopNodeId, int QueuedTriggerCount, long LastCommandSequence, string? FailureMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long StateRevision = 0, AiDebugPauseStateDto? PauseState = null, AiDebugNodeResultDto? LastNodeResult = null);
/// <summary>
/// Bounded discovery projection for active debug sessions. Detailed pause
/// inputs and node outputs remain available only through the single-session
/// debug state projection.
/// 活动调试会话的有界发现投影；详细暂停输入和节点输出仅通过单会话调试状态投影提供。
/// </summary>
public sealed record AiDebugSessionSummaryDto(Guid Id, Guid RunId, Guid ProjectId, Guid FlowId, string Status, IReadOnlyList<string> BreakpointNodeIds, string? CurrentNodeId, Guid? ActiveInvocationId, string? ActiveFlipflopNodeId, int QueuedTriggerCount, long LastCommandSequence, long StateRevision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
/// <summary>
/// Defines the API data contract for AI debug pause state.
/// 定义AI调试暂停状态的 API 数据契约。
/// </summary>
public sealed record AiDebugPauseStateDto(string NodeId, string NodeType, int Step, int FrameDepth, Guid? InvocationId, long BoundarySequence, JsonElement Inputs, DateTimeOffset PausedAt);
/// <summary>
/// Captures the outcome of AI debug node operations.
/// 记录AI调试节点操作的结果。
/// </summary>
public sealed record AiDebugNodeResultDto(string NodeId, long Sequence, DateTimeOffset CompletedAt, string Outcome, string? Branch, JsonElement Inputs, JsonElement Outputs, string? ErrorCode, string? ErrorMessage);
/// <summary>
/// Captures the outcome of AI debug state wait operations.
/// 记录AI调试状态等待操作的结果。
/// </summary>
public sealed record AiDebugStateWaitResultDto(bool HasChanged, bool TimedOut, AiDebugStateDto State);
/// <summary>
/// Defines the API data contract for AI run inspection.
/// 定义AI运行检查的 API 数据契约。
/// </summary>
public sealed record AiRunInspectionDto(AiRunSummaryDto Run, AiFlowTopologyDto? DefinitionSnapshot, AiPageDto<AiRunEventDto> Events, AiPageDto<AiNodeExecutionRecordDto> Outputs, AiDebugStateDto? DebugSession);
