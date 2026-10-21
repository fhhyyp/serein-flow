using System.ComponentModel.DataAnnotations;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpProjectQueryTools(AiReadModelService readModels, McpSecurityService security)
{
    [SereinFlowMcpTool("sereinflow_get_project", McpPermissionDto.ProjectRead)]
    public async Task<object?> GetProjectAsync(GetMcpProjectRequest request, SereinFlowMcpCaller caller, CancellationToken cancellationToken)
    {
        security.Require(caller.Principal, McpPermissionDto.ProjectRead, request.ProjectId);
        return await readModels.GetProjectAsync(request.ProjectId!.Value, cancellationToken);
    }
}

/// <summary>
/// Carries request data for get MCP project operations.
/// 承载获取MCP项目操作的请求数据。
/// </summary>
internal sealed record GetMcpProjectRequest([property: Required] Guid? ProjectId);
