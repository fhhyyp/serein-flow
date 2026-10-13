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
        var response = await context.Services.GetRequiredService<IRunVariableService>().AccessAsync(
            new(WorkerProtocol.Version, runId, name is null ? "list" : "get", name,
                Offset: GetOptionalInt(arguments, "offset") ?? 0, Take: GetOptionalInt(arguments, "take") ?? 100), cancellationToken);
        EnsureSuccess(response);
        return (object?)response.Snapshot ?? response.Variable!;
    }

    internal static async Task<object> SetAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var runId = GetGuid(arguments, "runId");
        await AuthorizeRunAsync(context, runId, McpPermissionDto.RunVariableWrite, cancellationToken);
        var name = GetOptionalString(arguments, "name")
            ?? throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "Variable name is required.");
        var valueType = GetRequiredString(arguments, "valueType");
        if (!arguments.TryGetProperty("value", out var value))
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "An explicit JSON value is required.",
                new { code = VariableErrorCodes.ValueInvalid });
        var key = GetRequiredString(arguments, "idempotencyKey");
        var payload = Serialize(new { runId, name, valueType, value, idempotencyKey = key });
        var principal = context.RequirePrincipal();
        var idempotency = context.Services.GetRequiredService<McpIdempotencyService>();
        const string operation = "run.variable.set";
        var replay = await idempotency.FindAsync(principal.Id, operation, key, payload, cancellationToken);
        if (replay is not null) return DeserializeStoredResponse(replay.ResponseJson);
        var response = await context.Services.GetRequiredService<IRunVariableService>().AccessAsync(
            new(WorkerProtocol.Version, runId, "set", name, value.Clone(), valueType), cancellationToken);
        EnsureSuccess(response);
        var result = response.Variable!;
        await idempotency.SaveAsync(principal.Id, operation, key, result, payload, cancellationToken);
        return result;
    }

    private static async Task AuthorizeRunAsync(McpToolContext context, Guid runId,
        McpPermissionDto permission, CancellationToken cancellationToken)
    {
        var run = await context.Services.GetRequiredService<IFlowRunStore>().FindAsync(runId, cancellationToken);
        if (run is null) throw new McpProtocolException(McpProtocolErrorCodes.FromHttpStatus(404),
            "Run not found. 未找到运行实例。", new { code = RunErrorCodes.NotFound });
        context.Security.Require(context.RequirePrincipal(), permission, run.ProjectId);
    }

    private static void EnsureSuccess(WorkerVariableResponseDto response)
    {
        if (response.StatusCode != 200)
            throw new McpProtocolException(McpProtocolErrorCodes.FromHttpStatus(response.StatusCode),
                response.Message ?? "Variable request failed. 变量请求失败。", new { code = response.Code });
    }
}
