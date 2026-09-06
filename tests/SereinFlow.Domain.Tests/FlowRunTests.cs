using SereinFlow.Contracts;
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

    [Fact]
    public void DebugRunsRequireAndRetainTheirDebugSessionIdentity()
    {
        var sessionId = Guid.NewGuid();
        var run = FlowRun.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            FlowConcurrencyMode.Parallel,
            false,
            executionKind: FlowRunExecutionKind.Debug,
            debugSessionId: sessionId);

        Assert.Equal(FlowRunExecutionKind.Debug, run.ExecutionKind);
        Assert.Equal(sessionId, run.DebugSessionId);
        Assert.Throws<ArgumentException>(() => FlowRun.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            FlowConcurrencyMode.Parallel,
            false,
            executionKind: FlowRunExecutionKind.Debug));
    }

    [Fact]
    public void DebugSessionRetainsTheLastAcceptedControlSequence()
    {
        var now = DateTimeOffset.UtcNow;
        var session = FlowDebugSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], now);
        session.MarkRunning(now.AddSeconds(1));
        session.Pause("node-a", now.AddSeconds(2));

        session.AcceptCommand(4, now.AddSeconds(3));

        Assert.Equal(4, session.LastCommandSequence);
        Assert.Throws<InvalidOperationException>(() => session.AcceptCommand(4, now.AddSeconds(4)));
        Assert.Throws<InvalidOperationException>(() => session.AcceptCommand(3, now.AddSeconds(4)));
    }

    [Fact]
    public void DebugSessionTracksStructuredPauseAndLastNodeResult()
    {
        var now = DateTimeOffset.UtcNow;
        var session = FlowDebugSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], now);

        session.MarkRunning(now.AddSeconds(1));
        session.Pause(
            new FlowDebugPauseState(
                "node-a",
                "Action",
                3,
                2,
                Guid.NewGuid(),
                11,
                "{\"amount\":42}",
                now.AddSeconds(2)),
            now.AddSeconds(2));

        Assert.Equal(2, session.StateRevision);
        Assert.Equal("Action", session.PauseState!.NodeType);
        Assert.Equal(3, session.PauseState.Step);
        Assert.Equal(2, session.PauseState.FrameDepth);
        Assert.Equal(11, session.PauseState.BoundarySequence);
        Assert.Equal("{\"amount\":42}", session.PauseState.InputsJson);

        session.Resume(now.AddSeconds(3));
        Assert.Null(session.PauseState);
        Assert.Equal(3, session.StateRevision);

        session.RecordNodeResult(
            new FlowDebugNodeResult(
                "node-a",
                12,
                now.AddSeconds(4),
                "completed",
                "Success",
                "{\"amount\":42}",
                "{\"result\":84}",
                null,
                null),
            now.AddSeconds(4));

        Assert.Equal(4, session.StateRevision);
        Assert.Equal("node-a", session.LastNodeResult!.NodeId);
        Assert.Equal("{\"result\":84}", session.LastNodeResult.OutputsJson);

        session.Complete(FlowRunStatus.Succeeded, null, now.AddSeconds(5));
        Assert.Null(session.PauseState);
        Assert.NotNull(session.LastNodeResult);
        Assert.Equal(5, session.StateRevision);
    }

    [Fact]
    public void DebugSessionRetainsFailureDetailsAsTheLastNodeResult()
    {
        var now = DateTimeOffset.UtcNow;
        var session = FlowDebugSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], now);
        session.MarkRunning(now.AddSeconds(1));

        session.Fail("The run failed.", now.AddSeconds(3));
        session.RecordNodeResult(
            new FlowDebugNodeResult(
                "node-a",
                2,
                now.AddSeconds(2),
                "error",
                "Error",
                "{\"input\":1}",
                "{}",
                NodeErrorCodes.Failed,
                "The node failed."),
            now.AddSeconds(2));

        Assert.Equal(FlowDebugSessionStatus.Failed, session.Status);
        Assert.Null(session.PauseState);
        Assert.Equal("error", session.LastNodeResult!.Outcome);
        Assert.Equal(NodeErrorCodes.Failed, session.LastNodeResult.ErrorCode);
        Assert.Equal("The node failed.", session.LastNodeResult.ErrorMessage);
    }
}
