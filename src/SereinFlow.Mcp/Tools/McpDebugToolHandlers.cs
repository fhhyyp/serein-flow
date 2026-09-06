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
    internal static Task<object> ReadRunsResourceAsync(
        McpToolContext context,
        CancellationToken cancellationToken)
        => ListRunsAsync(context, default, cancellationToken);

    internal static Task<object> ReadDebugSessionsResourceAsync(
        McpToolContext context,
        CancellationToken cancellationToken)
        => ListDebugSessionsAsync(context, default, cancellationToken);

    internal static async Task<object> ListRunsAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var requestedProjectId = TryGetGuid(arguments, "projectId");
        var projectId = IsProjectScoped(principal)
            ? principal.ProjectId
            : requestedProjectId;
        context.Security.RequireAny(
            principal,
            projectId,
            McpPermissionDto.DebugRead,
            McpPermissionDto.RunRead);
        var status = ReadOptionalRunStatus(arguments);
        return await context.Services.GetRequiredService<AiReadModelService>()
            .ListRunsAsync(projectId, status, ReadOptions(arguments), cancellationToken);
    }

    internal static async Task<object> ListDebugSessionsAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var requestedProjectId = TryGetGuid(arguments, "projectId");
        var projectId = IsProjectScoped(principal)
            ? principal.ProjectId
            : requestedProjectId;
        context.Security.RequireAny(
            principal,
            projectId,
            McpPermissionDto.DebugRead,
            McpPermissionDto.RunRead);
        return await context.Services.GetRequiredService<AiReadModelService>()
            .ListActiveDebugSessionsAsync(projectId, ReadOptions(arguments), cancellationToken);
    }

    internal static async Task<object> StartAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = Deserialize<McpStartFlowDebugSessionRequestDto>(arguments);
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        request = request with { IdempotencyKey = idempotencyKey };
        var requestPayload = Serialize(request);
        var idempotency = context.Services.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(
            principal.Id,
            DebugErrorCodes.SessionStart,
            idempotencyKey,
            requestPayload,
            cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);

        var apiRequest = new StartFlowDebugSessionRequestDto(
            request.BreakpointNodeIds,
            request.ProjectInputs,
            request.TimeoutSeconds,
            request.MaxSteps,
            request.MaxNodeVisits,
            request.ExpectedFlowVersion,
            request.MaxQueuedFlipflopTriggers);
        var result = await context.Services.GetRequiredService<IFlowDebugSessionService>()
            .CreateAsync(request.ProjectId, request.FlowId, apiRequest, cancellationToken);
        if (!result.IsAccepted)
            ThrowStartFailure(result);

        var session = result.Session!;
        var response = new McpDebugSessionStartedDto(
            session.Id,
            session.RunId,
            session.ProjectId,
            session.FlowId,
            (FlowDebugSessionStatusDto)session.Status,
            session.StateRevision);
        await idempotency.SaveAsync(
            principal.Id,
            DebugErrorCodes.SessionStart,
            idempotencyKey,
            response,
            requestPayload,
            cancellationToken);
        return response;
    }

    internal static Task<object> ContinueAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
        => SendCommandAsync(context, arguments, "continue", static (service, sessionId, sequence, token) =>
            service.ContinueAsync(sessionId, sequence, token), cancellationToken);

    internal static Task<object> StepAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
        => SendCommandAsync(context, arguments, "step", static (service, sessionId, sequence, token) =>
            service.StepAsync(sessionId, sequence, token), cancellationToken);

    internal static Task<object> StopAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
        => SendCommandAsync(context, arguments, "stop", static (service, sessionId, sequence, token) =>
            service.StopAsync(sessionId, sequence, token), cancellationToken);

    private static async Task<object> SendCommandAsync(
        McpToolContext context,
        JsonElement arguments,
        string command,
        Func<IFlowDebugSessionService, Guid, long, CancellationToken, Task<FlowDebugSessionCommandResult>> send,
        CancellationToken cancellationToken)
    {
        var sessionId = GetGuid(arguments, "sessionId");
        var commandSequence = GetLong(arguments, "commandSequence");
        var service = context.Services.GetRequiredService<IFlowDebugSessionService>();
        var session = await service.FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.ResourceNotFound,
                "Debug session not found. 未找到调试会话。",
                new { code = DebugErrorCodes.SessionNotFound });
        }

        context.Security.Require(context.Principal, McpPermissionDto.DebugControl, session.ProjectId);
        var result = await send(service, sessionId, commandSequence, cancellationToken);
        if (!result.IsAccepted)
            ThrowCommandFailure(result);

        var latest = await service.FindAsync(sessionId, cancellationToken) ?? session;
        return new McpDebugCommandAcceptedDto(
            latest.Id,
            latest.RunId,
            command,
            commandSequence,
            (FlowDebugSessionStatusDto)latest.Status,
            latest.StateRevision);
    }

    private static FlowRunStatusDto? ReadOptionalRunStatus(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "status");
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<FlowRunStatusDto>(value, true, out var status) && Enum.IsDefined(status)
                ? status
                : throw new McpProtocolException(
                    McpProtocolErrorCodes.InvalidParams,
                    "The run status must be pending, running, succeeded, failed, cancelled, timedOut or interrupted.",
                    new { code = McpErrorCodes.InvalidArguments, path = "status" });
    }

    private static void ThrowStartFailure(FlowDebugSessionStartResult result)
    {
        var code = result.ErrorCode ?? DebugErrorCodes.StartRejected;
        throw new McpProtocolException(
            ToProtocolCode(result.StatusCode),
            result.ErrorTitle ?? "The debug session could not be started.",
            new { code, details = result.ErrorBody });
    }

    private static void ThrowCommandFailure(FlowDebugSessionCommandResult result)
    {
        var code = result.ErrorCode ?? DebugErrorCodes.CommandRejected;
        throw new McpProtocolException(
            ToProtocolCode(result.StatusCode),
            result.ErrorTitle ?? "The debug command was rejected.",
            new { code });
    }

    private static int ToProtocolCode(int statusCode)
        => McpProtocolErrorCodes.FromHttpStatus(statusCode);
}
