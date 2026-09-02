using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Protocol;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;

[Route("api/runs")]
public sealed class RunsController : ApiControllerBase
{
    private readonly IFlowRunStore _runs;
    private readonly IFlowRunOutputStore _outputs;
    private readonly IFlowRunEventStore _events;
    private readonly RunExecutionQueue _queue;
    private readonly RunInterruptionService _interruptions;
    private readonly IFlowDebugSessionStore _debugSessions;
    private readonly RunEventBroadcaster _eventBroadcaster;
    private readonly IWorkerMessageRunClient _messageClient;

    public RunsController(
        IFlowRunStore runs,
        IFlowRunOutputStore outputs,
        IFlowRunEventStore events,
        RunExecutionQueue queue,
        RunInterruptionService interruptions,
        IFlowDebugSessionStore debugSessions,
        RunEventBroadcaster eventBroadcaster,
        IWorkerMessageRunClient messageClient)
    {
        _runs = runs;
        _outputs = outputs;
        _events = events;
        _queue = queue;
        _interruptions = interruptions;
        _debugSessions = debugSessions;
        _eventBroadcaster = eventBroadcaster;
        _messageClient = messageClient;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FlowRunDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> List(
        [FromQuery] string? status,
        [FromQuery] Guid? projectId,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FlowRunStatus>? statuses = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = new List<FlowRunStatus>();
            foreach (var value in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<FlowRunStatus>(value, ignoreCase: true, out var item))
                {
                    return ApiProblem(
                        StatusCodes.Status400BadRequest,
                        "A run status filter is invalid. 运行状态筛选条件无效。",
                        extensions: new Dictionary<string, object?> { ["code"] = "run.status_invalid" });
                }

                parsed.Add(item);
            }

            statuses = parsed.Distinct().ToArray();
        }

        var runs = await _runs.ListAsync(new FlowRunQuery(statuses, projectId, take ?? 100), cancellationToken);
        return Ok(runs.Select(ToRunDto).ToArray());
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(FlowRunOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var queued = await _runs.ListAsync(new FlowRunQuery([FlowRunStatus.Pending], Take: 50), cancellationToken);
        var active = await _runs.ListAsync(new FlowRunQuery([FlowRunStatus.Running], Take: 50), cancellationToken);
        var recent = await _runs.ListAsync(new FlowRunQuery(Take: 100), cancellationToken);
        var snapshot = _queue.GetSnapshot();
        return Ok(new FlowRunOverviewDto(
            snapshot.QueueCapacity,
            snapshot.QueuedCount,
            snapshot.ActiveRunCount,
            snapshot.ActiveListenerRunCount,
            snapshot.MaxConcurrentRuns,
            snapshot.MaxConcurrentListenerRuns,
            snapshot.MaxConcurrentRunsPerProject,
            queued.Select(ToRunDto).ToArray(),
            active.Select(ToRunDto).ToArray(),
            recent.Where(static run => run.IsTerminal).Take(50).Select(ToRunDto).ToArray()));
    }

    [HttpGet("{runId:guid}")]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        return run is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。")
            : Ok(ToRunDto(run));
    }

    [HttpGet("{runId:guid}/debug-session")]
    [ProducesResponseType(typeof(FlowDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetDebugSession([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var session = await _debugSessions.FindByRunIdAsync(runId, cancellationToken);
        return session is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。")
            : Ok(ToFlowDebugSessionDto(session));
    }

    [HttpGet("{runId:guid}/snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSnapshot([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var snapshot = await _runs.GetSnapshotAsync(runId, cancellationToken);
        return snapshot is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Run snapshot not found. 未找到运行快照。")
            : Ok(snapshot);
    }

    [HttpGet("{runId:guid}/outputs")]
    [ProducesResponseType(typeof(FlowRunOutputDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetOutputs([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        if (await _runs.FindAsync(runId, cancellationToken) is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");

        var outputs = await _outputs.ListAsync(runId, cancellationToken);
        return Ok(outputs.Select(ToRunOutputDto).ToArray());
    }

    [HttpPost("{runId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancel([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        if (run.IsTerminal)
            return ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。");

        return _queue.Cancel(runId, RunCancellationSources.Api)
            ? Accepted($"/api/runs/{runId:D}")
            : ApiProblem(StatusCodes.Status409Conflict, "Run is not currently cancellable. 当前运行实例不可取消。");
    }

    [HttpPost("{runId:guid}/messages/{topic}")]
    [ProducesResponseType(typeof(WorkerMessageDeliveryResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult> DeliverMessage(
        [FromRoute] Guid runId,
        [FromRoute] string topic,
        [FromBody] RunMessageIngressRequestDto? request,
        CancellationToken cancellationToken)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        if (run.IsTerminal || run.Status != FlowRunStatus.Running)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "The Worker run is not active. Worker 运行当前不活动。",
                extensions: new Dictionary<string, object?> { ["code"] = "worker.not_active" });
        }
        if (string.IsNullOrWhiteSpace(topic) || topic.Length > 256 || topic.Contains('\r') || topic.Contains('\n'))
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The message topic is invalid. 消息主题无效。",
                extensions: new Dictionary<string, object?> { ["code"] = "message.topic_invalid" });
        }
        if (request is null || request.Payload.ValueKind == JsonValueKind.Undefined)
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "A JSON message payload is required. 必须提供 JSON 消息载荷。",
                extensions: new Dictionary<string, object?> { ["code"] = "message.payload_required" });
        }
        if (!Enum.IsDefined(request.ChannelKind))
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The message channel kind is invalid. 消息通道类型无效。",
                extensions: new Dictionary<string, object?> { ["code"] = "message.channel_invalid" });
        }

        var messageIdResult = ResolveMessageId(
            runId,
            topic.Trim(),
            request.MessageId,
            Request.Headers["Idempotency-Key"].FirstOrDefault());
        if (!messageIdResult.IsSuccess)
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "MessageId or Idempotency-Key must be a valid GUID. MessageId 或 Idempotency-Key 必须是有效 GUID。",
                extensions: new Dictionary<string, object?> { ["code"] = "message.id_invalid" });
        }

        var delivery = new WorkerMessageDeliveryDto(
            WorkerProtocol.Version,
            runId,
            messageIdResult.MessageId,
            topic.Trim(),
            request.ChannelKind,
            WorkerMessageSerializationModeDto.Json,
            request.ContractId,
            JsonSerializer.Serialize(request.Payload, SereinJsonSerialization.CreateWebOptions()),
            DateTimeOffset.UtcNow);
        var response = await _messageClient.DeliverMessageAsync(delivery, cancellationToken);
        return response.Status switch
        {
            WorkerMessageDeliveryStatusDto.Accepted
                => Accepted($"/api/runs/{runId:D}/messages/{Uri.EscapeDataString(topic.Trim())}", response),
            WorkerMessageDeliveryStatusDto.NotFound
                => ApiProblem(
                    StatusCodes.Status404NotFound,
                    response.Message ?? "Worker run not found. 未找到 Worker 运行实例。",
                    extensions: new Dictionary<string, object?> { ["code"] = response.Code ?? "worker.not_found" }),
            WorkerMessageDeliveryStatusDto.NotReady
                => ApiProblem(
                    StatusCodes.Status409Conflict,
                    response.Message ?? "Message endpoint is not ready. 消息入口尚未就绪。",
                    extensions: new Dictionary<string, object?> { ["code"] = response.Code ?? "message.endpoint_not_ready" }),
            WorkerMessageDeliveryStatusDto.TimedOut
                => StatusCode(StatusCodes.Status504GatewayTimeout, response),
            _ when string.Equals(response.Code, "message.endpoint_forbidden", StringComparison.Ordinal)
                => StatusCode(StatusCodes.Status403Forbidden, response),
            _ when string.Equals(response.Code, "message.channel_full", StringComparison.Ordinal)
                => StatusCode(StatusCodes.Status429TooManyRequests, response),
            _ => ApiProblem(
                StatusCodes.Status400BadRequest,
                response.Message ?? "The message was rejected. 消息被拒绝。",
                extensions: new Dictionary<string, object?> { ["code"] = response.Code ?? "message.rejected" })
        };
    }

    [HttpPost("{runId:guid}/interrupt")]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Interrupt([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        if (run.IsTerminal)
            return ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。");
        if (run.Status != FlowRunStatus.Running)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。");
        }
        if (_queue.IsTracked(runId))
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "The run is currently supervised and cannot be interrupted manually. 当前运行实例正在受控执行，不能手动标记为中断。");
        }

        var result = await _interruptions.InterruptAsync(
            runId,
            "operator_reconciliation",
            "run.operator_interrupted",
            "The orphaned run was marked as interrupted by an operator. 孤儿运行实例已由操作人员标记为中断。",
            cancellationToken);
        return result.Disposition switch
        {
            RunInterruptionDisposition.Interrupted => Accepted($"/api/runs/{runId:D}", ToRunDto(result.Run!)),
            RunInterruptionDisposition.NotFound => ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。"),
            RunInterruptionDisposition.AlreadyTerminal => ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。"),
            RunInterruptionDisposition.NotRunning => ApiProblem(
                StatusCodes.Status409Conflict,
                "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。"),
            _ => ApiProblem(StatusCodes.Status409Conflict, "The run could not be marked as interrupted. 无法将运行实例标记为中断。"),
        };
    }

    [HttpGet("{runId:guid}/events")]
    [ProducesResponseType(typeof(FlowRunEventDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetEvents(
        [FromRoute] Guid runId,
        [FromQuery] long? afterSequence,
        CancellationToken cancellationToken)
    {
        if (await _runs.FindAsync(runId, cancellationToken) is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");

        var events = (await _events.GetAfterAsync(runId, afterSequence ?? 0, cancellationToken))
            .Select(item => new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson));
        return Ok(events);
    }

    [HttpGet("{runId:guid}/events/stream")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Produces("text/event-stream")]
    public async Task StreamEvents([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        var lastEventId = HttpContext.Request.Headers.TryGetValue("Last-Event-ID", out var value)
            && long.TryParse(value, out var parsed)
            ? parsed
            : 0;

        // Active runs subscribe before replay so committed events cannot be missed.
        var reader = run.IsTerminal ? null : _eventBroadcaster.Subscribe(runId, HttpContext.RequestAborted);
        foreach (var item in await _events.GetAfterAsync(runId, lastEventId, cancellationToken))
        {
            await WriteSseAsync(
                HttpContext,
                new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson));
            lastEventId = item.Sequence;
        }

        if (reader is null)
            return;

        await foreach (var item in reader.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (item.Sequence <= lastEventId)
                continue;

            await WriteSseAsync(HttpContext, item);
            lastEventId = item.Sequence;
        }
    }

    private static MessageIdResolution ResolveMessageId(
        Guid runId,
        string topic,
        string? requestedMessageId,
        string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(requestedMessageId))
            return Guid.TryParse(requestedMessageId, out var parsed)
                ? new MessageIdResolution(true, parsed)
                : new MessageIdResolution(false, Guid.Empty);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return new MessageIdResolution(true, Guid.NewGuid());
        if (Guid.TryParse(idempotencyKey, out var idempotencyGuid))
            return new MessageIdResolution(true, idempotencyGuid);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{runId:D}:{topic}:{idempotencyKey}"));
        return new MessageIdResolution(true, new Guid(bytes.AsSpan(0, 16)));
    }

    private readonly record struct MessageIdResolution(bool IsSuccess, Guid MessageId);
}
