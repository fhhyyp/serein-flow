using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Contracts;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;
[Route("api/debug-sessions")]
public sealed class DebugSessionsController : ApiControllerBase
{
    private readonly FlowDebugSessionService _sessions;
    public DebugSessionsController(FlowDebugSessionService sessions)
    {
        _sessions = sessions;
    }

    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(FlowDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessions.FindAsync(sessionId, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue = session is null ? ApiProblem(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。") : resolveOkResult();
        return evaluatedValue;
        global::Microsoft.AspNetCore.Mvc.ObjectResult resolveOkResult()
        {
            var toFlowDebugSessionDtoResult = ToFlowDebugSessionDto(session);
            var okResult = Ok(toFlowDebugSessionDtoResult);
            return okResult;
        }
    }

    [HttpGet("{sessionId:guid}/wait")]
    [ProducesResponseType(typeof(FlowDebugWaitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Wait([FromRoute] Guid sessionId, [FromQuery] long? afterRevision, [FromQuery] int? timeoutSeconds, CancellationToken cancellationToken)
    {
        var revision = afterRevision ?? -1;
        if (afterRevision is not null && revision < 0)
        {
            var dictionary = new Dictionary<string, object?>
            {
                ["code"] = DebugErrorCodes.InvalidStateRevision
            };
            var apiProblemResult = ApiProblem(StatusCodes.Status400BadRequest, "The debug state revision cannot be negative. 调试状态修订号不能为负数。", extensions: dictionary);
            return apiProblemResult;
        }

        var seconds = timeoutSeconds ?? 15;
        if (seconds is < 0 or > 60)
        {
            var dictionary1 = new Dictionary<string, object?>
            {
                ["code"] = DebugErrorCodes.InvalidWaitTimeout
            };
            var apiProblemResult2 = ApiProblem(StatusCodes.Status400BadRequest, "The debug wait timeout must be between 0 and 60 seconds. 调试等待超时必须在 0 到 60 秒之间。", extensions: dictionary1);
            return apiProblemResult2;
        }

        var fromSecondsResult = TimeSpan.FromSeconds(seconds);
        var result = await _sessions.WaitForChangeAsync(sessionId, revision, fromSecondsResult, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue6 = result.Session is null ? ApiProblem(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。") : resolveOkResult3();
        return evaluatedValue6;
        global::Microsoft.AspNetCore.Mvc.ObjectResult resolveOkResult3()
        {
            var toFlowDebugSessionDtoResult4 = ToFlowDebugSessionDto(result.Session);
            var flowDebugWaitResultDto = new FlowDebugWaitResultDto(result.HasChanged, result.TimedOut, toFlowDebugSessionDtoResult4);
            var okResult5 = Ok(flowDebugWaitResultDto);
            return okResult5;
        }
    }

    [HttpPost("{sessionId:guid}/continue")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Continue([FromRoute] Guid sessionId, [FromBody] FlowDebugCommandRequestDto request, CancellationToken cancellationToken)
    {
        var continueAsyncResult = await _sessions.ContinueAsync(sessionId, request.CommandSequence, cancellationToken);
        var toDebugCommandResponseResult = ToDebugCommandResponse(continueAsyncResult);
        return toDebugCommandResponseResult;
    }

    [HttpPost("{sessionId:guid}/step")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Step([FromRoute] Guid sessionId, [FromBody] FlowDebugCommandRequestDto request, CancellationToken cancellationToken)
    {
        var stepAsyncResult = await _sessions.StepAsync(sessionId, request.CommandSequence, cancellationToken);
        var toDebugCommandResponseResult7 = ToDebugCommandResponse(stepAsyncResult);
        return toDebugCommandResponseResult7;
    }

    [HttpPost("{sessionId:guid}/stop")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Stop([FromRoute] Guid sessionId, [FromBody] FlowDebugCommandRequestDto request, CancellationToken cancellationToken)
    {
        var stopAsyncResult = await _sessions.StopAsync(sessionId, request.CommandSequence, cancellationToken);
        var toDebugCommandResponseResult8 = ToDebugCommandResponse(stopAsyncResult);
        return toDebugCommandResponseResult8;
    }
}
