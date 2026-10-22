using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpFlowComparisonTools(
    AiReadModelService _aiReadModelService,
    FlowDiffService _flowDiffService,
    McpSecurityService _mcpSecurityService)
{
    [SereinFlowMcpTool("sereinflow_compare_flow_versions", McpPermissionDto.ProjectRead)]
    internal async Task<FlowVersionComparisonDto> CompareVersionsAsync(
        SereinFlowMcpCaller context,
        McpCompareFlowVersionsRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var projectId = arguments.ProjectId;
        var flowId = arguments.FlowId;
        var track = arguments.Track;
        var fromVersion = arguments.FromVersion;
        var toVersion = arguments.ToVersion;
        _mcpSecurityService.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var fromVersionDetail = await _aiReadModelService.GetFlowVersionAsync(projectId, flowId, fromVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The source flow version was not found.");
        var toVersionDetail = await _aiReadModelService.GetFlowVersionAsync(projectId, flowId, toVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The target flow version was not found.");
        if (fromVersionDetail.Version.Track != track || toVersionDetail.Version.Track != track)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested versions are not on the selected track.");
        }

        var redactedSource = fromVersionDetail with
        {
            Definition = FlowDiffService.RedactSensitive(fromVersionDetail.Definition)
        };
        var redactedTarget = toVersionDetail with
        {
            Definition = FlowDiffService.RedactSensitive(toVersionDetail.Definition)
        };
        var diff = _flowDiffService.Compare(fromVersionDetail.Definition, toVersionDetail.Definition);
        return new FlowVersionComparisonDto(redactedSource, redactedTarget, FlowDiffService.RedactSensitive(diff));
    }
}
