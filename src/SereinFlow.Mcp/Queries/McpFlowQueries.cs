using SereinFlow.Application;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.Support.McpErrors;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpFlowQueries(
    AiReadModelService service,
    IBuiltinNodeCatalog _builtinNodeCatalog,
    McpSecurityService security)
{
    internal async Task<AiFlowTopologyDto?> ReadFlowTopologyAsync(
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        FlowVersionTrackDto track,
        long? version,
        AiReadModelOptions? options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        return await service.GetFlowTopologyAsync(projectId, flowId, track, version, options, cancellationToken);
    }

    internal async Task<AiFlowEditModelDto?> ReadFlowEditModelAsync(
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        var getCatalog = _builtinNodeCatalog.GetCatalog();
        return await service.GetFlowEditModelAsync(projectId, flowId, getCatalog, options, cancellationToken);
    }

    internal async Task<IReadOnlyList<FlowVersionSummaryDto>> ReadVersionsAsync(
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        string trackText,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        return await service.GetFlowVersionHistoryAsync(projectId, flowId, track, cancellationToken);
    }

    internal async Task<FlowVersionDetailDto?> ReadVersionAsync(
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        string trackText,
        long version,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var detail = await service.GetFlowVersionAsync(projectId, flowId, version, cancellationToken);
        if (detail is null || detail.Version.Track != track)
        {
            return null;
        }

        return detail with
        {
            Definition = FlowDiffService.RedactSensitive(detail.Definition)
        };
    }
}
