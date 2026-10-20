using SereinFlow.Api;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SereinFlow.ArchitectureTests;
public sealed class RunEventBroadcasterTests
{
    [Fact]
    public async Task PublishAsyncDeliversACopyToEachSseSubscriber()
    {
        var broadcaster = new RunEventBroadcaster();
        var runId = Guid.NewGuid();
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var first = broadcaster.Subscribe(runId, firstCancellation.Token);
        var second = broadcaster.Subscribe(runId, secondCancellation.Token);
        var item = new FlowRunEventDto(runId, 1, DateTimeOffset.UtcNow, "node.completed", "action-1", "{\"success\":true}");
        await broadcaster.PublishAsync(item);
        var firstItem = await first.ReadAsync();
        var secondItem = await second.ReadAsync();
        Assert.Equal(item, firstItem);
        Assert.Equal(item, secondItem);
    }

    [Fact]
    public async Task TerminalEventCompletesLaterSseSubscriptions()
    {
        var broadcaster = new RunEventBroadcaster();
        var runId = Guid.NewGuid();
        var flowRunEventDto = new FlowRunEventDto(runId, 1, DateTimeOffset.UtcNow, "run.completed", null, "{}");
        await broadcaster.PublishAsync(flowRunEventDto);
        var reader = broadcaster.Subscribe(runId);
        var waitToReadAsyncResult = await reader.WaitToReadAsync();
        Assert.False(waitToReadAsyncResult);
    }

    [Fact]
    public async Task InterruptedEventCompletesLaterSseSubscriptions()
    {
        var broadcaster = new RunEventBroadcaster();
        var runId = Guid.NewGuid();
        var flowRunEventDto1 = new FlowRunEventDto(runId, 1, DateTimeOffset.UtcNow, RunErrorCodes.Interrupted, null, "{}");
        await broadcaster.PublishAsync(flowRunEventDto1);
        var reader = broadcaster.Subscribe(runId);
        var waitToReadAsyncResult2 = await reader.WaitToReadAsync();
        Assert.False(waitToReadAsyncResult2);
    }

    [Fact]
    public async Task InterruptionServiceArchivesRunningRunAndAppendsOneAuditEvent()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var newGuidResult = Guid.NewGuid();
        var newGuidResult3 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult, newGuidResult3, 1, startedAt);
        var addMinutesResult = startedAt.AddMinutes(5);
        run.MarkRunning(startedAt, addMinutesResult);
        var runStore = new InMemoryRunStore(run);
        var eventStore = new InMemoryRunEventStore();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        await using var provider = services.BuildServiceProvider();
        var runEventBroadcaster = new RunEventBroadcaster();
        var getRequiredServiceResult = provider.GetRequiredService<IHubContext<RunEventsHub>>();
        var service = new RunInterruptionService(runStore, eventStore, runEventBroadcaster, getRequiredServiceResult, NullLogger<RunInterruptionService>.Instance);
        var first = await service.InterruptAsync(run.Id, "engine_restart", RunErrorCodes.WorkerLost, "The worker was lost. Worker 已丢失。");
        var second = await service.InterruptAsync(run.Id, "engine_restart", RunErrorCodes.WorkerLost, "The worker was lost. Worker 已丢失。");
        Assert.True(first.IsInterrupted);
        Assert.Equal(RunInterruptionDisposition.AlreadyTerminal, second.Disposition);
        Assert.Equal(FlowRunStatus.Interrupted, run.Status);
        var audit = Assert.Single(eventStore.Events);
        Assert.Equal(RunErrorCodes.Interrupted, audit.Type);
        Assert.Equal(1, audit.Sequence);
    }

    [Fact]
    public async Task SchedulerWakeUpsAreCoalesced()
    {
        var runExecutionOptions = new RunExecutionOptions();
        var createResult = Options.Create(runExecutionOptions);
        var queue = new RunExecutionQueue(createResult);
        var runExecutionSettingsDto = new RunExecutionSettingsDto(QueueCapacity: 100, MaxConcurrentRuns: 4, MaxConcurrentListenerRuns: 1, MaxConcurrentRunsPerProject: 2, QueueWaitTimeoutSeconds: 60, ShutdownGracePeriodSeconds: 10, SynchronousInvocationTimeoutSeconds: 30);
        queue.Configure(runExecutionSettingsDto);
        var runExecutionSettingsDto4 = new RunExecutionSettingsDto(QueueCapacity: 100, MaxConcurrentRuns: 4, MaxConcurrentListenerRuns: 1, MaxConcurrentRunsPerProject: 2, QueueWaitTimeoutSeconds: 60, ShutdownGracePeriodSeconds: 10, SynchronousInvocationTimeoutSeconds: 30);
        queue.Configure(runExecutionSettingsDto4);
        var fromSecondsResult = TimeSpan.FromSeconds(1);
        var waitForSchedulerSignalAsyncResult = await queue.WaitForSchedulerSignalAsync(fromSecondsResult, CancellationToken.None);
        Assert.True(waitForSchedulerSignalAsyncResult);
        var fromMillisecondsResult = TimeSpan.FromMilliseconds(10);
        var waitForSchedulerSignalAsyncResult5 = await queue.WaitForSchedulerSignalAsync(fromMillisecondsResult, CancellationToken.None);
        Assert.False(waitForSchedulerSignalAsyncResult5);
    }

    [Fact]
    public void ExecutionSlotsRespectGlobalProjectAndListenerLimits()
    {
        var runExecutionOptions6 = new RunExecutionOptions
        {
            MaxConcurrentRuns = 2,
            MaxConcurrentRunsPerProject = 1,
            MaxConcurrentListenerRuns = 1
        };
        var createResult7 = Options.Create(runExecutionOptions6);
        var queue = new RunExecutionQueue(createResult7);
        var firstProject = Guid.NewGuid();
        var first = CreateWorkItem(firstProject, listener: false);
        var sameProject = CreateWorkItem(firstProject, listener: false);
        var newGuidResult8 = Guid.NewGuid();
        var secondProject = CreateWorkItem(newGuidResult8, listener: false);
        var tryAcquireExecutionSlotsResult = queue.TryAcquireExecutionSlots(first, out var firstLease);
        Assert.True(tryAcquireExecutionSlotsResult);
        queue.PromoteToActive(first);
        var tryAcquireExecutionSlotsResult9 = queue.TryAcquireExecutionSlots(sameProject, out _);
        Assert.False(tryAcquireExecutionSlotsResult9);
        var tryAcquireExecutionSlotsResult10 = queue.TryAcquireExecutionSlots(secondProject, out var secondLease);
        Assert.True(tryAcquireExecutionSlotsResult10);
        queue.PromoteToActive(secondProject);
        var evaluatedValue = queue.GetSnapshot().ActiveRunCount;
        Assert.Equal(2, evaluatedValue);
        secondLease!.Dispose();
        firstLease!.Dispose();
        var newGuidResult11 = Guid.NewGuid();
        var firstListener = CreateWorkItem(newGuidResult11, listener: true);
        var newGuidResult12 = Guid.NewGuid();
        var secondListener = CreateWorkItem(newGuidResult12, listener: true);
        var tryAcquireExecutionSlotsResult13 = queue.TryAcquireExecutionSlots(firstListener, out var listenerLease);
        Assert.True(tryAcquireExecutionSlotsResult13);
        queue.PromoteToActive(firstListener);
        var tryAcquireExecutionSlotsResult14 = queue.TryAcquireExecutionSlots(secondListener, out _);
        Assert.False(tryAcquireExecutionSlotsResult14);
        var evaluatedValue15 = queue.GetSnapshot().ActiveListenerRunCount;
        Assert.Equal(1, evaluatedValue15);
        listenerLease!.Dispose();
        var evaluatedValue16 = queue.GetSnapshot().ActiveRunCount;
        Assert.Equal(0, evaluatedValue16);
    }

    [Fact]
    public void ConfiguringLimitsChangesAdmissionForNewRunsWithoutInterruptingActiveRuns()
    {
        var runExecutionOptions17 = new RunExecutionOptions
        {
            QueueCapacity = 1,
            MaxConcurrentRuns = 1,
            MaxConcurrentRunsPerProject = 1,
            MaxConcurrentListenerRuns = 0
        };
        var createResult18 = Options.Create(runExecutionOptions17);
        var queue = new RunExecutionQueue(createResult18);
        var newGuidResult19 = Guid.NewGuid();
        var first = CreateWorkItem(newGuidResult19, listener: false);
        var newGuidResult20 = Guid.NewGuid();
        var second = CreateWorkItem(newGuidResult20, listener: false);
        var tryAcquireExecutionSlotsResult21 = queue.TryAcquireExecutionSlots(first, out var firstLease);
        Assert.True(tryAcquireExecutionSlotsResult21);
        queue.PromoteToActive(first);
        var tryAcquireExecutionSlotsResult22 = queue.TryAcquireExecutionSlots(second, out _);
        Assert.False(tryAcquireExecutionSlotsResult22);
        var runExecutionSettingsDto23 = new RunExecutionSettingsDto(QueueCapacity: 2, MaxConcurrentRuns: 2, MaxConcurrentListenerRuns: 1, MaxConcurrentRunsPerProject: 2, QueueWaitTimeoutSeconds: 60, ShutdownGracePeriodSeconds: 10, SynchronousInvocationTimeoutSeconds: 30);
        var updated = queue.Configure(runExecutionSettingsDto23);
        Assert.Equal(2, updated.MaxConcurrentRuns);
        Assert.Equal(2, updated.QueueCapacity);
        var tryAcquireExecutionSlotsResult24 = queue.TryAcquireExecutionSlots(second, out var secondLease);
        Assert.True(tryAcquireExecutionSlotsResult24);
        queue.PromoteToActive(second);
        var evaluatedValue25 = queue.GetSnapshot().ActiveRunCount;
        Assert.Equal(2, evaluatedValue25);
        secondLease!.Dispose();
        firstLease!.Dispose();
    }

    [Fact]
    public void QueueCapacityChangeAllowsAdditionalReservations()
    {
        var runExecutionOptions26 = new RunExecutionOptions
        {
            QueueCapacity = 1
        };
        var createResult27 = Options.Create(runExecutionOptions26);
        var queue = new RunExecutionQueue(createResult27);
        using var firstReservation = queue.TryReserve();
        Assert.NotNull(firstReservation);
        var tryReserveResult = queue.TryReserve();
        Assert.Null(tryReserveResult);
        var runExecutionSettingsDto28 = new RunExecutionSettingsDto(QueueCapacity: 2, MaxConcurrentRuns: 4, MaxConcurrentListenerRuns: 1, MaxConcurrentRunsPerProject: 2, QueueWaitTimeoutSeconds: 60, ShutdownGracePeriodSeconds: 10, SynchronousInvocationTimeoutSeconds: 30);
        queue.Configure(runExecutionSettingsDto28);
        using var secondReservation = queue.TryReserve();
        Assert.NotNull(secondReservation);
        var evaluatedValue29 = queue.GetSnapshot().QueuedCount;
        Assert.Equal(2, evaluatedValue29);
    }

    private static RunWorkItem CreateWorkItem(Guid projectId, bool listener)
    {
        var nodes = listener ? new[]
        {
            resolveNodeDto()
        }

        : Array.Empty<NodeDto>();
        var newGuidResult31 = Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.CanvasDto> evaluatedValue33 = [resolveCanvasDto()];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(newGuidResult31, 4, 1, evaluatedValue33, listener ? "listener" : "entry", string.Empty, RunPolicy: flowRunPolicyDto);
        var newGuidResult34 = Guid.NewGuid();
        var runWorkItem = new RunWorkItem(newGuidResult34, projectId, definition, null, DateTimeOffset.UtcNow, 300, 10_000);
        return runWorkItem;
        global::SereinFlow.Contracts.NodeDto resolveNodeDto()
        {
            var emptyResult = Array.Empty<NodePortDto>();
            var emptyResult30 = Array.Empty<NodeParameterDto>();
            var nodeDto = new NodeDto("listener", NodeTypeDto.Flipflop, "Listener", 0, 0, emptyResult, emptyResult30, null);
            return nodeDto;
        }

        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto()
        {
            var emptyResult32 = Array.Empty<ConnectionDto>();
            var canvasDto = new CanvasDto("main", CanvasLifecycleDto.Main, nodes, emptyResult32);
            return canvasDto;
        }
    }

    private sealed class InMemoryRunStore : IFlowRunStore
    {
        private readonly FlowRun _run;
        public InMemoryRunStore(FlowRun run)
        {
            _run = run;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
        {
            var fromResultResult = Task.FromResult(run);
            return fromResultResult;
        }

        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
        {
            var flowRunAdmissionResult = new FlowRunAdmissionResult(run);
            var fromResultResult35 = Task.FromResult(flowRunAdmissionResult);
            return fromResultResult35;
        }

        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult36 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult36;
        }

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult37 = Task.FromResult<IReadOnlyList<FlowRun>>([_run]);
            return fromResultResult37;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        {
            var fromResultResult38 = Task.FromResult(run);
            return fromResultResult38;
        }

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult39 = Task.FromResult<FlowRun?>(_run.Id == runId ? _run : null);
            return fromResultResult39;
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult40 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult40;
        }

        public Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default)
        {
            var fromResultResult41 = Task.FromResult(_run.Id == run.Id);
            return fromResultResult41;
        }

        public FlowRun CreateWithSnapshot(FlowRun run, FlowDefinitionDto definition) => run;
        public FlowRun? Find(Guid runId) => _run.Id == runId ? _run : null;
        public FlowDefinitionDto? GetSnapshot(Guid runId) => null;
        public bool Save(FlowRun run) => _run.Id == run.Id;
    }

    private sealed class InMemoryRunEventStore : IFlowRunEventStore
    {
        public List<FlowRunEvent> Events { get; } = [];

        public Task AppendAsync(IReadOnlyList<FlowRunEvent> events, CancellationToken cancellationToken = default)
        {
            Events.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid runId, long sequenceExclusive, CancellationToken cancellationToken = default)
        {
            var whereResult = Events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive);
            var toArrayResult = whereResult.ToArray();
            var fromResultResult42 = Task.FromResult<IReadOnlyList<FlowRunEvent>>(toArrayResult);
            return fromResultResult42;
        }

        public Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var whereResult43 = Events.Where(item => item.RunId == runId);
            var selectResult = whereResult43.Select(item => item.Sequence);
            var defaultIfEmptyResult = selectResult.DefaultIfEmpty();
            var maxResult = defaultIfEmptyResult.Max();
            var fromResultResult44 = Task.FromResult(maxResult);
            return fromResultResult44;
        }

        public void Append(IReadOnlyList<FlowRunEvent> events)
        {
            Events.AddRange(events);
        }

        public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
        {
            var whereResult45 = Events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive);
            var toArrayResult46 = whereResult45.ToArray();
            return toArrayResult46;
        }
    }
}
