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
        var project = await _projectRepository.FindAsync(projectId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The project was not found.");
        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "Archived projects cannot be changed by this MCP operation.", new { code = ProjectErrorCodes.Archived });
        }

        return project;
    }
}
