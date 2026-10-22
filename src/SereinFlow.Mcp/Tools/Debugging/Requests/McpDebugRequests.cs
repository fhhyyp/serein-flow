
namespace SereinFlow.Mcp;

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
