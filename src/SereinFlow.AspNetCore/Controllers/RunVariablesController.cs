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
        WorkerVariableRequestDto evaluatedValue = new(WorkerProtocol.Version, runId, "list", Offset: offset, Take: take);
        var executeAsyncResult = ExecuteAsync(evaluatedValue, McpPermissionDto.RunRead, cancellationToken);
        return executeAsyncResult;
    }

    [HttpGet("{name}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableDto), StatusCodes.Status200OK)]
    public Task<ActionResult> Get(Guid runId, string name, CancellationToken cancellationToken)
    {
        WorkerVariableRequestDto evaluatedValue1 = new(WorkerProtocol.Version, runId, "get", name);
        var executeAsyncResult2 = ExecuteAsync(evaluatedValue1, McpPermissionDto.RunRead, cancellationToken);
        return executeAsyncResult2;
    }

    [HttpPut("{name}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(RunVariableDto), StatusCodes.Status200OK)]
    public Task<ActionResult> Set(Guid runId, string name, [FromBody] RunVariableSetRequestDto request, CancellationToken cancellationToken)
    {
        WorkerVariableRequestDto evaluatedValue3 = new(WorkerProtocol.Version, runId, "set", name, request.Value, request.ValueType);
        var executeAsyncResult4 = ExecuteAsync(evaluatedValue3, McpPermissionDto.RunVariableWrite, cancellationToken);
        return executeAsyncResult4;
    }

    private async Task<ActionResult> ExecuteAsync(WorkerVariableRequestDto request, McpPermissionDto permission, CancellationToken cancellationToken)
    {
        var authorizeAsyncResult = await authorization.AuthorizeAsync(HttpContext, permission, null, cancellationToken);
        var preFailure = ToAuthorizationFailure(authorizeAsyncResult);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await runs.FindAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            var dictionary = new Dictionary<string, object?>
            {
                ["code"] = RunErrorCodes.NotFound
            };
            var apiProblemResult = ApiProblem(404, "Run not found. 未找到运行实例。", extensions: dictionary);
            return apiProblemResult;
        }

        var authorizeAsyncResult6 = await authorization.AuthorizeAsync(HttpContext, permission, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorizeAsyncResult6);
        if (failure is not null)
        {
            return failure;
        }

        Response.Headers.CacheControl = "no-store";
        var result = await variables.AccessAsync(request, cancellationToken);
        if (result.StatusCode == 200)
        {
            ActionResult evaluatedValue5 = result.Snapshot is { } snapshot ? Ok(snapshot) : Ok(result.Variable);
            return evaluatedValue5;
        }

        var dictionary7 = new Dictionary<string, object?>
        {
            ["code"] = result.Code
        };
        var apiProblemResult8 = ApiProblem(result.StatusCode, result.Message ?? "Variable request failed. 变量请求失败。", extensions: dictionary7);
        return apiProblemResult8;
    }
}
