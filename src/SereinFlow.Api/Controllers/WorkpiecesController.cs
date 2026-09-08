using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[Route("api/runs/{runId:guid}/workpieces")]
public sealed class WorkpiecesController : ApiControllerBase
{
    private readonly IFlowRunStore _runs;
    private readonly IFlowWorkpieceStore _workpieces;
    private readonly SereinFlowApiAuthorizationService _authorization;

    public WorkpiecesController(
        IFlowRunStore runs,
        IFlowWorkpieceStore workpieces,
        SereinFlowApiAuthorizationService authorization)
    {
        _runs = runs;
        _workpieces = workpieces;
        _authorization = authorization;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FlowWorkpieceDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> List(
        [FromRoute] Guid runId,
        CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(
            HttpContext,
            McpPermissionDto.RunRead,
            projectId: null,
            cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
            return preFailure;

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");

        var authorization = await _authorization.AuthorizeAsync(
            HttpContext,
            McpPermissionDto.RunRead,
            run.ProjectId,
            cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
            return failure;

        var items = await _workpieces.ListAsync(runId, cancellationToken);
        return Ok(items.Select(ToDto).ToArray());
    }

    [HttpGet("{workpieceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(
        [FromRoute] Guid runId,
        [FromRoute] string workpieceId,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(
            HttpContext,
            McpPermissionDto.RunRead,
            projectId: null,
            cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
            return preFailure;

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");

        var authorization = await _authorization.AuthorizeAsync(
            HttpContext,
            McpPermissionDto.RunRead,
            run.ProjectId,
            cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
            return failure;

        var item = await _workpieces.FindAsync(runId, workpieceId, cancellationToken);
        if (item is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Workpiece not found. 未找到流程工件。");

        var content = await _workpieces.OpenReadAsync(runId, workpieceId, cancellationToken);
        if (content is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Workpiece content not found. 未找到流程工件内容。");

        return download
            ? File(content, item.ContentType, item.Name, enableRangeProcessing: true)
            : File(content, item.ContentType, enableRangeProcessing: true);
    }

    private static FlowWorkpieceDto ToDto(FlowWorkpieceRecord item)
        => new(
            item.RunId,
            item.Id,
            item.Kind,
            item.Name,
            item.ContentType,
            item.Length,
            item.CreatedAt,
            SereinFlowApiUris.RunWorkpiece(item.RunId, item.Id),
            item.NodeId,
            item.ExecutionId);
}
