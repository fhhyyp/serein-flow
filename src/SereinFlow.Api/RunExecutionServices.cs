using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Api;

public sealed record RunWorkItem(
    Guid RunId,
    Guid ProjectId,
    FlowDefinitionDto Definition,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs,
    DateTimeOffset Deadline,
    int MaxSteps,
    int MaxNodeVisits = 1_000);

public sealed class RunExecutionQueue
{
    private readonly Channel<RunWorkItem> _queue = Channel.CreateUnbounded<RunWorkItem>();
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private readonly HashSet<Guid> _pendingCancellations = [];
    private readonly object _gate = new();

    public ValueTask EnqueueAsync(RunWorkItem item, CancellationToken cancellationToken = default)
        => _queue.Writer.WriteAsync(item, cancellationToken);

    public IAsyncEnumerable<RunWorkItem> ReadAllAsync(CancellationToken cancellationToken)
        => _queue.Reader.ReadAllAsync(cancellationToken);

    public CancellationToken Register(Guid runId, CancellationToken stoppingToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        lock (_gate)
        {
            _cancellations[runId] = source;
            if (_pendingCancellations.Remove(runId))
                source.Cancel();
        }
        return source.Token;
    }

    public bool Cancel(Guid runId)
    {
        lock (_gate)
        {
            if (!_cancellations.TryGetValue(runId, out var source))
            {
                // A run may still be waiting in the channel. Preserve the
                // cancellation request until the hosted service registers it.
                // 运行可能仍在队列中等待；先记录取消请求，后台服务注册时立即应用。
                _pendingCancellations.Add(runId);
                return true;
            }
            source.Cancel();
            return true;
        }
    }

    public void Remove(Guid runId)
    {
        lock (_gate)
        {
            if (_cancellations.Remove(runId, out var source))
                source.Dispose();
        }
    }
}

public sealed class RunEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<FlowRunEventDto>> _channels = new();
    private readonly ConcurrentDictionary<Guid, byte> _completedRuns = new();

    public ChannelReader<FlowRunEventDto> Subscribe(Guid runId)
    {
        var channel = _channels.GetOrAdd(runId, static _ => Channel.CreateUnbounded<FlowRunEventDto>());
        if (_completedRuns.ContainsKey(runId))
            channel.Writer.TryComplete();
        return channel.Reader;
    }

    public async ValueTask PublishAsync(FlowRunEventDto item, CancellationToken cancellationToken = default)
    {
        if (_channels.TryGetValue(item.RunId, out var channel))
            await channel.Writer.WriteAsync(item, cancellationToken);

        if (item.Type is "run.completed" or "run.failed" or "run.cancelled" or "run.timed_out")
        {
            _completedRuns.TryAdd(item.RunId, 0);
            if (_channels.TryRemove(item.RunId, out var completed))
                completed.Writer.TryComplete();
        }
    }
}

public sealed class RunEventsHub : Hub
{
    public Task Subscribe(Guid runId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"run:{runId:D}");

    public Task Unsubscribe(Guid runId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run:{runId:D}");
}

public sealed class RunExecutionHostedService : BackgroundService
{
    private static readonly Action<ILogger, Guid, Exception?> WorkerRunFailedLog =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4101, "WorkerRunFailed"),
            "Worker run {RunId} failed before completion. Worker 运行在完成前失败。");

    private readonly RunExecutionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerRunClient _workerClient;
    private readonly RunEventBroadcaster _broadcaster;
    private readonly IHubContext<RunEventsHub> _hub;
    private readonly ILogger<RunExecutionHostedService> _logger;
    private readonly string _scriptRoot;
    private readonly string _libraryRoot;

    public RunExecutionHostedService(
        RunExecutionQueue queue,
        IServiceScopeFactory scopeFactory,
        IWorkerRunClient workerClient,
        RunEventBroadcaster broadcaster,
        IHubContext<RunEventsHub> hub,
        ILogger<RunExecutionHostedService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _workerClient = workerClient;
        _broadcaster = broadcaster;
        _hub = hub;
        _logger = logger;
        _scriptRoot = ResolvePath(configuration["SereinFlow:ScriptArtifactRoot"] ?? "data/script-artifacts", environment.ContentRootPath);
        _libraryRoot = ResolvePath(configuration["SereinFlow:LibraryDirectory"] ?? "data/libraries", environment.ContentRootPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingRunsAsync(stoppingToken);
        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var eventStore = scope.ServiceProvider.GetRequiredService<IFlowRunEventStore>();
            var runToken = _queue.Register(item.RunId, stoppingToken);
            try
            {
                _logger.LogInformation(
                    "Starting flow run {RunId} for project {ProjectId}, flow {FlowId}. 开始运行流程实例。",
                    item.RunId,
                    item.ProjectId,
                    item.Definition.Id);
                var run = await runStore.FindAsync(item.RunId, stoppingToken);
                if (run is null)
                    continue;
                run.MarkRunning(DateTimeOffset.UtcNow);
                await runStore.SaveAsync(run, stoppingToken);

                var request = new WorkerRunRequestDto(
                    WorkerProtocol.Version,
                    item.RunId,
                    item.Definition.Id,
                    item.Definition.Version,
                    JsonSerializer.Serialize(item.Definition, SereinJsonSerialization.CreateWebOptions()),
                    item.Deadline,
                    item.ProjectId.ToString("D"),
                    item.ProjectInputs,
                    item.MaxSteps,
                    _scriptRoot,
                    _libraryRoot,
                    item.MaxNodeVisits);

                var result = await _workerClient.RunAsync(
                    request,
                    new DelegateWorkerRunEventSink((workerEvent, token) => HandleEventAsync(workerEvent, runStore, eventStore, token)),
                    runToken);

                if (result.Status == FlowRunStatusDto.Cancelled)
                    run.Cancel(result.ErrorCode ?? "cancelled", DateTimeOffset.UtcNow);
                else
                    run.Complete(ToDomainStatus(result.Status), DateTimeOffset.UtcNow, result.ErrorMessage);
                await runStore.SaveAsync(run, CancellationToken.None);
                await PublishTerminalAsync(run, result, eventStore, CancellationToken.None);
                _logger.LogInformation(
                    "Flow run {RunId} finished with status {Status}. 流程实例已结束。",
                    item.RunId,
                    run.Status);
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                    var run = await runStore.FindAsync(item.RunId, CancellationToken.None);
                if (run is not null && !run.IsTerminal)
                {
                    run.Cancel("worker.cancelled", DateTimeOffset.UtcNow);
                    await runStore.SaveAsync(run, CancellationToken.None);
                    await PublishTerminalAsync(
                        run,
                        new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Cancelled, "worker.cancelled", "The worker run was cancelled. Worker 运行已取消。"),
                        eventStore,
                        CancellationToken.None);
                    _logger.LogInformation(
                        "Flow run {RunId} cancelled. 流程实例已取消。",
                        item.RunId);
                }
            }
            catch (Exception exception)
            {
                WorkerRunFailedLog(_logger, item.RunId, exception);
                var run = await runStore.FindAsync(item.RunId, CancellationToken.None);
                if (run is not null && !run.IsTerminal)
                {
                    var errorMessage = $"Worker run failed. Worker 运行失败。 {exception.Message}";
                    run.Complete(FlowRunStatus.Failed, DateTimeOffset.UtcNow, errorMessage);
                    await runStore.SaveAsync(run, CancellationToken.None);
                    await PublishTerminalAsync(
                        run,
                        new WorkerRunResultDto(WorkerProtocol.Version, run.Id, FlowRunStatusDto.Failed, "worker.run_failed", errorMessage),
                        eventStore,
                        CancellationToken.None);
                }
            }
            finally
            {
                _queue.Remove(item.RunId);
            }
        }
    }

    private async Task RecoverPendingRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runStore = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        foreach (var pending in await runStore.ListPendingAsync(cancellationToken))
        {
            await _queue.EnqueueAsync(
                new RunWorkItem(
                    pending.Run.Id,
                    pending.Run.ProjectId,
                    pending.Definition,
                    pending.Options.ProjectInputs,
                    pending.Options.Deadline,
                    pending.Options.MaxSteps,
                    pending.Options.MaxNodeVisits),
                cancellationToken);
        }
    }

    private async ValueTask HandleEventAsync(
        WorkerEventEnvelopeDto workerEvent,
        IFlowRunStore runStore,
        IFlowRunEventStore eventStore,
        CancellationToken cancellationToken)
    {
        if (workerEvent.ProtocolVersion != WorkerProtocol.Version
            || workerEvent.RunId == Guid.Empty
            || await runStore.FindAsync(workerEvent.RunId, cancellationToken) is null)
        {
            return;
        }

        // WorkerSupervisor already enforces monotonic sequences. Keep the
        // store boundary defensive so a custom client cannot duplicate events.
        // WorkerSupervisor 已校验序列；在持久化边界再次防御，避免自定义客户端写入重复事件。
        var lastSequence = await eventStore.GetLastSequenceAsync(workerEvent.RunId, cancellationToken);
        if (workerEvent.Sequence <= lastSequence)
            return;

        var type = workerEvent.EventType switch
        {
            WorkerEventType.RunStarted => "run.started",
            WorkerEventType.NodeStarted => "node.started",
            WorkerEventType.NodeCompleted => "node.completed",
            WorkerEventType.NodeFailed => "node.failed",
            WorkerEventType.NodeErrored => "node.error",
            WorkerEventType.RunCompleted => "run.completed",
            WorkerEventType.RunCancelled => "run.cancelled",
            _ => "log"
        };
        var item = new FlowRunEvent(
            workerEvent.RunId,
            workerEvent.Sequence,
            workerEvent.Timestamp,
            type,
            workerEvent.NodeId,
            workerEvent.PayloadJson);
        await eventStore.AppendAsync([item], cancellationToken);
        var dto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        try
        {
            await _broadcaster.PublishAsync(dto, cancellationToken);
        }
        catch (Exception exception)
        {
            // A disconnected SSE reader must never abort the Worker process.
            // The durable event has already been stored and can be replayed.
            // SSE 客户端断开不能终止 Worker；事件已持久化，可通过回放补齐。
            _logger.LogWarning(exception, "Live run event broadcast failed for {RunId}. 实时事件广播失败。", item.RunId);
        }
        try
        {
            await _hub.Clients.Group($"run:{item.RunId:D}").SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "SignalR run event push failed for {RunId}. SignalR 运行事件推送失败。", item.RunId);
        }
    }

    private async Task PublishTerminalAsync(
        FlowRun run,
        WorkerRunResultDto result,
        IFlowRunEventStore eventStore,
        CancellationToken cancellationToken)
    {
        var type = result.Status switch
        {
            FlowRunStatusDto.Cancelled => "run.cancelled",
            FlowRunStatusDto.TimedOut => "run.timed_out",
            FlowRunStatusDto.Failed => "run.failed",
            _ => "run.completed"
        };
        var sequence = await eventStore.GetLastSequenceAsync(run.Id, cancellationToken) + 1;
        var item = new FlowRunEvent(
            run.Id,
            sequence,
            DateTimeOffset.UtcNow,
            type,
            null,
            JsonSerializer.Serialize(
                new { result.Status, result.ErrorCode, result.ErrorMessage },
                SereinJsonSerialization.CreateWebOptions()));
        await eventStore.AppendAsync([item], cancellationToken);
        var dto = new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson);
        try
        {
            await _broadcaster.PublishAsync(dto, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Terminal run event broadcast failed for {RunId}. 终止事件广播失败。", run.Id);
        }
        try
        {
            await _hub.Clients.Group($"run:{run.Id:D}").SendAsync("runEvent", dto, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Terminal SignalR event push failed for {RunId}. 终止 SignalR 事件推送失败。", run.Id);
        }
    }

    private static FlowRunStatus ToDomainStatus(FlowRunStatusDto status)
        => status switch
        {
            FlowRunStatusDto.Succeeded => FlowRunStatus.Succeeded,
            FlowRunStatusDto.Cancelled => FlowRunStatus.Cancelled,
            FlowRunStatusDto.TimedOut => FlowRunStatus.TimedOut,
            _ => FlowRunStatus.Failed
        };

    private static string ResolvePath(string value, string root)
        => Path.IsPathRooted(value) ? value : Path.Combine(root, value);
}
