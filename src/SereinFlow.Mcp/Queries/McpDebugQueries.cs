using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpReadOptions;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpDebugQueries(AiReadModelService service, McpSecurityService security)
{
    internal async Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(
        McpPrincipal? principal,
        McpDebugStateWaitRequest arguments,
        CancellationToken cancellationToken)
    {
        security.RequireAny(principal, null, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var afterRevision = arguments.AfterRevision;
        var timeoutSeconds = arguments.TimeoutSeconds;
        if (afterRevision < 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'afterRevision' cannot be negative.");
        }

        if (timeoutSeconds is < 0 or > 60)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'timeoutSeconds' must be between 0 and 60.");
        }

        var sessionId = arguments.SessionId;
        var fromSeconds = TimeSpan.FromSeconds(timeoutSeconds);
        var result = await service.WaitForDebugStateChangeAsync(sessionId, afterRevision, fromSeconds, cancellationToken);
        if (result is not null)
        {
            security.RequireAny(principal, result.State.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        }

        return result;
    }

    internal async Task<AiDebugStateDto?> ReadDebugAsync(McpPrincipal? principal, Guid sessionId, CancellationToken cancellationToken)
    {
        security.RequireAny(principal, null, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var state = await service.GetDebugStateAsync(sessionId, cancellationToken);
        if (state is not null)
        {
            security.RequireAny(principal, state.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        }

        return state;
    }

    internal async Task<AiPageDto<AiDebugSessionSummaryDto>> ListDebugSessionsAsync(
        SereinFlowMcpCaller context,
        McpListDebugSessionsRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var requestedProjectId = arguments.ProjectId;
        var projectId = IsProjectScoped(principal) ? principal.ProjectId : requestedProjectId;
        security.RequireAny(principal, projectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var options = ReadOptions(arguments);
        return await service.ListActiveDebugSessionsAsync(projectId, options, cancellationToken);
    }
}
