using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;
internal static class McpRunVariableToolHandlers
{
    internal static async Task<object> GetAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var runId = GetGuid(arguments, "runId");
        await AuthorizeRunAsync(context, runId, McpPermissionDto.RunRead, cancellationToken);
        var name = GetOptionalString(arguments, "name");
        var getRequiredServiceResult = context.Services.GetRequiredService<IRunVariableService>();
        var evaluatedValue = GetOptionalInt(arguments, "offset") ?? 0;
        var evaluatedValue1 = GetOptionalInt(arguments, "take") ?? 100;
        global::SereinFlow.Contracts.WorkerVariableRequestDto evaluatedValue2 = new(WorkerProtocol.Version, runId, name is null ? "list" : "get", name, Offset: evaluatedValue, Take: evaluatedValue1);
        var response = await getRequiredServiceResult.AccessAsync(evaluatedValue2, cancellationToken);
        EnsureSuccess(response);
        return (object? )response.Snapshot ?? response.Variable!;
    }

    internal static async Task<object> SetAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var runId = GetGuid(arguments, "runId");
        await AuthorizeRunAsync(context, runId, McpPermissionDto.RunVariableWrite, cancellationToken);
        var name = GetOptionalString(arguments, "name") ?? throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "Variable name is required.");
        var valueType = GetRequiredString(arguments, "valueType");
        if (!arguments.TryGetProperty("value", out var value))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "An explicit JSON value is required.", new { code = VariableErrorCodes.ValueInvalid });
        }

        var key = GetRequiredString(arguments, "idempotencyKey");
        var payload = Serialize(new { runId, name, valueType, value, idempotencyKey = key });
        var principal = context.RequirePrincipal();
        var idempotency = context.Services.GetRequiredService<McpIdempotencyService>();
        const string operation = "run.variable.set";
        var replay = await idempotency.FindAsync(principal.Id, operation, key, payload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var getRequiredServiceResult3 = context.Services.GetRequiredService<IRunVariableService>();
        var cloneResult = value.Clone();
        global::SereinFlow.Contracts.WorkerVariableRequestDto evaluatedValue4 = new(WorkerProtocol.Version, runId, "set", name, cloneResult, valueType);
        var response = await getRequiredServiceResult3.AccessAsync(evaluatedValue4, cancellationToken);
        EnsureSuccess(response);
        var result = response.Variable!;
        await idempotency.SaveAsync(principal.Id, operation, key, result, payload, cancellationToken);
        return result;
    }

    private static async Task AuthorizeRunAsync(McpToolContext context, Guid runId, McpPermissionDto permission, CancellationToken cancellationToken)
    {
        var getRequiredServiceResult5 = context.Services.GetRequiredService<IFlowRunStore>();
        var run = await getRequiredServiceResult5.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var fromHttpStatusResult = McpProtocolErrorCodes.FromHttpStatus(404);
            throw new McpProtocolException(fromHttpStatusResult, "Run not found. 未找到运行实例。", new { code = RunErrorCodes.NotFound });
        }

        var requirePrincipalResult = context.RequirePrincipal();
        context.Security.Require(requirePrincipalResult, permission, run.ProjectId);
    }

    private static void EnsureSuccess(WorkerVariableResponseDto response)
    {
        if (response.StatusCode != 200)
        {
            var fromHttpStatusResult6 = McpProtocolErrorCodes.FromHttpStatus(response.StatusCode);
            throw new McpProtocolException(fromHttpStatusResult6, response.Message ?? "Variable request failed. 变量请求失败。", new { code = response.Code });
        }
    }
}
