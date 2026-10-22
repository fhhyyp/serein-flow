using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpProjectQueries(AiReadModelService service, McpSecurityService security)
{
    internal async Task<AiPageDto<AiProjectSummaryDto>> ReadProjectsAsync(
        McpPrincipal? principal,
        bool archivedOnly,
        CancellationToken cancellationToken,
        AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.ProjectRead);
        if (IsProjectScoped(principal))
        {
            var project = await service.GetProjectAsync(principal!.ProjectId!.Value, cancellationToken);
            var matchesRequestedState = project is not null
                && string.Equals(project.Status, ProjectStatus.Archived.ToString(), StringComparison.OrdinalIgnoreCase) == archivedOnly;
            return new AiPageDto<AiProjectSummaryDto>(AiReadModelContract.SchemaVersion, matchesRequestedState ? [project!] : [], false, null);
        }

        return archivedOnly ? await service.ListArchivedProjectsAsync(options, cancellationToken) : await service.ListProjectsAsync(options, cancellationToken);
    }

    internal async Task<AiProjectSummaryDto?> ReadProjectAsync(McpPrincipal? principal, Guid projectId, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        return await service.GetProjectAsync(projectId, cancellationToken);
    }
}
