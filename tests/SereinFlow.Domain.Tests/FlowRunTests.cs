using SereinFlow.Domain;

namespace SereinFlow.Domain.Tests;

public sealed class FlowRunTests
{
    [Fact]
    public void RunTransitionsToTerminalSuccessOnce()
    {
        var run = FlowRun.Start(Guid.NewGuid(), 4, DateTimeOffset.UtcNow);

        run.MarkRunning(DateTimeOffset.UtcNow.AddMilliseconds(1));
        run.Complete(FlowRunStatus.Succeeded, DateTimeOffset.UtcNow.AddMilliseconds(2));

        Assert.Equal(FlowRunStatus.Succeeded, run.Status);
        Assert.True(run.IsTerminal);
        Assert.Throws<InvalidOperationException>(() => run.Cancel("late cancellation", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RunCancellationIsIdempotentBeforeTerminalState()
    {
        var run = FlowRun.Start(Guid.NewGuid(), 1, DateTimeOffset.UtcNow);

        run.Cancel("requested", DateTimeOffset.UtcNow.AddSeconds(1));
        run.Cancel("requested again", DateTimeOffset.UtcNow.AddSeconds(2));

        Assert.Equal(FlowRunStatus.Cancelled, run.Status);
        Assert.Equal("requested", run.CancellationReason);
    }

    [Fact]
    public void RunningRunCanBeInterruptedOnlyOnce()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var run = FlowRun.Start(Guid.NewGuid(), 1, startedAt);
        run.MarkRunning(startedAt.AddSeconds(1));

        run.Interrupt(
            "engine_restart",
            "The worker was lost. Worker 已丢失。",
            startedAt.AddSeconds(2));

        Assert.Equal(FlowRunStatus.Interrupted, run.Status);
        Assert.True(run.IsTerminal);
        Assert.Equal("engine_restart", run.CancellationReason);
        Assert.Equal("The worker was lost. Worker 已丢失。", run.ErrorSummary);
        Assert.Throws<InvalidOperationException>(() => run.Complete(FlowRunStatus.Succeeded, startedAt.AddSeconds(3)));
        Assert.Throws<InvalidOperationException>(() => run.Cancel("late cancellation", startedAt.AddSeconds(3)));
        Assert.Throws<InvalidOperationException>(() => run.Interrupt("late interruption", "ignored", startedAt.AddSeconds(3)));
    }

    [Fact]
    public void PendingRunCannotBeInterrupted()
    {
        var run = FlowRun.Start(Guid.NewGuid(), 1, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => run.Interrupt(
            "engine_restart",
            "The worker was lost. Worker 已丢失。",
            DateTimeOffset.UtcNow));
    }
}
