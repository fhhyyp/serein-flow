using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

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
internal class McpFlowEditRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }

    [McpParameter(Required = true)]
    public Guid FlowId { get; set; }
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
