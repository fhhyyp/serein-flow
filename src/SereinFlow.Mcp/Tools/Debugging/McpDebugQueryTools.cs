using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpDebugQueryTools(McpDebugQueries queries)
{
    [SereinFlowMcpTool("sereinflow_get_debug_state", SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead)]
    internal Task<AiDebugStateDto?> GetStateAsync(
        SereinFlowMcpCaller caller,
        McpDebugStateRequest request,
        CancellationToken cancellationToken) => queries.ReadDebugAsync(caller.Principal, request.SessionId, cancellationToken);
    [SereinFlowMcpTool("sereinflow_wait_debug_state", SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead)]
    internal Task<AiDebugStateWaitResultDto?> WaitAsync(
        SereinFlowMcpCaller caller,
        McpDebugStateWaitRequest request,
        CancellationToken cancellationToken) => queries.WaitForDebugStateAsync(caller.Principal, request, cancellationToken);
    [SereinFlowMcpTool("sereinflow_list_debug_sessions", SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead)]
    internal Task<AiPageDto<AiDebugSessionSummaryDto>> ListAsync(
        SereinFlowMcpCaller caller,
        McpListDebugSessionsRequest request,
        CancellationToken cancellationToken) => queries.ListDebugSessionsAsync(caller, request, cancellationToken);
}
