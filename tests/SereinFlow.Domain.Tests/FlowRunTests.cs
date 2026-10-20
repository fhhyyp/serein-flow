using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Domain.Tests;
public sealed class FlowRunTests
{
    [Fact]
    public void RunTransitionsToTerminalSuccessOnce()
    {
        var newGuidResult = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult, 4, DateTimeOffset.UtcNow);
        var addMillisecondsResult = DateTimeOffset.UtcNow.AddMilliseconds(1);
        run.MarkRunning(addMillisecondsResult);
        var addMillisecondsResult1 = DateTimeOffset.UtcNow.AddMilliseconds(2);
        run.Complete(FlowRunStatus.Succeeded, addMillisecondsResult1);
        Assert.Equal(FlowRunStatus.Succeeded, run.Status);
        Assert.True(run.IsTerminal);
        Assert.Throws<InvalidOperationException>(() =>
        {
            run.Cancel("late cancellation", DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public void RunRetainsItsImmutableDefinitionChecksum()
    {
        var newGuidResult2 = Guid.NewGuid();
        var newGuidResult3 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult2, newGuidResult3, 4, DateTimeOffset.UtcNow, FlowConcurrencyMode.Parallel, false, definitionChecksum: " checksum-v4 ");
        Assert.Equal("checksum-v4", run.DefinitionChecksum);
    }

    [Fact]
    public void RunCancellationIsIdempotentBeforeTerminalState()
    {
        var newGuidResult4 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult4, 1, DateTimeOffset.UtcNow);
        var addSecondsResult = DateTimeOffset.UtcNow.AddSeconds(1);
        run.Cancel("requested", addSecondsResult);
        var addSecondsResult5 = DateTimeOffset.UtcNow.AddSeconds(2);
        run.Cancel("requested again", addSecondsResult5);
        Assert.Equal(FlowRunStatus.Cancelled, run.Status);
        Assert.Equal("requested", run.CancellationReason);
    }

    [Fact]
    public void RunningRunCanBeInterruptedOnlyOnce()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var newGuidResult9 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult9, 1, startedAt);
        var addSecondsResult10 = startedAt.AddSeconds(1);
        run.MarkRunning(addSecondsResult10);
        var addSecondsResult11 = startedAt.AddSeconds(2);
        run.Interrupt("engine_restart", "The worker was lost. Worker 已丢失。", addSecondsResult11);
        Assert.Equal(FlowRunStatus.Interrupted, run.Status);
        Assert.True(run.IsTerminal);
        Assert.Equal("engine_restart", run.CancellationReason);
        Assert.Equal("The worker was lost. Worker 已丢失。", run.ErrorSummary);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSecondsResult6 = startedAt.AddSeconds(3);
            run.Complete(FlowRunStatus.Succeeded, addSecondsResult6);
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSecondsResult7 = startedAt.AddSeconds(3);
            run.Cancel("late cancellation", addSecondsResult7);
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSecondsResult8 = startedAt.AddSeconds(3);
            run.Interrupt("late interruption", "ignored", addSecondsResult8);
        });
    }

    [Fact]
    public void PendingRunCannotBeInterrupted()
    {
        var newGuidResult12 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult12, 1, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() =>
        {
            run.Interrupt("engine_restart", "The worker was lost. Worker 已丢失。", DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public void DebugRunsRequireAndRetainTheirDebugSessionIdentity()
    {
        var sessionId = Guid.NewGuid();
        var newGuidResult15 = Guid.NewGuid();
        var newGuidResult16 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult15, newGuidResult16, 1, DateTimeOffset.UtcNow, FlowConcurrencyMode.Parallel, false, executionKind: FlowRunExecutionKind.Debug, debugSessionId: sessionId);
        Assert.Equal(FlowRunExecutionKind.Debug, run.ExecutionKind);
        Assert.Equal(sessionId, run.DebugSessionId);
        Assert.Throws<ArgumentException>(() =>
        {
            var newGuidResult13 = Guid.NewGuid();
            var newGuidResult14 = Guid.NewGuid();
            var startResult = FlowRun.Start(newGuidResult13, newGuidResult14, 1, DateTimeOffset.UtcNow, FlowConcurrencyMode.Parallel, false, executionKind: FlowRunExecutionKind.Debug);
            return startResult;
        });
    }

    [Fact]
    public void DebugSessionRetainsTheLastAcceptedControlSequence()
    {
        var now = DateTimeOffset.UtcNow;
        var newGuidResult19 = Guid.NewGuid();
        var newGuidResult20 = Guid.NewGuid();
        var newGuidResult21 = Guid.NewGuid();
        var session = FlowDebugSession.Create(newGuidResult19, newGuidResult20, newGuidResult21, [], now);
        var addSecondsResult22 = now.AddSeconds(1);
        session.MarkRunning(addSecondsResult22);
        var addSecondsResult23 = now.AddSeconds(2);
        session.Pause("node-a", addSecondsResult23);
        var addSecondsResult24 = now.AddSeconds(3);
        session.AcceptCommand(4, addSecondsResult24);
        Assert.Equal(4, session.LastCommandSequence);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSecondsResult17 = now.AddSeconds(4);
            session.AcceptCommand(4, addSecondsResult17);
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSecondsResult18 = now.AddSeconds(4);
            session.AcceptCommand(3, addSecondsResult18);
        });
    }

    [Fact]
    public void DebugSessionTracksStructuredPauseAndLastNodeResult()
    {
        var now = DateTimeOffset.UtcNow;
        var newGuidResult25 = Guid.NewGuid();
        var newGuidResult26 = Guid.NewGuid();
        var newGuidResult27 = Guid.NewGuid();
        var session = FlowDebugSession.Create(newGuidResult25, newGuidResult26, newGuidResult27, [], now);
        var addSecondsResult28 = now.AddSeconds(1);
        session.MarkRunning(addSecondsResult28);
        var newGuidResult29 = Guid.NewGuid();
        var addSecondsResult30 = now.AddSeconds(2);
        var flowDebugPauseState = new FlowDebugPauseState("node-a", "Action", 3, 2, newGuidResult29, 11, "{\"amount\":42}", addSecondsResult30);
        var addSecondsResult31 = now.AddSeconds(2);
        session.Pause(flowDebugPauseState, addSecondsResult31);
        Assert.Equal(2, session.StateRevision);
        Assert.Equal("Action", session.PauseState!.NodeType);
        Assert.Equal(3, session.PauseState.Step);
        Assert.Equal(2, session.PauseState.FrameDepth);
        Assert.Equal(11, session.PauseState.BoundarySequence);
        Assert.Equal("{\"amount\":42}", session.PauseState.InputsJson);
        var addSecondsResult32 = now.AddSeconds(3);
        session.Resume(addSecondsResult32);
        Assert.Null(session.PauseState);
        Assert.Equal(3, session.StateRevision);
        var addSecondsResult33 = now.AddSeconds(4);
        var flowDebugNodeResult = new FlowDebugNodeResult("node-a", 12, addSecondsResult33, "completed", "Success", "{\"amount\":42}", "{\"result\":84}", null, null);
        var addSecondsResult34 = now.AddSeconds(4);
        session.RecordNodeResult(flowDebugNodeResult, addSecondsResult34);
        Assert.Equal(4, session.StateRevision);
        Assert.Equal("node-a", session.LastNodeResult!.NodeId);
        Assert.Equal("{\"result\":84}", session.LastNodeResult.OutputsJson);
        var addSecondsResult35 = now.AddSeconds(5);
        session.Complete(FlowRunStatus.Succeeded, null, addSecondsResult35);
        Assert.Null(session.PauseState);
        Assert.NotNull(session.LastNodeResult);
        Assert.Equal(5, session.StateRevision);
    }

    [Fact]
    public void DebugSessionRetainsFailureDetailsAsTheLastNodeResult()
    {
        var now = DateTimeOffset.UtcNow;
        var newGuidResult36 = Guid.NewGuid();
        var newGuidResult37 = Guid.NewGuid();
        var newGuidResult38 = Guid.NewGuid();
        var session = FlowDebugSession.Create(newGuidResult36, newGuidResult37, newGuidResult38, [], now);
        var addSecondsResult39 = now.AddSeconds(1);
        session.MarkRunning(addSecondsResult39);
        var addSecondsResult40 = now.AddSeconds(3);
        session.Fail("The run failed.", addSecondsResult40);
        var addSecondsResult41 = now.AddSeconds(2);
        var flowDebugNodeResult42 = new FlowDebugNodeResult("node-a", 2, addSecondsResult41, "error", "Error", "{\"input\":1}", "{}", NodeErrorCodes.Failed, "The node failed.");
        var addSecondsResult43 = now.AddSeconds(2);
        session.RecordNodeResult(flowDebugNodeResult42, addSecondsResult43);
        Assert.Equal(FlowDebugSessionStatus.Failed, session.Status);
        Assert.Null(session.PauseState);
        Assert.Equal("error", session.LastNodeResult!.Outcome);
        Assert.Equal(NodeErrorCodes.Failed, session.LastNodeResult.ErrorCode);
        Assert.Equal("The node failed.", session.LastNodeResult.ErrorMessage);
    }
}
