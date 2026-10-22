using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpJson;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpRunMessageTools(
    IFlowRunStore _flowRunStore,
    IRunMessageDeliveryService _runMessageDeliveryService,
    McpIdempotencyService _mcpIdempotencyService,
    McpSecurityService _mcpSecurityService)
{
    private const string Operation = "run.message.publish";
    [SereinFlowMcpTool("sereinflow_publish_run_message", McpPermissionDto.RunMessagePublish, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> PublishAsync(
        SereinFlowMcpCaller context,
        McpPublishRunMessageRequest arguments,
        CancellationToken cancellationToken)
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
        var run = await _flowRunStore.FindAsync(runId, cancellationToken);
        if (run is not null)
        {
            _mcpSecurityService.Require(principal, McpPermissionDto.RunMessagePublish, run.ProjectId);
        }

        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, Operation, idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var runMessageDeliveryCommand = new RunMessageDeliveryCommand(runId, topic, payload, messageId, idempotencyKey, contractId, channelKind);
        var result = await _runMessageDeliveryService.DeliverAsync(runMessageDeliveryCommand, cancellationToken);
        if (!result.IsAccepted)
        {
            ThrowFailure(result);
        }

        var response = result.Response ?? throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The message delivery response was empty. 消息投递响应为空。", new { code = MessageErrorCodes.Rejected });
        await _mcpIdempotencyService.SaveAsync(principal.Id, Operation, idempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static void ThrowFailure(RunMessageDeliveryResult result)
    {
        throw new McpProtocolException(ToProtocolCode(result.StatusCode), result.Message, new { code = result.ErrorCode ?? MessageErrorCodes.Rejected });
    }

    private static int ToProtocolCode(int statusCode)
    {
        return McpProtocolErrorCodes.FromHttpStatus(statusCode);
    }
}
