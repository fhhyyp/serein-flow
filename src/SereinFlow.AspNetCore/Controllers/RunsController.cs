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
                    var dictionary = new Dictionary<string, object?>
                    {
                        ["code"] = RunErrorCodes.StatusInvalid
                    };
                    var apiProblemResult = ApiProblem(StatusCodes.Status400BadRequest, "A run status filter is invalid. 运行状态筛选条件无效。", extensions: dictionary);
                    return apiProblemResult;
                }

                parsed.Add(item);
            }

            var distinctResult = parsed.Distinct();
            statuses = distinctResult.ToArray();
        }

        var flowRunQuery = new FlowRunQuery(statuses, projectId, take ?? 100);
        var runs = await _runs.ListAsync(flowRunQuery, cancellationToken);
        var selectResult = runs.Select(ToRunDto);
        var toArrayResult = selectResult.ToArray();
        var okResult = Ok(toArrayResult);
        return okResult;
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(FlowRunOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var flowRunQuery1 = new FlowRunQuery([FlowRunStatus.Pending], Take: 50);
        var queued = await _runs.ListAsync(flowRunQuery1, cancellationToken);
        var flowRunQuery2 = new FlowRunQuery([FlowRunStatus.Running], Take: 50);
        var active = await _runs.ListAsync(flowRunQuery2, cancellationToken);
        var flowRunQuery3 = new FlowRunQuery(Take: RecentRunQueryLimit);
        var recent = await _runs.ListAsync(flowRunQuery3, cancellationToken);
        var snapshot = _queue.GetSnapshot();
        var selectResult4 = queued.Select(ToRunDto);
        var toArrayResult5 = selectResult4.ToArray();
        var selectResult6 = active.Select(ToRunDto);
        var toArrayResult7 = selectResult6.ToArray();
        var whereResult = recent.Where(static run => run.IsTerminal);
        var takeResult = whereResult.Take(RecentCompletedRunLimit);
        var selectResult8 = takeResult.Select(ToRunDto);
        var toArrayResult9 = selectResult8.ToArray();
        var flowRunOverviewDto = new FlowRunOverviewDto(snapshot.QueueCapacity, snapshot.QueuedCount, snapshot.ActiveRunCount, snapshot.ActiveListenerRunCount, snapshot.MaxConcurrentRuns, snapshot.MaxConcurrentListenerRuns, snapshot.MaxConcurrentRunsPerProject, toArrayResult5, toArrayResult7, toArrayResult9);
        var okResult10 = Ok(flowRunOverviewDto);
        return okResult10;
    }

    [HttpGet("{runId:guid}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult11 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult11;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        var evaluatedValue = failure ?? resolveOkResult();
        return evaluatedValue;
        global::Microsoft.AspNetCore.Mvc.ActionResult resolveOkResult()
        {
            var toRunDtoResult = ToRunDto(run);
            var okResult12 = Ok(toRunDtoResult);
            return okResult12;
        }
    }

    [HttpGet("{runId:guid}/debug-session")]
    [ProducesResponseType(typeof(FlowDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetDebugSession([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var session = await _debugSessions.FindByRunIdAsync(runId, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue15 = session is null ? ApiProblem(StatusCodes.Status404NotFound, "Debug session not found. 未找到调试会话。") : resolveOkResult13();
        return evaluatedValue15;
        global::Microsoft.AspNetCore.Mvc.ObjectResult resolveOkResult13()
        {
            var toFlowDebugSessionDtoResult = ToFlowDebugSessionDto(session);
            var okResult14 = Ok(toFlowDebugSessionDtoResult);
            return okResult14;
        }
    }

    [HttpGet("{runId:guid}/snapshot")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSnapshot([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult16 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult16;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var snapshot = await _runs.GetSnapshotAsync(runId, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue17 = snapshot is null ? ApiProblem(StatusCodes.Status404NotFound, "Run snapshot not found. 未找到运行快照。") : Ok(snapshot);
        return evaluatedValue17;
    }

    [HttpGet("{runId:guid}/outputs")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunOutputDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetOutputs([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult18 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult18;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var outputs = await _outputs.ListAsync(runId, cancellationToken);
        var selectResult19 = outputs.Select(ToRunOutputDto);
        var toArrayResult20 = selectResult19.ToArray();
        var okResult21 = Ok(toArrayResult20);
        return okResult21;
    }

    [HttpPost("{runId:guid}/cancel")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancel([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunExecute, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult22 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult22;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunExecute, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        if (run.IsTerminal)
        {
            var apiProblemResult23 = ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。");
            return apiProblemResult23;
        }

        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue24 = _queue.Cancel(runId, RunCancellationSources.Api) ? Accepted($"/api/runs/{runId:D}") : ApiProblem(StatusCodes.Status409Conflict, "Run is not currently cancellable. 当前运行实例不可取消。");
        return evaluatedValue24;
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
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunMessagePublish, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult25 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult25;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunMessagePublish, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var firstOrDefaultResult = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var command = new RunMessageDeliveryCommand(runId, topic, request?.Payload ?? default, request?.MessageId, firstOrDefaultResult, request?.ContractId, request?.ChannelKind ?? WorkerMessageChannelKindDto.Queue);
        var result = await _messageDelivery.DeliverAsync(command, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue29 = result.Disposition switch
        {
            RunMessageDeliveryDisposition.Accepted => resolveAcceptedResult(),
            RunMessageDeliveryDisposition.TimedOut => StatusCode(result.StatusCode, result.Response),
            RunMessageDeliveryDisposition.EndpointForbidden or RunMessageDeliveryDisposition.ChannelFull => StatusCode(result.StatusCode, result.Response),
            _ => resolveApiProblemResult()};
        return evaluatedValue29;
        global::Microsoft.AspNetCore.Mvc.ObjectResult resolveAcceptedResult()
        {
            var evaluatedValue26 = $"/api/runs/{runId:D}/messages/{resolveEscapeDataStringResult()}";
            var acceptedResult = Accepted(evaluatedValue26, result.Response);
            return acceptedResult;
            string resolveEscapeDataStringResult()
            {
                var trimResult = topic.Trim();
                var escapeDataStringResult = Uri.EscapeDataString(trimResult);
                return escapeDataStringResult;
            }
        }

        global::Microsoft.AspNetCore.Mvc.ObjectResult resolveApiProblemResult()
        {
            var dictionary27 = new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode ?? MessageErrorCodes.Rejected
            };
            var apiProblemResult28 = ApiProblem(result.StatusCode, result.Message, extensions: dictionary27);
            return apiProblemResult28;
        }
    }

    [HttpPost("{runId:guid}/interrupt")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Interrupt([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunExecute, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult30 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult30;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunExecute, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        if (run.IsTerminal)
        {
            var apiProblemResult31 = ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。");
            return apiProblemResult31;
        }

        if (run.Status != FlowRunStatus.Running)
        {
            var apiProblemResult32 = ApiProblem(StatusCodes.Status409Conflict, "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。");
            return apiProblemResult32;
        }

        if (_queue.IsTracked(runId))
        {
            var apiProblemResult33 = ApiProblem(StatusCodes.Status409Conflict, "The run is currently supervised and cannot be interrupted manually. 当前运行实例正在受控执行，不能手动标记为中断。");
            return apiProblemResult33;
        }

        var result = await _interruptions.InterruptAsync(runId, "operator_reconciliation", RunErrorCodes.OperatorInterrupted, "The orphaned run was marked as interrupted by an operator. 孤儿运行实例已由操作人员标记为中断。", cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue37 = result.Disposition switch
        {
            RunInterruptionDisposition.Interrupted => resolveAcceptedResult34(),
            RunInterruptionDisposition.NotFound => ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。"),
            RunInterruptionDisposition.AlreadyTerminal => ApiProblem(StatusCodes.Status409Conflict, "The run is already complete. 运行实例已经完成。"),
            RunInterruptionDisposition.NotRunning => ApiProblem(StatusCodes.Status409Conflict, "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。"),
            _ => ApiProblem(StatusCodes.Status409Conflict, "The run could not be marked as interrupted. 无法将运行实例标记为中断。"),
        };
        return evaluatedValue37;
        global::Microsoft.AspNetCore.Mvc.ObjectResult resolveAcceptedResult34()
        {
            var toRunDtoResult35 = ToRunDto(result.Run!);
            var acceptedResult36 = Accepted($"/api/runs/{runId:D}", toRunDtoResult35);
            return acceptedResult36;
        }
    }

    [HttpGet("{runId:guid}/events")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(FlowRunEventDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetEvents([FromRoute] Guid runId, [FromQuery] long? afterSequence, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, projectId: null, cancellationToken);
        var preFailure = ToAuthorizationFailure(preAuthorization);
        if (preFailure is not null)
        {
            return preFailure;
        }

        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            var apiProblemResult38 = ApiProblem(StatusCodes.Status404NotFound, "Run not found. 未找到运行实例。");
            return apiProblemResult38;
        }

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, run.ProjectId, cancellationToken);
        var failure = ToAuthorizationFailure(authorization);
        if (failure is not null)
        {
            return failure;
        }

        var evaluatedValue39 = (await _events.GetAfterAsync(runId, afterSequence ?? 0, cancellationToken));
        var events = evaluatedValue39.Select(item =>
        {
            var flowRunEventDto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
            return flowRunEventDto;
        });
        var okResult40 = Ok(events);
        return okResult40;
    }

    [HttpGet("{runId:guid}/events/stream")]
    [SereinFlowScopedAuthorization]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Produces("text/event-stream")]
    public async Task StreamEvents([FromRoute] Guid runId, CancellationToken cancellationToken)
    {
        var preAuthorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, projectId: null, cancellationToken);
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

        var authorization = await _authorization.AuthorizeAsync(HttpContext, McpPermissionDto.RunRead, run.ProjectId, cancellationToken);
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
        var reader = run.IsTerminal ? null : _eventBroadcaster.Subscribe(runId, HttpContext.RequestAborted);
        foreach (var item in await _events.GetAfterAsync(runId, lastEventId, cancellationToken))
        {
            var flowRunEventDto41 = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
            await WriteSseAsync(HttpContext, flowRunEventDto41);
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
