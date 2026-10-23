using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[Route("api/runs/{runId:guid}/variables")]
public sealed class RunVariablesController(IFlowRunStore runs, IRunVariableService variables, SereinFlowApiAuthorizationService authorization) : ApiControllerBase
{
    [HttpGet]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableSnapshotDto), StatusCodes.Status200OK)]
    public Task<ActionResult> List(Guid runId, [FromQuery] int offset = 0, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        WorkerVariableRequestDto request = new(WorkerProtocol.Version, runId, "list", Offset: offset, Take: take);
        return ExecuteAsync(request, SereinFlowPermission.RunRead, cancellationToken);
    }

    [HttpGet("{name}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableDto), StatusCodes.Status200OK)]
    public Task<ActionResult> Get(Guid runId, string name, CancellationToken cancellationToken)
    {
        WorkerVariableRequestDto request = new(WorkerProtocol.Version, runId, "get", name);
        return ExecuteAsync(request, SereinFlowPermission.RunRead, cancellationToken);
    }

    [HttpPut("{name}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableDto), StatusCodes.Status200OK)]
    public Task<ActionResult> Set(Guid runId, string name, [FromBody] RunVariableSetRequestDto request, CancellationToken cancellationToken)
    {
        WorkerVariableRequestDto workerVariableRequestDto = new(WorkerProtocol.Version, runId, "set", name, request.Value, request.ValueType);
        return ExecuteAsync(workerVariableRequestDto, SereinFlowPermission.RunVariableWrite, cancellationToken);
    }

    private async Task<ActionResult> ExecuteAsync(WorkerVariableRequestDto request, SereinFlowPermission permission, CancellationToken cancellationToken)
    {
        var authorize = await authorization.AuthorizeAsync(HttpContext, permission, null, cancellationToken);
        var preFailure = ToAuthorizationFailure(authorize);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await runs.FindAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            return ApiProblemWithCode(404, "Run not found. 未找到运行实例。", code: RunErrorCodes.NotFound);
        }

        var mappedAuthorize = await authorization.AuthorizeAsync(HttpContext, permission, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(mappedAuthorize);
        if (failure is not null)
        {
            return failure;
        }

        Response.Headers.CacheControl = "no-store";
        var result = await variables.AccessAsync(request, cancellationToken);
        if (result.StatusCode == 200)
        {
            return result.Snapshot is { } snapshot ? Ok(snapshot) : Ok(result.Variable);
        }
        return ApiProblemWithCode(result.StatusCode, result.Message ?? "Variable request failed. 变量请求失败。", code: result.Code);
    }
}
