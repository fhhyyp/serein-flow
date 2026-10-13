using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[Route("api/runs/{runId:guid}/variables")]
public sealed class RunVariablesController(
    IFlowRunStore runs,
    IRunVariableService variables,
    SereinFlowApiAuthorizationService authorization) : ApiControllerBase
{
    [HttpGet]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableSnapshotDto), StatusCodes.Status200OK)]
    public Task<ActionResult> List(Guid runId, [FromQuery] int offset = 0, [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(new(WorkerProtocol.Version, runId, "list", Offset: offset, Take: take),
            McpPermissionDto.RunRead, cancellationToken);

    [HttpGet("{name}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableDto), StatusCodes.Status200OK)]
    public Task<ActionResult> Get(Guid runId, string name, CancellationToken cancellationToken)
        => ExecuteAsync(new(WorkerProtocol.Version, runId, "get", name), McpPermissionDto.RunRead, cancellationToken);

    [HttpPut("{name}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableDto), StatusCodes.Status200OK)]
    public Task<ActionResult> Set(Guid runId, string name, [FromBody] RunVariableSetRequestDto request,
        CancellationToken cancellationToken)
        => ExecuteAsync(new(WorkerProtocol.Version, runId, "set", name, request.Value, request.ValueType),
            McpPermissionDto.RunVariableWrite, cancellationToken);

    private async Task<ActionResult> ExecuteAsync(WorkerVariableRequestDto request,
        McpPermissionDto permission, CancellationToken cancellationToken)
    {
        var preFailure = ToAuthorizationFailure(await authorization.AuthorizeAsync(
            HttpContext, permission, null, cancellationToken));
        if (preFailure is not null) return preFailure;
        var run = await runs.FindAsync(request.RunId, cancellationToken);
        if (run is null) return ApiProblem(404, "Run not found. 未找到运行实例。",
            extensions: new Dictionary<string, object?> { ["code"] = RunErrorCodes.NotFound });
        var failure = ToAuthorizationFailure(await authorization.AuthorizeAsync(
            HttpContext, permission, run.ProjectId, cancellationToken));
        if (failure is not null) return failure;
        Response.Headers.CacheControl = "no-store";
        var result = await variables.AccessAsync(request, cancellationToken);
        if (result.StatusCode == 200) return result.Snapshot is { } snapshot ? Ok(snapshot) : Ok(result.Variable);
        return ApiProblem(result.StatusCode, result.Message ?? "Variable request failed. 变量请求失败。",
            extensions: new Dictionary<string, object?> { ["code"] = result.Code });
    }
}
