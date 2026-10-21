using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

[McpBindProperties]
internal class McpReadOptionsRequest
{
    public int MaxItems { get; set; } = 200;
    public int MaxJsonBytes { get; set; } = 64 * 1024;
    public bool IncludeFlowLiteralValues { get; set; } = false;
    public bool IncludeScriptSource { get; set; } = false;
}

[McpBindProperties]
internal class McpFlowTopologyRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
    [McpParameter(Required = true)]
    public Guid FlowId { get; set; }
    [McpParameter(IgnoreWhitespace = true, AllowNumericEnum = true, ErrorMessage = "The flow version track must be development or production.")]
    public FlowVersionTrackDto Track { get; set; } = FlowVersionTrackDto.Development;
    public long? Version { get; set; }
}

[McpBindProperties]
internal class McpListLibrariesRequest : McpReadOptionsRequest
{
    public bool IncludeArchived { get; set; } = false;
}

[McpBindProperties]
internal class McpListLibraryFamiliesRequest : McpReadOptionsRequest
{
    public bool IncludeArchivedArtifacts { get; set; } = true;
}

[McpBindProperties]
internal class McpLibraryQueryRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string LibraryId { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpLibraryFamilyQueryRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string FamilyId { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpProjectLibrariesRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
}

[McpBindProperties]
internal class McpLibraryUpgradeQueryRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
    [McpParameter(Required = true)]
    public Guid UpgradeId { get; set; }
}

[McpBindProperties]
internal class McpRunInspectionRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid RunId { get; set; }
    [McpParameter(IgnoreWhitespace = true, AllowNumericEnum = true, ErrorMessage = "The flow version track must be development or production.")]
    public FlowVersionTrackDto? Track { get; set; }
    public long AfterEventSequence { get; set; } = 0;
}

[McpBindProperties]
internal class McpRunQueryRequest
{
    [McpParameter(Required = true)]
    public Guid RunId { get; set; }
}

[McpBindProperties]
internal class McpRunWorkpieceQueryRequest : McpRunQueryRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string WorkpieceId { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpDebugStateRequest
{
    [McpParameter(Required = true)]
    public Guid SessionId { get; set; }
}

[McpBindProperties]
internal class McpDebugStateWaitRequest : McpDebugStateRequest
{
    [McpParameter(Required = true)]
    public long AfterRevision { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
}

[McpBindProperties]
internal class McpFlowEditRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
    [McpParameter(Required = true)]
    public Guid FlowId { get; set; }
}

[McpBindProperties]
internal class McpListRunsRequest : McpReadOptionsRequest
{
    public Guid? ProjectId { get; set; }
    [McpParameter(IgnoreWhitespace = true, AllowNumericEnum = true, ErrorMessage = "The run status must be pending, running, succeeded, failed, cancelled, timedOut or interrupted.", ErrorCode = McpErrorCodes.InvalidArguments, ErrorPath = "status")]
    public FlowRunStatusDto? Status { get; set; }
}

[McpBindProperties]
internal class McpListDebugSessionsRequest : McpReadOptionsRequest
{
    public Guid? ProjectId { get; set; }
}

[McpBindProperties]
internal class McpDebugCommandRequest : McpDebugStateRequest
{
    [McpParameter(Required = true)]
    public long CommandSequence { get; set; }
}

[McpBindProperties]
internal class McpCompareFlowVersionsRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
    [McpParameter(Required = true)]
    public Guid FlowId { get; set; }
    [McpParameter(Required = true, AllowNumericEnum = true, ErrorMessage = "The flow version track must be development or production.")]
    public FlowVersionTrackDto Track { get; set; }
    [McpParameter(Required = true)]
    public long FromVersion { get; set; }
    [McpParameter(Required = true)]
    public long ToVersion { get; set; }
}

[McpBindProperties]
internal class McpLibraryPackagePreviewRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string FileName { get; set; } = string.Empty;
    [McpParameter(Required = true, Trim = true)]
    public string PackageBase64 { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? FamilyId { get; set; }
    public string? BaselineArtifactId { get; set; }
}

[McpBindProperties]
internal class McpGetRunVariablesRequest : McpRunQueryRequest
{
    public string? Name { get; set; }
    public int Offset { get; set; } = 0;
    public int Take { get; set; } = 100;
}

[McpBindProperties]
internal class McpSetRunVariableRequest : McpRunQueryRequest
{
    [McpParameter(Required = true, AllowEmpty = true, ErrorMessage = "Variable name is required.")]
    public string Name { get; set; } = string.Empty;
    [McpParameter(Required = true, Trim = true)]
    public string ValueType { get; set; } = string.Empty;
    [McpParameter(Required = true, AllowNull = true, ErrorMessage = "An explicit JSON value is required.", ErrorCode = VariableErrorCodes.ValueInvalid)]
    public JsonElement Value { get; set; }
    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpPublishRunMessageRequest
{
    [McpParameter(Required = true, NonEmptyGuid = true, ErrorMessage = "MCP parameter 'runId' must be a non-empty GUID.", ErrorCode = McpErrorCodes.InvalidArguments, ErrorPath = "runId")]
    public Guid RunId { get; set; }
    [McpParameter(Required = true, Trim = true, AllowEmpty = true, ErrorMessage = "MCP parameter 'topic' is required and must be a string.", ErrorCode = McpErrorCodes.InvalidArguments, ErrorPath = "topic")]
    public string Topic { get; set; } = string.Empty;
    [McpParameter(Required = true, AllowNull = true, ErrorMessage = "MCP parameter 'payload' is required and must be a JSON value.", ErrorCode = MessageErrorCodes.PayloadRequired, ErrorPath = "payload")]
    public JsonElement Payload { get; set; }
    [McpParameter(ErrorMessage = "The message channel kind must be 'queue' or 'eventBus'. 消息通道类型必须是 queue 或 eventBus。", ErrorCode = MessageErrorCodes.ChannelInvalid, ErrorPath = "channelKind")]
    public WorkerMessageChannelKindDto ChannelKind { get; set; } = WorkerMessageChannelKindDto.Queue;
    public string? ContractId { get; set; }
    public string? MessageId { get; set; }
    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpCreateApiKeyRequest
{
    public Guid? ProjectId { get; set; }
    [McpParameter(Required = true, Trim = true)]
    public string Name { get; set; } = string.Empty;
    [McpParameter(Required = true)]
    public IReadOnlyList<McpPermissionDto> Permissions { get; set; } = Array.Empty<McpPermissionDto>();
    [McpParameter(IgnoreWhitespace = true, ErrorMessage = "MCP parameter 'expiresAt' must be an ISO date.")]
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsAdministrator { get; set; } = false;
    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpRotateApiKeyRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string KeyId { get; set; } = string.Empty;
    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
