using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Tools.McpToolSupport;

namespace SereinFlow.Mcp.Tools;
/// <summary>
/// MCP handler for publishing JSON values to active run message endpoints.
/// 向活动运行消息端点发布 JSON 值的 MCP 处理器。
/// </summary>
internal static class McpRunMessageToolHandlers
{
    private const string Operation = "run.message.publish";
    [SereinFlowMcpTool("sereinflow_publish_run_message", McpPermissionDto.RunMessagePublish, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal static async Task<object> PublishAsync(SereinFlowMcpToolContext context, McpPublishRunMessageRequest arguments, CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var runId = arguments.RunId;
        var topic = arguments.Topic;
        var payload = arguments.Payload;
        var channelKind = arguments.ChannelKind;
        var contractId = arguments.ContractId;
        var messageId = arguments.MessageId;
        var idempotencyKey = arguments.IdempotencyKey;
        var requestPayload = Serialize(new { runId, topic, payload, channelKind = channelKind == WorkerMessageChannelKindDto.Queue ? "queue" : "eventBus", contractId, messageId, idempotencyKey, });
        var getRequiredServiceResult = context.Services.GetRequiredService<IFlowRunStore>();
        var run = await getRequiredServiceResult.FindAsync(runId, cancellationToken);
        if (run is not null)
        {
            context.Security.Require(principal, McpPermissionDto.RunMessagePublish, run.ProjectId);
        }

        var idempotency = context.Services.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, Operation, idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var getRequiredServiceResult1 = context.Services.GetRequiredService<IRunMessageDeliveryService>();
        var runMessageDeliveryCommand = new RunMessageDeliveryCommand(runId, topic, payload, messageId, idempotencyKey, contractId, channelKind);
        var result = await getRequiredServiceResult1.DeliverAsync(runMessageDeliveryCommand, cancellationToken);
        if (!result.IsAccepted)
        {
            ThrowFailure(result);
        }

        var response = result.Response ?? throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The message delivery response was empty.", new { code = MessageErrorCodes.Rejected });
        await idempotency.SaveAsync(principal.Id, Operation, idempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static void ThrowFailure(RunMessageDeliveryResult result)
    {
        var toProtocolCodeResult = ToProtocolCode(result.StatusCode);
        throw new McpProtocolException(toProtocolCodeResult, result.Message, new { code = result.ErrorCode ?? MessageErrorCodes.Rejected });
    }

    private static int ToProtocolCode(int statusCode)
    {
        var fromHttpStatusResult = McpProtocolErrorCodes.FromHttpStatus(statusCode);
        return fromHttpStatusResult;
    }
}
