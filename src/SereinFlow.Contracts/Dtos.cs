namespace SereinFlow.Contracts;

public static class WorkerProtocol
{
    public const int Version = 1;
}

public enum NodeTypeDto
{
    Action,
    FlowCall,
    GlobalData,
    Flipflop,
    Script,
    Condition,
    Value,
    Expression,
    Trigger
}

public enum CanvasLifecycleDto
{
    Main,
    Init,
    Loading,
    Exit
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
    Error,
    Upstream
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
    TimedOut
}

public enum WorkerEventType
{
    RunStarted,
    NodeStarted,
    NodeCompleted,
    NodeFailed,
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

public sealed record FlowDefinitionSummaryDto(Guid Id, long Version, string EntryNodeId);

public sealed record ProjectWorkspaceDto(ProjectDto Project, IReadOnlyList<FlowDefinitionSummaryDto> Flows);

public sealed record CreateProjectRequestDto(string Name, FlowDefinitionDto Definition);

public sealed record UpdateFlowDefinitionRequestDto(long ExpectedVersion, FlowDefinitionDto Definition);

public sealed record CanvasDto(
    string Id,
    CanvasLifecycleDto Lifecycle,
    IReadOnlyList<NodeDto> Nodes,
    IReadOnlyList<ConnectionDto> Connections);

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
    double? Width);

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
    string? SourcePortId);

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

public sealed record ScriptValueContractDto(string Name, string ValueKind, bool Required);

public sealed record FlowDefinitionDto(
    Guid Id,
    int SchemaVersion,
    long Version,
    IReadOnlyList<CanvasDto> Canvases,
    string EntryNodeId,
    string Checksum);

public sealed record PluginManifestDto(
    string AssemblyName,
    string AssemblyVersion,
    string Sha256,
    IReadOnlyList<string> TargetRids,
    string ApiVersion,
    IReadOnlyList<string> NodeTypes);

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
    string? ErrorSummary);

public sealed record FlowRunEventDto(
    Guid RunId,
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string? NodeId,
    string PayloadJson);

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
    DateTimeOffset Deadline);

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
