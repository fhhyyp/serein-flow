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
}
