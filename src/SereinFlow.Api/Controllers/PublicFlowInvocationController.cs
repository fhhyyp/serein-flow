using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;

[Route("api/public")]
public sealed class PublicFlowInvocationController : ApiControllerBase
{
    private readonly IFlowInterfaceRepository _interfaces;
    private readonly IFlowVersionRepository _versions;
    private readonly RunSubmissionService _submissions;
    private readonly IFlowRunStore _runs;
    private readonly IFlowRunOutputStore _outputs;
    private readonly RunExecutionQueue _queue;

    public PublicFlowInvocationController(
        IFlowInterfaceRepository interfaces,
        IFlowVersionRepository versions,
        RunSubmissionService submissions,
        IFlowRunStore runs,
        IFlowRunOutputStore outputs,
        RunExecutionQueue queue)
    {
        _interfaces = interfaces;
        _versions = versions;
        _submissions = submissions;
        _runs = runs;
        _outputs = outputs;
        _queue = queue;
    }

    [HttpPost("flows/{interfaceId:guid}/invoke")]
    [ProducesResponseType(typeof(PublicFlowInvocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PublicFlowInvocationResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Invoke(
        [FromRoute] Guid interfaceId,
        [FromBody] PublicFlowInvocationRequestDto request,
        CancellationToken cancellationToken)
    {
        var flowInterface = await _interfaces.FindAsync(interfaceId, cancellationToken);
        if (flowInterface is null || !flowInterface.IsEnabled)
            return ApiProblem(StatusCodes.Status404NotFound, "The flow interface is unavailable. 流程接口不可用。");

        var productionDefinition = await _versions.FindProductionDefinitionAsync(
            flowInterface.ProjectId,
            flowInterface.FlowId,
            cancellationToken);
        if (productionDefinition is null)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "The flow interface requires a published production version. 流程接口需要已发布的生产版本。",
                extensions: new Dictionary<string, object?> { ["code"] = FlowErrorCodes.ProductionVersionRequired });
        }

        var submission = await _submissions.SubmitDefinitionAsync(
            flowInterface.ProjectId,
            productionDefinition,
            new RunFlowRequestDto(null, request.ProjectInputs, request.TimeoutSeconds, request.MaxSteps, request.MaxNodeVisits),
            cancellationToken);
        if (!submission.IsAccepted)
            return ToRunSubmissionResponse(submission);

        var submittedRun = submission.Run!;
        if (flowInterface.InvocationMode == FlowInvocationModeDto.Asynchronous)
        {
            return Accepted(
                $"/api/public/tasks/{submittedRun.Id:D}",
                new PublicFlowInvocationResponseDto(submittedRun.Id, FlowRunStatusDto.Pending, false, []));
        }

        var maximumWait = TimeSpan.FromSeconds(_queue.Options.SynchronousInvocationTimeoutSeconds);
        var completedRun = await WaitForTerminalRunAsync(_runs, submittedRun.Id, maximumWait, cancellationToken);
        if (completedRun is null || !completedRun.IsTerminal)
        {
            return Accepted(
                $"/api/public/tasks/{submittedRun.Id:D}",
                new PublicFlowInvocationResponseDto(submittedRun.Id, FlowRunStatusDto.Pending, false, []));
        }

        return Ok(new PublicFlowInvocationResponseDto(
            completedRun.Id,
            (FlowRunStatusDto)completedRun.Status,
            true,
            await ReadNodeDataAsync(_outputs, completedRun.Id, cancellationToken)));
    }

    [HttpGet("tasks/{taskId:guid}")]
    [ProducesResponseType(typeof(PublicFlowInvocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTask([FromRoute] Guid taskId, CancellationToken cancellationToken)
    {
        var run = await _runs.FindAsync(taskId, cancellationToken);
        if (run is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Task not found. 任务不存在。");

        return Ok(new PublicFlowInvocationResponseDto(
            run.Id,
            (FlowRunStatusDto)run.Status,
            run.IsTerminal,
            run.IsTerminal ? await ReadNodeDataAsync(_outputs, run.Id, cancellationToken) : []));
    }
}
