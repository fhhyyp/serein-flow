using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

/// <summary>
/// MCP handler for publishing JSON values to active run message endpoints.
/// 向活动运行消息端点发布 JSON 值的 MCP 处理器。
/// </summary>
internal static class McpRunMessageToolHandlers
{
    private const string Operation = "run.message.publish";

    internal static async Task<object> PublishAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var runId = ReadRunId(arguments);
        var topic = ReadRequiredText(arguments, "topic").Trim();
        if (!arguments.TryGetProperty("payload", out var payloadValue))
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.InvalidParams,
                "MCP parameter 'payload' is required and must be a JSON value.",
                new { code = MessageErrorCodes.PayloadRequired, path = "payload" });
        }
        var payload = payloadValue.Clone();
        var channelKind = ReadChannelKind(arguments);
        var contractId = GetOptionalString(arguments, "contractId");
        var messageId = GetOptionalString(arguments, "messageId");
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new
        {
            runId,
            topic,
            payload,
            channelKind = channelKind == WorkerMessageChannelKindDto.Queue ? "queue" : "eventBus",
            contractId,
            messageId,
            idempotencyKey,
        });

        var run = await context.Services.GetRequiredService<IFlowRunStore>()
            .FindAsync(runId, cancellationToken);
        if (run is not null)
            context.Security.Require(principal, McpPermissionDto.RunMessagePublish, run.ProjectId);

        var idempotency = context.Services.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(
            principal.Id,
            Operation,
            idempotencyKey,
            requestPayload,
            cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);

        var result = await context.Services.GetRequiredService<IRunMessageDeliveryService>()
            .DeliverAsync(
                new RunMessageDeliveryCommand(
                    runId,
                    topic,
                    payload,
                    messageId,
                    idempotencyKey,
                    contractId,
                    channelKind),
                cancellationToken);
        if (!result.IsAccepted)
            ThrowFailure(result);

        var response = result.Response
            ?? throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The message delivery response was empty.", new { code = MessageErrorCodes.Rejected });
        await idempotency.SaveAsync(
            principal.Id,
            Operation,
            idempotencyKey,
            response,
            requestPayload,
            cancellationToken);
        return response;
    }

    private static Guid ReadRunId(JsonElement arguments)
    {
        var value = ReadRequiredText(arguments, "runId");
        if (!Guid.TryParse(value, out var runId) || runId == Guid.Empty)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.InvalidParams,
                "MCP parameter 'runId' must be a non-empty GUID.",
                new { code = McpErrorCodes.InvalidArguments, path = "runId" });
        }

        return runId;
    }

    private static string ReadRequiredText(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.InvalidParams,
                $"MCP parameter '{name}' is required and must be a string.",
                new { code = McpErrorCodes.InvalidArguments, path = name });
        }

        return value.GetString() ?? string.Empty;
    }

    private static WorkerMessageChannelKindDto ReadChannelKind(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("channelKind", out var value))
            return WorkerMessageChannelKindDto.Queue;
        if (value.ValueKind != JsonValueKind.String)
            throw InvalidChannelKind();

        return value.GetString()?.Trim() switch
        {
            { } text when string.Equals(text, "queue", StringComparison.OrdinalIgnoreCase)
                => WorkerMessageChannelKindDto.Queue,
            { } text when string.Equals(text, "eventBus", StringComparison.OrdinalIgnoreCase)
                => WorkerMessageChannelKindDto.EventBus,
            _ => throw InvalidChannelKind(),
        };
    }

    private static McpProtocolException InvalidChannelKind()
        => new(
            McpProtocolErrorCodes.InvalidParams,
            "The message channel kind must be 'queue' or 'eventBus'. 消息通道类型必须是 queue 或 eventBus。",
            new { code = MessageErrorCodes.ChannelInvalid, path = "channelKind" });

    private static void ThrowFailure(RunMessageDeliveryResult result)
        => throw new McpProtocolException(
            ToProtocolCode(result.StatusCode),
            result.Message,
            new { code = result.ErrorCode ?? MessageErrorCodes.Rejected });

    private static int ToProtocolCode(int statusCode)
        => McpProtocolErrorCodes.FromHttpStatus(statusCode);
}
