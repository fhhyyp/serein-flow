using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

[McpBindProperties]
internal class McpRunInspectionRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid RunId { get; set; }

    [McpParameter(IgnoreWhitespace = true, AllowNumericEnum = true, ErrorMessage = "The flow version track must be development or production. 流程版本轨道必须为 development 或 production。")]
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
internal class McpListRunsRequest : McpReadOptionsRequest
{
    public Guid? ProjectId { get; set; }

    [McpParameter(IgnoreWhitespace = true, AllowNumericEnum = true, ErrorMessage = "The run status must be pending, running, succeeded, failed, cancelled, timedOut or interrupted. 运行状态必须为 pending、running、succeeded、failed、cancelled、timedOut 或 interrupted。", ErrorCode = McpErrorCodes.InvalidArguments, ErrorPath = "status")]
    public FlowRunStatusDto? Status { get; set; }
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
    [McpParameter(Required = true, AllowEmpty = true, ErrorMessage = "Variable name is required. 必须提供变量名称。")]
    public string Name { get; set; } = string.Empty;

    [McpParameter(Required = true, Trim = true)]
    public string ValueType { get; set; } = string.Empty;

    [McpParameter(Required = true, AllowNull = true, ErrorMessage = "An explicit JSON value is required. 必须显式提供 JSON 值。", ErrorCode = VariableErrorCodes.ValueInvalid)]
    public JsonElement Value { get; set; }

    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpPublishRunMessageRequest
{
    [McpParameter(Required = true, NonEmptyGuid = true, ErrorMessage = "MCP parameter 'runId' must be a non-empty GUID. MCP 参数 runId 必须是非空 GUID。", ErrorCode = McpErrorCodes.InvalidArguments, ErrorPath = "runId")]
    public Guid RunId { get; set; }

    [McpParameter(Required = true, Trim = true, AllowEmpty = true, ErrorMessage = "MCP parameter 'topic' is required and must be a string. 必须提供字符串类型的 MCP 参数 topic。", ErrorCode = McpErrorCodes.InvalidArguments, ErrorPath = "topic")]
    public string Topic { get; set; } = string.Empty;

    [McpParameter(Required = true, AllowNull = true, ErrorMessage = "MCP parameter 'payload' is required and must be a JSON value. 必须提供 JSON 值类型的 MCP 参数 payload。", ErrorCode = MessageErrorCodes.PayloadRequired, ErrorPath = "payload")]
    public JsonElement Payload { get; set; }

    [McpParameter(ErrorMessage = "The message channel kind must be 'queue' or 'eventBus'. 消息通道类型必须是 queue 或 eventBus。", ErrorCode = MessageErrorCodes.ChannelInvalid, ErrorPath = "channelKind")]
    public WorkerMessageChannelKindDto ChannelKind { get; set; } = WorkerMessageChannelKindDto.Queue;
    public string? ContractId { get; set; }
    public string? MessageId { get; set; }

    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
