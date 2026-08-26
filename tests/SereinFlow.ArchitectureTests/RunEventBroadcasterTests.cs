using SereinFlow.Api;
using SereinFlow.Contracts;
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
}
