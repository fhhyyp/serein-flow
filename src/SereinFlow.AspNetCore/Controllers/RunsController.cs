using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;

[Route("api/runs")]
public sealed class RunsController : ApiControllerBase
{
    private const int RecentRunQueryLimit = 100;
    private const int RecentCompletedRunLimit = 50;
    private readonly IFlowRunStore _runs;
    private readonly IFlowRunOutputStore _outputs;
    private readonly IFlowRunEventStore _events;
    private readonly RunExecutionQueue _queue;
    private readonly RunInterruptionService _interruptions;
    private readonly IFlowDebugSessionStore _debugSessions;
    private readonly RunEventBroadcaster _eventBroadcaster;
    private readonly IRunMessageDeliveryService _messageDelivery;
    private readonly SereinFlowApiAuthorizationService _authorization;
    public RunsController(IFlowRunStore runs, IFlowRunOutputStore outputs, IFlowRunEventStore events, RunExecutionQueue queue, RunInterruptionService interruptions, IFlowDebugSessionStore debugSessions, RunEventBroadcaster eventBroadcaster, IRunMessageDeliveryService messageDelivery, SereinFlowApiAuthorizationService authorization)
    {
        _runs = runs;
        _outputs = outputs;
        _events = events;
        _queue = queue;
        _interruptions = interruptions;
        _debugSessions = debugSessions;
        _eventBroadcaster = eventBroadcaster;
        _messageDelivery = messageDelivery;
        _authorization = authorization;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FlowRunDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> List([FromQuery] string? status, [FromQuery] Guid? projectId, [FromQuery] int? take, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FlowRunStatus>? statuses = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = new List<FlowRunStatus>();
            foreach (var value in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<FlowRunStatus>(value, ignoreCase: true, out var item))
                {
                    return ApiProblemWithCode(StatusCodes.Status400BadRequest, "A run status filter is invalid. 运行状态筛选条件无效。", code: RunErrorCodes.StatusInvalid);
                }

                parsed.Add(item);
            }
            statuses = parsed.Distinct().ToArray();
        }

        var flowRunQuery = new FlowRunQuery(statuses, projectId, take ?? 100);
        var mappedRuns = (await _runs.ListAsync(flowRunQuery, cancellationToken)).Select(ToRunDto).ToArray();
        return Ok(mappedRuns);
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(FlowRunOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var query = new FlowRunQuery([FlowRunStatus.Pending], Take: 50);
        var queued = await _runs.ListAsync(query, cancellationToken);
        var flowRunQuery = new FlowRunQuery([FlowRunStatus.Running], Take: 50);
        var active = await _runs.ListAsync(flowRunQuery, cancellationToken);
        var mappedQuery = new FlowRunQuery(Take: RecentRunQueryLimit);
        var recent = await _runs.ListAsync(mappedQuery, cancellationToken);
        var snapshot = _queue.GetSnapshot();
        var queuedRuns = queued.Select(ToRunDto).ToArray();
        var activeRuns = active.Select(ToRunDto).ToArray();
        var recentRuns = recent
            .Where(static run => run.IsTerminal)
            .Take(RecentCompletedRunLimit)
            .Select(ToRunDto)
            .ToArray();
        var flowRunOverviewDto = new FlowRunOverviewDto(snapshot.QueueCapacity, snapshot.QueuedCount, snapshot.ActiveRunCount, snapshot.ActiveListenerRunCount, snapshot.MaxConcurrentRuns, snapshot.MaxConcurrentListenerRuns, snapshot.MaxConcurrentRunsPerProject, queuedRuns, activeRuns, recentRuns);
        return Ok(flowRunOverviewDto);
    }

    [HttpGet("{runId:guid}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        return failure ?? CreateRunResponse();
        ActionResult CreateRunResponse()
        {
            var runDto = ToRunDto(run);
            return Ok(runDto);
        }
    }

    [HttpGet("{runId:guid}/debug-session")]
    [ProducesResponseType(typeof(FlowDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetDebugSession([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var session = await _debugSessions.FindByRunIdAsync(runId, cancellationToken);
        return session is null ? ApiProblem(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。") : CreateDebugSessionResponse();
        ObjectResult CreateDebugSessionResponse()
        {
            var flowDebugSessionDto = ToFlowDebugSessionDto(session);
            return Ok(flowDebugSessionDto);
        }
    }

    [HttpGet("{runId:guid}/snapshot")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSnapshot([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var snapshot = await _runs.GetSnapshotAsync(runId, cancellationToken);
        return snapshot is null ? ApiProblem(StatusCodes.Status404NotFound, "Run snapshot not found. 未找到运行快照。") : Ok(snapshot);
    }

    [HttpGet("{runId:guid}/outputs")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunOutputDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetOutputs([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }
        var mappedOutputs = (await _outputs.ListAsync(runId, cancellationToken)).Select(ToRunOutputDto).ToArray();
        return Ok(mappedOutputs);
    }

    [HttpPost("{runId:guid}/cancel")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancel([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunExecute, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunExecute, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        if (run.IsTerminal)
        {
            return ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。");
        }
        return _queue.Cancel(runId, RunCancellationSources.Api) ? Accepted($"/api/runs/{runId:D}") : ApiProblem(StatusCodes.Status409Conflict, "Run is not currently cancellable. 当前运行实例不可取消。");
    }

    [HttpPost("{runId:guid}/messages/{topic}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(WorkerMessageDeliveryResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult> DeliverMessage([FromRoute] Guid runId, [FromRoute] string topic, [FromBody] RunMessageIngressRequestDto? request, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunMessagePublish, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunMessagePublish, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var command = new RunMessageDeliveryCommand(runId, topic, request?.Payload ?? default, request?.MessageId, idempotencyKey, request?.ContractId, request?.ChannelKind ?? WorkerMessageChannelKindDto.Queue);
        var result = await _messageDelivery.DeliverAsync(command, cancellationToken);
        return result.Disposition switch
        {
            RunMessageDeliveryDisposition.Accepted => ReadAccepted(),
            RunMessageDeliveryDisposition.TimedOut => StatusCode(result.StatusCode, result.Response),
            RunMessageDeliveryDisposition.EndpointForbidden or RunMessageDeliveryDisposition.ChannelFull => StatusCode(result.StatusCode, result.Response),
            _ => CreateMessageFailureResponse()
        };
        ObjectResult ReadAccepted()
        {
            var messageLocation = $"/api/runs/{runId:D}/messages/{Uri.EscapeDataString(topic.Trim())}";
            return Accepted(messageLocation, result.Response);
        }

        ObjectResult CreateMessageFailureResponse()
        {
            return ApiProblemWithCode(result.StatusCode, result.Message, code: result.ErrorCode ?? MessageErrorCodes.Rejected);
        }
    }

    [HttpPost("{runId:guid}/interrupt")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Interrupt([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunExecute, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunExecute, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        if (run.IsTerminal)
        {
            return ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。");
        }

        if (run.Status != FlowRunStatus.Running)
        {
            return ApiProblem(StatusCodes.Status409Conflict, "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。");
        }

        if (_queue.IsTracked(runId))
        {
            return ApiProblem(StatusCodes.Status409Conflict, "The run is currently supervised and cannot be interrupted manually. 当前运行实例正在受控执行，不能手动标记为中断。");
        }

        var result = await _interruptions.InterruptAsync(runId, "operator_reconciliation", RunErrorCodes.OperatorInterrupted, "The orphaned run was marked as interrupted by an operator. 孤儿运行实例已由操作人员标记为中断。", cancellationToken);
        return result.Disposition switch
        {
            RunInterruptionDisposition.Interrupted => ReadInterrupted(),
            RunInterruptionDisposition.NotFound => ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。"),
            RunInterruptionDisposition.AlreadyTerminal => ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。"),
            RunInterruptionDisposition.NotRunning => ApiProblem(StatusCodes.Status409Conflict, "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。"),
            _ => ApiProblem(StatusCodes.Status409Conflict, "The run could not be marked as interrupted. 无法将运行实例标记为中断。"),
        };
        ObjectResult ReadInterrupted()
        {
            var runDto = ToRunDto(result.Run!);
            return Accepted($"/api/runs/{runId:D}", runDto);
        }
    }

    [HttpGet("{runId:guid}/events")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunEventDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetEvents([FromRoute] Guid runId, [FromQuery] long? afterSequence, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var events = (await _events.GetAfterAsync(runId, afterSequence ?? 0, cancellationToken)).Select(item =>
        {
            return new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        });
        return Ok(events);
    }

    [HttpGet("{runId:guid}/events/stream")]
    [SereinFlowScopedAuthorization]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Produces("text/event-stream")]
    public async Task StreamEvents([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, projectId: null, cancellationToken);
        if (!preAuthorization.IsAllowed)
        {
            HttpContext.Response.StatusCode = preAuthorization.StatusCode;
            await Response.WriteAsJsonAsync(new { status = preAuthorization.StatusCode, title = preAuthorization.Message, code = preAuthorization.Code, }, cancellationToken);
            return;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, SereinFlowPermission.RunRead, run.ProjectId, cancellationToken);
        if (!authorization.IsAllowed)
        {
            HttpContext.Response.StatusCode = authorization.StatusCode;
            await Response.WriteAsJsonAsync(new { status = authorization.StatusCode, title = authorization.Message, code = authorization.Code, }, cancellationToken);
            return;
        }

        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        var lastEventId = HttpContext.Request.Headers.TryGetValue("Last-Event-ID", out var value) && long.TryParse(value, out var parsed) ? parsed : 0;
        // Active runs subscribe before replay so committed events cannot be missed.
        // 活动运行先订阅再回放，避免遗漏已提交的事件。
        var reader = run.IsTerminal ? null : _eventBroadcaster.Subscribe(runId, HttpContext.RequestAborted);
        foreach (var item in await _events.GetAfterAsync(runId, lastEventId, cancellationToken))
        {
            var flowRunEventDto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
            await WriteSseAsync(HttpContext, flowRunEventDto);
            lastEventId = item.Sequence;
        }

        if (reader is null)
        {
            return;
        }

        await foreach (var item in reader.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (item.Sequence <= lastEventId)
            {
                continue;
            }

            await WriteSseAsync(HttpContext, item);
            lastEventId = item.Sequence;
        }
    }
}
