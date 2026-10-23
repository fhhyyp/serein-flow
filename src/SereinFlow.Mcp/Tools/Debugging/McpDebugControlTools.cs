using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpJson;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpDebugControlTools(
    IFlowDebugSessionService _flowDebugSessionService,
    McpIdempotencyService _mcpIdempotencyService,
    SereinFlowAccess _mcpSecurityService)
{
    [SereinFlowMcpTool("sereinflow_start_debug_session", SereinFlowPermission.DebugControl, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> StartAsync(
        SereinFlowMcpCaller context,
        McpStartFlowDebugSessionRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        await _mcpSecurityService.RequireAsync(principal, SereinFlowPermission.DebugControl, request.ProjectId, cancellationToken: cancellationToken);
        var idempotencyKey = arguments.IdempotencyKey!.Trim();
        request = request with
        {
            IdempotencyKey = idempotencyKey
        };
        var requestPayload = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, DebugErrorCodes.SessionStart, idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var apiRequest = new StartFlowDebugSessionRequestDto(request.BreakpointNodeIds, request.ProjectInputs, request.TimeoutSeconds, request.MaxSteps, request.MaxNodeVisits, request.ExpectedFlowVersion, request.MaxQueuedFlipflopTriggers);
        var result = await _flowDebugSessionService.CreateAsync(request.ProjectId, request.FlowId, apiRequest, cancellationToken);
        if (!result.IsAccepted)
        {
            ThrowStartFailure(result);
        }

        var session = result.Session!;
        var response = new McpDebugSessionStartedDto(session.Id, session.RunId, session.ProjectId, session.FlowId, (FlowDebugSessionStatusDto)session.Status, session.StateRevision);
        await _mcpIdempotencyService.SaveAsync(principal.Id, DebugErrorCodes.SessionStart, idempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    [SereinFlowMcpTool("sereinflow_continue_debug", SereinFlowPermission.DebugControl, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal Task<McpDebugCommandAcceptedDto> ContinueAsync(
        SereinFlowMcpCaller context,
        McpDebugCommandRequest arguments,
        CancellationToken cancellationToken)
    {
        return SendCommandAsync(context, arguments, "continue", static (service, sessionId, sequence, token) =>
        {
            return service.ContinueAsync(sessionId, sequence, token);
        }, cancellationToken);
    }

    [SereinFlowMcpTool("sereinflow_step_debug", SereinFlowPermission.DebugControl, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal Task<McpDebugCommandAcceptedDto> StepAsync(
        SereinFlowMcpCaller context,
        McpDebugCommandRequest arguments,
        CancellationToken cancellationToken)
    {
        return SendCommandAsync(context, arguments, "step", static (service, sessionId, sequence, token) =>
        {
            return service.StepAsync(sessionId, sequence, token);
        }, cancellationToken);
    }

    [SereinFlowMcpTool("sereinflow_stop_debug", SereinFlowPermission.DebugControl, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal Task<McpDebugCommandAcceptedDto> StopAsync(
        SereinFlowMcpCaller context,
        McpDebugCommandRequest arguments,
        CancellationToken cancellationToken)
    {
        return SendCommandAsync(context, arguments, "stop", static (service, sessionId, sequence, token) =>
        {
            return service.StopAsync(sessionId, sequence, token);
        }, cancellationToken);
    }

    private async Task<McpDebugCommandAcceptedDto> SendCommandAsync(
        SereinFlowMcpCaller context,
        McpDebugCommandRequest arguments,
        string command,
        Func<IFlowDebugSessionService, Guid, long, CancellationToken, Task<FlowDebugSessionCommandResult>> send,
        CancellationToken cancellationToken)
    {
        var sessionId = arguments.SessionId;
        var commandSequence = arguments.CommandSequence;
        var session = await _flowDebugSessionService.FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "Debug session not found. 未找到调试会话。", new { code = DebugErrorCodes.SessionNotFound });
        }

        await _mcpSecurityService.RequireAsync(context.Principal, SereinFlowPermission.DebugControl, session.ProjectId, cancellationToken: cancellationToken);
        var result = await send(_flowDebugSessionService, sessionId, commandSequence, cancellationToken);
        if (!result.IsAccepted)
        {
            ThrowCommandFailure(result);
        }

        // Keep the acknowledgement cursor at command acceptance, so a fast
        // Worker pause remains visible to the next wait-for-state request.
        // 将应答游标固定在命令被接受时的修订号，使 Worker 快速暂停仍可被下一次状态等待观察到。
        var latest = result.Session ?? await _flowDebugSessionService.FindAsync(sessionId, cancellationToken) ?? session;
        return new McpDebugCommandAcceptedDto(latest.Id, latest.RunId, command, commandSequence, (FlowDebugSessionStatusDto)latest.Status, latest.StateRevision);
    }

    private static void ThrowStartFailure(FlowDebugSessionStartResult result)
    {
        var code = result.ErrorCode ?? DebugErrorCodes.StartRejected;
        throw new McpProtocolException(ToProtocolCode(result.StatusCode), result.ErrorTitle ?? "The debug session could not be started. 无法启动调试会话。", new { code, details = result.ErrorBody });
    }

    private static void ThrowCommandFailure(FlowDebugSessionCommandResult result)
    {
        var code = result.ErrorCode ?? DebugErrorCodes.CommandRejected;
        throw new McpProtocolException(ToProtocolCode(result.StatusCode), result.ErrorTitle ?? "The debug command was rejected. 调试命令已被拒绝。", new { code });
    }

    private static int ToProtocolCode(int statusCode)
    {
        return McpProtocolErrorCodes.FromHttpStatus(statusCode);
    }
}
