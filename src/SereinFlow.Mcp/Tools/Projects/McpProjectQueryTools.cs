using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;
using static SereinFlow.Mcp.Support.McpReadOptions;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpProjectQueryTools(McpProjectQueries queries)
{
    [SereinFlowMcpTool("sereinflow_list_projects", SereinFlowPermission.ProjectRead)]
    internal Task<AiPageDto<AiProjectSummaryDto>> ListAsync(
        SereinFlowMcpCaller caller,
        McpReadOptionsRequest request,
        CancellationToken cancellationToken) => queries.ReadProjectsAsync(caller.Principal, false, cancellationToken, ReadOptions(request));
    [SereinFlowMcpTool("sereinflow_list_archived_projects", SereinFlowPermission.ProjectRead)]
    internal Task<AiPageDto<AiProjectSummaryDto>> ListArchivedAsync(
        SereinFlowMcpCaller caller,
        McpReadOptionsRequest request,
        CancellationToken cancellationToken) => queries.ReadProjectsAsync(caller.Principal, true, cancellationToken, ReadOptions(request));
    [SereinFlowMcpTool("sereinflow_get_project", SereinFlowPermission.ProjectRead)]
    internal Task<AiProjectSummaryDto?> GetAsync(
        SereinFlowMcpCaller caller,
        GetMcpProjectRequest request,
        CancellationToken cancellationToken) => queries.ReadProjectAsync(caller.Principal, request.ProjectId!.Value, cancellationToken);
}

internal sealed record GetMcpProjectRequest([property: System.ComponentModel.DataAnnotations.Required] Guid? ProjectId);
