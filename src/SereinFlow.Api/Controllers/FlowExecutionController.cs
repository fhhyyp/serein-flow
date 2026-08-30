using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[Route("api/projects/{projectId:guid}/flows/{flowId:guid}")]
public sealed class FlowExecutionController : ApiControllerBase
{
    private readonly RunSubmissionService _submissions;
    private readonly FlowDebugSessionService _debugSessions;

    public FlowExecutionController(RunSubmissionService submissions, FlowDebugSessionService debugSessions)
    {
        _submissions = submissions;
        _debugSessions = debugSessions;
    }

    [HttpPost("runs")]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> StartRun(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromBody] RunFlowRequestDto request,
        CancellationToken cancellationToken)
        => ToRunSubmissionResponse(await _submissions.SubmitAsync(projectId, flowId, request, cancellationToken));

    [HttpPost("debug-sessions")]
    [ProducesResponseType(typeof(FlowDebugSessionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> StartDebugSession(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromBody] StartFlowDebugSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _debugSessions.CreateAsync(projectId, flowId, request, cancellationToken);
        if (result.IsAccepted)
        {
            return Accepted(
                $"/api/debug-sessions/{result.Session!.Id:D}",
                ApiEndpointHelpers.ToFlowDebugSessionDto(result.Session));
        }

        return result.ErrorBody is not null
            ? new JsonResult(result.ErrorBody) { StatusCode = result.StatusCode }
            : ApiProblem(result.StatusCode, result.ErrorTitle);
    }
}
