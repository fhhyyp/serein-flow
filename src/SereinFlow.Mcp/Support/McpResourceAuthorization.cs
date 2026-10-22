using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Mcp.Support;

internal sealed class McpResourceAuthorization(IProjectRepository _projectRepository, McpSecurityService security)
{
    internal async Task<Project> RequireActiveProjectAsync(
        McpPrincipal principal,
        Guid projectId,
        McpPermissionDto permission,
        CancellationToken cancellationToken)
    {
        security.Require(principal, permission, projectId);
        var project = await _projectRepository.FindAsync(projectId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The project was not found. 未找到项目。");
        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "Archived projects cannot be changed by this MCP operation. 此 MCP 操作不能更改已归档的项目。", new { code = ProjectErrorCodes.Archived });
        }

        return project;
    }
}
