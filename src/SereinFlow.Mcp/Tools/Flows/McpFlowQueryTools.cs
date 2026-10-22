using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;
using static SereinFlow.Mcp.Support.McpReadOptions;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpFlowQueryTools(McpFlowQueries queries)
{
    [SereinFlowMcpTool("sereinflow_get_flow_topology", McpPermissionDto.ProjectRead)]
    internal Task<AiFlowTopologyDto?> GetTopologyAsync(
        SereinFlowMcpCaller caller,
        McpFlowTopologyRequest request,
        CancellationToken cancellationToken) => queries.ReadFlowTopologyAsync(caller.Principal, request.ProjectId, request.FlowId, request.Track, request.Version, ReadOptions(request), cancellationToken);
    [SereinFlowMcpTool("sereinflow_get_flow_edit_model", McpPermissionDto.ProjectRead)]
    internal Task<AiFlowEditModelDto?> GetEditModelAsync(
        SereinFlowMcpCaller caller,
        McpFlowEditRequest request,
        CancellationToken cancellationToken) => queries.ReadFlowEditModelAsync(caller.Principal, request.ProjectId, request.FlowId, ReadOptions(request), cancellationToken);
}
