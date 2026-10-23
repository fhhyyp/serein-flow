using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpProjectQueries(AiReadModelService service, SereinFlowAccess security)
{
    internal async Task<AiPageDto<AiProjectSummaryDto>> ReadProjectsAsync(
        SereinFlowActor? principal,
        bool archivedOnly,
        CancellationToken cancellationToken,
        AiReadModelOptions? options = null)
    {
        await security.RequireAsync(principal, SereinFlowPermission.ProjectRead, cancellationToken: cancellationToken);
        var scope = await security.GetScopeAsync(principal!, SereinFlowPermission.ProjectRead, cancellationToken);
        return archivedOnly
            ? await service.ListArchivedProjectsAsync(options, cancellationToken, scope)
            : await service.ListProjectsAsync(options, cancellationToken, scope);
    }

    internal async Task<AiProjectSummaryDto?> ReadProjectAsync(SereinFlowActor? principal, Guid projectId, CancellationToken cancellationToken)
    {
        await security.RequireAsync(principal, SereinFlowPermission.ProjectRead, projectId, cancellationToken: cancellationToken);
        return await service.GetProjectAsync(projectId, cancellationToken);
    }
}
