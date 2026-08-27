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
        var item = new FlowRunEventDto(
            runId,
            1,
            DateTimeOffset.UtcNow,
            "node.completed",
            "action-1",
            "{\"success\":true}");

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
        await broadcaster.PublishAsync(new FlowRunEventDto(
            runId,
            1,
            DateTimeOffset.UtcNow,
            "run.completed",
            null,
            "{}"));

        var reader = broadcaster.Subscribe(runId);

        Assert.False(await reader.WaitToReadAsync());
    }

    [Fact]
    public async Task InterruptedEventCompletesLaterSseSubscriptions()
    {
        var broadcaster = new RunEventBroadcaster();
        var runId = Guid.NewGuid();
        await broadcaster.PublishAsync(new FlowRunEventDto(
            runId,
            1,
            DateTimeOffset.UtcNow,
            "run.interrupted",
            null,
            "{}"));

        var reader = broadcaster.Subscribe(runId);

        Assert.False(await reader.WaitToReadAsync());
    }

    [Fact]
    public async Task InterruptionServiceArchivesRunningRunAndAppendsOneAuditEvent()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var run = FlowRun.Start(Guid.NewGuid(), Guid.NewGuid(), 1, startedAt);
        run.MarkRunning(startedAt, startedAt.AddMinutes(5));
        var runStore = new InMemoryRunStore(run);
        var eventStore = new InMemoryRunEventStore();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        await using var provider = services.BuildServiceProvider();
        var service = new RunInterruptionService(
            runStore,
            eventStore,
            new RunEventBroadcaster(),
            provider.GetRequiredService<IHubContext<RunEventsHub>>(),
            NullLogger<RunInterruptionService>.Instance);

        var first = await service.InterruptAsync(
            run.Id,
            "engine_restart",
            "run.worker_lost",
            "The worker was lost. Worker 已丢失。");
        var second = await service.InterruptAsync(
            run.Id,
            "engine_restart",
            "run.worker_lost",
            "The worker was lost. Worker 已丢失。");

        Assert.True(first.IsInterrupted);
        Assert.Equal(RunInterruptionDisposition.AlreadyTerminal, second.Disposition);
        Assert.Equal(FlowRunStatus.Interrupted, run.Status);
        var audit = Assert.Single(eventStore.Events);
        Assert.Equal("run.interrupted", audit.Type);
        Assert.Equal(1, audit.Sequence);
    }

    [Fact]
    public async Task SchedulerWakeUpsAreCoalesced()
    {
        var queue = new RunExecutionQueue(Options.Create(new RunExecutionOptions()));
        queue.Configure(new RunExecutionSettingsDto(
            QueueCapacity: 100,
            MaxConcurrentRuns: 4,
            MaxConcurrentListenerRuns: 1,
            MaxConcurrentRunsPerProject: 2,
            QueueWaitTimeoutSeconds: 60,
            ShutdownGracePeriodSeconds: 10,
            SynchronousInvocationTimeoutSeconds: 30));
        queue.Configure(new RunExecutionSettingsDto(
            QueueCapacity: 100,
            MaxConcurrentRuns: 4,
            MaxConcurrentListenerRuns: 1,
            MaxConcurrentRunsPerProject: 2,
            QueueWaitTimeoutSeconds: 60,
            ShutdownGracePeriodSeconds: 10,
            SynchronousInvocationTimeoutSeconds: 30));

        Assert.True(await queue.WaitForSchedulerSignalAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.False(await queue.WaitForSchedulerSignalAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None));
    }

    [Fact]
    public void ExecutionSlotsRespectGlobalProjectAndListenerLimits()
    {
        var queue = new RunExecutionQueue(Options.Create(new RunExecutionOptions
        {
            MaxConcurrentRuns = 2,
            MaxConcurrentRunsPerProject = 1,
            MaxConcurrentListenerRuns = 1
        }));
        var firstProject = Guid.NewGuid();
        var first = CreateWorkItem(firstProject, listener: false);
        var sameProject = CreateWorkItem(firstProject, listener: false);
        var secondProject = CreateWorkItem(Guid.NewGuid(), listener: false);

        Assert.True(queue.TryAcquireExecutionSlots(first, out var firstLease));
        queue.PromoteToActive(first);
        Assert.False(queue.TryAcquireExecutionSlots(sameProject, out _));
        Assert.True(queue.TryAcquireExecutionSlots(secondProject, out var secondLease));
        queue.PromoteToActive(secondProject);
        Assert.Equal(2, queue.GetSnapshot().ActiveRunCount);

        secondLease!.Dispose();
        firstLease!.Dispose();

        var firstListener = CreateWorkItem(Guid.NewGuid(), listener: true);
        var secondListener = CreateWorkItem(Guid.NewGuid(), listener: true);
        Assert.True(queue.TryAcquireExecutionSlots(firstListener, out var listenerLease));
        queue.PromoteToActive(firstListener);
        Assert.False(queue.TryAcquireExecutionSlots(secondListener, out _));
        Assert.Equal(1, queue.GetSnapshot().ActiveListenerRunCount);

        listenerLease!.Dispose();
        Assert.Equal(0, queue.GetSnapshot().ActiveRunCount);
    }

    [Fact]
    public void ConfiguringLimitsChangesAdmissionForNewRunsWithoutInterruptingActiveRuns()
    {
        var queue = new RunExecutionQueue(Options.Create(new RunExecutionOptions
        {
            QueueCapacity = 1,
            MaxConcurrentRuns = 1,
            MaxConcurrentRunsPerProject = 1,
            MaxConcurrentListenerRuns = 0
        }));
        var first = CreateWorkItem(Guid.NewGuid(), listener: false);
        var second = CreateWorkItem(Guid.NewGuid(), listener: false);

        Assert.True(queue.TryAcquireExecutionSlots(first, out var firstLease));
        queue.PromoteToActive(first);
        Assert.False(queue.TryAcquireExecutionSlots(second, out _));

        var updated = queue.Configure(new RunExecutionSettingsDto(
            QueueCapacity: 2,
            MaxConcurrentRuns: 2,
            MaxConcurrentListenerRuns: 1,
            MaxConcurrentRunsPerProject: 2,
            QueueWaitTimeoutSeconds: 60,
            ShutdownGracePeriodSeconds: 10,
            SynchronousInvocationTimeoutSeconds: 30));

        Assert.Equal(2, updated.MaxConcurrentRuns);
        Assert.Equal(2, updated.QueueCapacity);
        Assert.True(queue.TryAcquireExecutionSlots(second, out var secondLease));
        queue.PromoteToActive(second);
        Assert.Equal(2, queue.GetSnapshot().ActiveRunCount);

        secondLease!.Dispose();
        firstLease!.Dispose();
    }

    [Fact]
    public void QueueCapacityChangeAllowsAdditionalReservations()
    {
        var queue = new RunExecutionQueue(Options.Create(new RunExecutionOptions { QueueCapacity = 1 }));
        using var firstReservation = queue.TryReserve();
        Assert.NotNull(firstReservation);
        Assert.Null(queue.TryReserve());

        queue.Configure(new RunExecutionSettingsDto(
            QueueCapacity: 2,
            MaxConcurrentRuns: 4,
            MaxConcurrentListenerRuns: 1,
            MaxConcurrentRunsPerProject: 2,
            QueueWaitTimeoutSeconds: 60,
            ShutdownGracePeriodSeconds: 10,
            SynchronousInvocationTimeoutSeconds: 30));

        using var secondReservation = queue.TryReserve();
        Assert.NotNull(secondReservation);
        Assert.Equal(2, queue.GetSnapshot().QueuedCount);
    }

    private static RunWorkItem CreateWorkItem(Guid projectId, bool listener)
    {
        var nodes = listener
            ? new[]
            {
                new NodeDto(
                    "listener",
                    NodeTypeDto.Flipflop,
                    "Listener",
                    0,
                    0,
                    Array.Empty<NodePortDto>(),
                    Array.Empty<NodeParameterDto>(),
                    null)
            }
            : Array.Empty<NodeDto>();
        var definition = new FlowDefinitionDto(
            Guid.NewGuid(),
            4,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, nodes, Array.Empty<ConnectionDto>())],
            listener ? "listener" : "entry",
            string.Empty,
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
        return new RunWorkItem(
            Guid.NewGuid(),
            projectId,
            definition,
            null,
            DateTimeOffset.UtcNow,
            300,
            10_000);
    }

    private sealed class InMemoryRunStore : IFlowRunStore
    {
        private readonly FlowRun _run;

        public InMemoryRunStore(FlowRun run) => _run = run;

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(run);

        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new FlowRunAdmissionResult(run));

        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowRun>>([_run]);

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
            => Task.FromResult(run);

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowRun?>(_run.Id == runId ? _run : null);

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowDefinitionDto?>(null);

        public Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default)
            => Task.FromResult(_run.Id == run.Id);

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
            => Task.FromResult<IReadOnlyList<FlowRunEvent>>(Events
                .Where(item => item.RunId == runId && item.Sequence > sequenceExclusive)
                .ToArray());

        public Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult(Events.Where(item => item.RunId == runId).Select(item => item.Sequence).DefaultIfEmpty().Max());

        public void Append(IReadOnlyList<FlowRunEvent> events) => Events.AddRange(events);

        public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
            => Events.Where(item => item.RunId == runId && item.Sequence > sequenceExclusive).ToArray();
    }
}
