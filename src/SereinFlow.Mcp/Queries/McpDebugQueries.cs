using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpReadOptions;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpDebugQueries(AiReadModelService service, SereinFlowAccess security)
{
    internal async Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(
        SereinFlowActor? principal,
        McpDebugStateWaitRequest arguments,
        CancellationToken cancellationToken)
    {
        await security.RequireAnyAsync(principal, null, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        var afterRevision = arguments.AfterRevision;
        var timeoutSeconds = arguments.TimeoutSeconds;
        if (afterRevision < 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'afterRevision' cannot be negative. MCP 参数 afterRevision 不能为负数。");
        }

        if (timeoutSeconds is < 0 or > 60)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'timeoutSeconds' must be between 0 and 60. MCP 参数 timeoutSeconds 必须介于 0 到 60 之间。");
        }

        var sessionId = arguments.SessionId;
        var fromSeconds = TimeSpan.FromSeconds(timeoutSeconds);
        var result = await service.WaitForDebugStateChangeAsync(sessionId, afterRevision, fromSeconds, cancellationToken);
        if (result is not null)
        {
            await security.RequireAnyAsync(principal, result.State.ProjectId, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        }

        return result;
    }

    internal async Task<AiDebugStateDto?> ReadDebugAsync(SereinFlowActor? principal, Guid sessionId, CancellationToken cancellationToken)
    {
        await security.RequireAnyAsync(principal, null, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        var state = await service.GetDebugStateAsync(sessionId, cancellationToken);
        if (state is not null)
        {
            await security.RequireAnyAsync(principal, state.ProjectId, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
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
        var projectId = requestedProjectId;
        var scope = await security.GetReadScopeAsync(principal, cancellationToken);
        await security.RequireAnyAsync(principal, projectId, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        var options = ReadOptions(arguments);
        return await service.ListActiveDebugSessionsAsync(projectId, options, cancellationToken, scope);
    }
}
