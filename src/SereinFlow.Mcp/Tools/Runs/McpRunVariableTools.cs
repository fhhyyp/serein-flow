using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpJson;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpRunVariableTools(
    IFlowRunStore _flowRunStore,
    IRunVariableService _runVariableService,
    McpIdempotencyService _mcpIdempotencyService,
    SereinFlowAccess _mcpSecurityService)
{
    [SereinFlowMcpTool("sereinflow_get_run_variables", SereinFlowPermission.RunRead)]
    internal async Task<object> GetAsync(
        SereinFlowMcpCaller context,
        McpGetRunVariablesRequest arguments,
        CancellationToken cancellationToken)
    {
        var runId = arguments.RunId;
        await AuthorizeRunAsync(context, runId, SereinFlowPermission.RunRead, cancellationToken);
        var name = arguments.Name;
        var offset = arguments.Offset;
        var take = arguments.Take;
        var workerRequest = new WorkerVariableRequestDto(WorkerProtocol.Version, runId, name is null ? "list" : "get", name, Offset: offset, Take: take);
        var response = await _runVariableService.AccessAsync(workerRequest, cancellationToken);
        EnsureSuccess(response);
        return (object?)response.Snapshot ?? response.Variable!;
    }

    [SereinFlowMcpTool("sereinflow_set_run_variable", SereinFlowPermission.RunVariableWrite, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> SetAsync(
        SereinFlowMcpCaller context,
        McpSetRunVariableRequest arguments,
        CancellationToken cancellationToken)
    {
        var runId = arguments.RunId;
        await AuthorizeRunAsync(context, runId, SereinFlowPermission.RunVariableWrite, cancellationToken);
        var name = arguments.Name;
        var valueType = arguments.ValueType;
        var value = arguments.Value;
        var key = arguments.IdempotencyKey;
        var payload = Serialize(new { runId, name, valueType, value, idempotencyKey = key });
        var principal = context.RequirePrincipal();
        const string operation = "run.variable.set";
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, operation, key, payload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var workerRequest = new WorkerVariableRequestDto(WorkerProtocol.Version, runId, "set", name, value, valueType);
        var response = await _runVariableService.AccessAsync(workerRequest, cancellationToken);
        EnsureSuccess(response);
        var result = response.Variable!;
        await _mcpIdempotencyService.SaveAsync(principal.Id, operation, key, result, payload, cancellationToken);
        return result;
    }

    private async Task AuthorizeRunAsync(
        SereinFlowMcpCaller context,
        Guid runId,
        SereinFlowPermission permission,
        CancellationToken cancellationToken)
    {
        var run = await _flowRunStore.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.FromHttpStatus(404), "Run not found. 未找到运行实例。", new { code = RunErrorCodes.NotFound });
        }

        var principal = context.RequirePrincipal();
        await _mcpSecurityService.RequireAsync(principal, permission, run.ProjectId, cancellationToken: cancellationToken);
    }

    private static void EnsureSuccess(WorkerVariableResponseDto response)
    {
        if (response.StatusCode != 200)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.FromHttpStatus(response.StatusCode), response.Message ?? "Variable request failed. 变量请求失败。", new { code = response.Code });
        }
    }
}
