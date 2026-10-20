using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;
/// <summary>
/// MCP discovery and control handlers for the durable debug session model.
/// Detailed state remains on the read-only debug tools; mutations return only
/// bounded acknowledgements.
/// </summary>
internal static class McpDebugToolHandlers
{
    internal static Task<object> ReadRunsResourceAsync(McpToolContext context, CancellationToken cancellationToken)
    {
        var listRunsAsyncResult = ListRunsAsync(context, default, cancellationToken);
        return listRunsAsyncResult;
    }

    internal static Task<object> ReadDebugSessionsResourceAsync(McpToolContext context, CancellationToken cancellationToken)
    {
        var listDebugSessionsAsyncResult = ListDebugSessionsAsync(context, default, cancellationToken);
        return listDebugSessionsAsyncResult;
    }

    internal static async Task<object> ListRunsAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var requestedProjectId = TryGetGuid(arguments, "projectId");
        var projectId = IsProjectScoped(principal) ? principal.ProjectId : requestedProjectId;
        context.Security.RequireAny(principal, projectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var status = ReadOptionalRunStatus(arguments);
        var getRequiredServiceResult = context.Services.GetRequiredService<AiReadModelService>();
        var readOptionsResult = ReadOptions(arguments);
        var listRunsAsyncResult1 = await getRequiredServiceResult.ListRunsAsync(projectId, status, readOptionsResult, cancellationToken);
        return listRunsAsyncResult1;
    }

    internal static async Task<object> ListDebugSessionsAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var requestedProjectId = TryGetGuid(arguments, "projectId");
        var projectId = IsProjectScoped(principal) ? principal.ProjectId : requestedProjectId;
        context.Security.RequireAny(principal, projectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var getRequiredServiceResult2 = context.Services.GetRequiredService<AiReadModelService>();
        var readOptionsResult3 = ReadOptions(arguments);
        var listActiveDebugSessionsAsyncResult = await getRequiredServiceResult2.ListActiveDebugSessionsAsync(projectId, readOptionsResult3, cancellationToken);
        return listActiveDebugSessionsAsyncResult;
    }

    internal static async Task<object> StartAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = Deserialize<McpStartFlowDebugSessionRequestDto>(arguments);
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        request = request with
        {
            IdempotencyKey = idempotencyKey
        };
        var requestPayload = Serialize(request);
        var idempotency = context.Services.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, DebugErrorCodes.SessionStart, idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var apiRequest = new StartFlowDebugSessionRequestDto(request.BreakpointNodeIds, request.ProjectInputs, request.TimeoutSeconds, request.MaxSteps, request.MaxNodeVisits, request.ExpectedFlowVersion, request.MaxQueuedFlipflopTriggers);
        var getRequiredServiceResult4 = context.Services.GetRequiredService<IFlowDebugSessionService>();
        var result = await getRequiredServiceResult4.CreateAsync(request.ProjectId, request.FlowId, apiRequest, cancellationToken);
        if (!result.IsAccepted)
        {
            ThrowStartFailure(result);
        }

        var session = result.Session!;
        var response = new McpDebugSessionStartedDto(session.Id, session.RunId, session.ProjectId, session.FlowId, (FlowDebugSessionStatusDto)session.Status, session.StateRevision);
        await idempotency.SaveAsync(principal.Id, DebugErrorCodes.SessionStart, idempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    internal static Task<object> ContinueAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var sendCommandAsyncResult = SendCommandAsync(context, arguments, "continue", static (service, sessionId, sequence, token) =>
        {
            var continueAsyncResult = service.ContinueAsync(sessionId, sequence, token);
            return continueAsyncResult;
        }, cancellationToken);
        return sendCommandAsyncResult;
    }

    internal static Task<object> StepAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var sendCommandAsyncResult5 = SendCommandAsync(context, arguments, "step", static (service, sessionId, sequence, token) =>
        {
            var stepAsyncResult = service.StepAsync(sessionId, sequence, token);
            return stepAsyncResult;
        }, cancellationToken);
        return sendCommandAsyncResult5;
    }

    internal static Task<object> StopAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var sendCommandAsyncResult6 = SendCommandAsync(context, arguments, "stop", static (service, sessionId, sequence, token) =>
        {
            var stopAsyncResult = service.StopAsync(sessionId, sequence, token);
            return stopAsyncResult;
        }, cancellationToken);
        return sendCommandAsyncResult6;
    }

    private static async Task<object> SendCommandAsync(McpToolContext context, JsonElement arguments, string command, Func<IFlowDebugSessionService, Guid, long, CancellationToken, Task<FlowDebugSessionCommandResult>> send, CancellationToken cancellationToken)
    {
        var sessionId = GetGuid(arguments, "sessionId");
        var commandSequence = GetLong(arguments, "commandSequence");
        var service = context.Services.GetRequiredService<IFlowDebugSessionService>();
        var session = await service.FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "Debug session not found. 未找到调试会话。", new { code = DebugErrorCodes.SessionNotFound });
        }

        context.Security.Require(context.Principal, McpPermissionDto.DebugControl, session.ProjectId);
        var result = await send(service, sessionId, commandSequence, cancellationToken);
        if (!result.IsAccepted)
        {
            ThrowCommandFailure(result);
        }

        var latest = await service.FindAsync(sessionId, cancellationToken) ?? session;
        var mcpDebugCommandAcceptedDto = new McpDebugCommandAcceptedDto(latest.Id, latest.RunId, command, commandSequence, (FlowDebugSessionStatusDto)latest.Status, latest.StateRevision);
        return mcpDebugCommandAcceptedDto;
    }

    private static FlowRunStatusDto? ReadOptionalRunStatus(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "status");
        FlowRunStatusDto? evaluatedValue = string.IsNullOrWhiteSpace(value) ? null : Enum.TryParse<FlowRunStatusDto>(value, true, out var status) && Enum.IsDefined(status) ? status : throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The run status must be pending, running, succeeded, failed, cancelled, timedOut or interrupted.", new { code = McpErrorCodes.InvalidArguments, path = "status" });
        return evaluatedValue;
    }

    private static void ThrowStartFailure(FlowDebugSessionStartResult result)
    {
        var code = result.ErrorCode ?? DebugErrorCodes.StartRejected;
        var toProtocolCodeResult = ToProtocolCode(result.StatusCode);
        throw new McpProtocolException(toProtocolCodeResult, result.ErrorTitle ?? "The debug session could not be started.", new { code, details = result.ErrorBody });
    }

    private static void ThrowCommandFailure(FlowDebugSessionCommandResult result)
    {
        var code = result.ErrorCode ?? DebugErrorCodes.CommandRejected;
        var toProtocolCodeResult7 = ToProtocolCode(result.StatusCode);
        throw new McpProtocolException(toProtocolCodeResult7, result.ErrorTitle ?? "The debug command was rejected.", new { code });
    }

    private static int ToProtocolCode(int statusCode)
    {
        var fromHttpStatusResult = McpProtocolErrorCodes.FromHttpStatus(statusCode);
        return fromHttpStatusResult;
    }
}
