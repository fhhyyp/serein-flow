using System.Collections.Generic;
using System.Collections.Concurrent;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime.Tests;
public sealed class DebugExecutionGateTests
{
    [Fact]
    public async Task BreakpointPausesBeforeTheNodeExecutorAndExposesResolvedInputs()
    {
        IEnumerable<NodeParameterDefinition> evaluatedValue = [new NodeParameterDefinition("count", "42")];
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: evaluatedValue);
        var definition = CreateDefinition([node], [], node.Id);
        var pause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new DebugExecutionGate([node.Id], (boundary, _) =>
        {
            pause.TrySetResult(boundary);
            return ValueTask.CompletedTask;
        });
        var executor = new CountingActionExecutor();
        var runner = CreateRunner(executor, gate);
        await using var session = new FlowExecutionSession();
        var runAsyncResult = runner.RunAsync(definition, session);
        var runTask = runAsyncResult.AsTask();
        var fromSecondsResult = TimeSpan.FromSeconds(2);
        var boundary = await pause.Task.WaitAsync(fromSecondsResult);
        Assert.Equal(node.Id, boundary.NodeId);
        Assert.Equal(NodeType.Action, boundary.NodeType);
        Assert.Equal(1, boundary.Step);
        Assert.Equal(42L, boundary.Inputs["count"]);
        Assert.Equal(0, boundary.FrameDepth);
        var getExecutionCountResult = executor.GetExecutionCount(node.Id);
        Assert.Equal(0, getExecutionCountResult);
        Assert.Equal(node.Id, gate.CurrentBoundary?.NodeId);
        var tryContinueResult = gate.TryContinue();
        Assert.True(tryContinueResult);
        var fromSecondsResult1 = TimeSpan.FromSeconds(2);
        var result = await runTask.WaitAsync(fromSecondsResult1);
        Assert.True(result.IsSuccess);
        var getExecutionCountResult2 = executor.GetExecutionCount(node.Id);
        Assert.Equal(1, getExecutionCountResult2);
        Assert.Null(gate.CurrentBoundary);
    }

    [Fact]
    public async Task StepExecutesThePausedNodeOnceAndPausesBeforeTheNextNode()
    {
        var first = NodeDefinition.Create("first", NodeType.Action, "First");
        var second = NodeDefinition.Create("second", NodeType.Action, "Second");
        var connection = ConnectionDefinition.Execution(first.Id, "success", second.Id, "execute", ExecutionBranch.Success);
        var definition = CreateDefinition([first, second], [connection], first.Id);
        var firstPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new DebugExecutionGate([first.Id], (boundary, _) =>
        {
            if (boundary.NodeId == first.Id)
            {
                firstPause.TrySetResult(boundary);
            }
            else if (boundary.NodeId == second.Id)
            {
                secondPause.TrySetResult(boundary);
            }

            return ValueTask.CompletedTask;
        });
        var executor = new CountingActionExecutor();
        var runner = CreateRunner(executor, gate);
        await using var session = new FlowExecutionSession();
        var runAsyncResult3 = runner.RunAsync(definition, session);
        var runTask = runAsyncResult3.AsTask();
        var fromSecondsResult4 = TimeSpan.FromSeconds(2);
        await firstPause.Task.WaitAsync(fromSecondsResult4);
        var tryStepResult = gate.TryStep();
        Assert.True(tryStepResult);
        var fromSecondsResult5 = TimeSpan.FromSeconds(2);
        var nextBoundary = await secondPause.Task.WaitAsync(fromSecondsResult5);
        Assert.Equal(second.Id, nextBoundary.NodeId);
        var getExecutionCountResult6 = executor.GetExecutionCount(first.Id);
        Assert.Equal(1, getExecutionCountResult6);
        var getExecutionCountResult7 = executor.GetExecutionCount(second.Id);
        Assert.Equal(0, getExecutionCountResult7);
        var tryContinueResult8 = gate.TryContinue();
        Assert.True(tryContinueResult8);
        var fromSecondsResult9 = TimeSpan.FromSeconds(2);
        var result = await runTask.WaitAsync(fromSecondsResult9);
        Assert.True(result.IsSuccess);
        var getExecutionCountResult10 = executor.GetExecutionCount(first.Id);
        Assert.Equal(1, getExecutionCountResult10);
        var getExecutionCountResult11 = executor.GetExecutionCount(second.Id);
        Assert.Equal(1, getExecutionCountResult11);
    }

    [Fact]
    public async Task GateCancellationCancelsTheSessionWithoutExecutingThePausedNode()
    {
        var node = NodeDefinition.Create("action", NodeType.Action, "Action");
        var definition = CreateDefinition([node], [], node.Id);
        var pause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new DebugExecutionGate([node.Id], (boundary, _) =>
        {
            pause.TrySetResult(boundary);
            return ValueTask.CompletedTask;
        });
        var executor = new CountingActionExecutor();
        var runner = CreateRunner(executor, gate);
        await using var session = new FlowExecutionSession();
        var runAsyncResult12 = runner.RunAsync(definition, session);
        var runTask = runAsyncResult12.AsTask();
        var fromSecondsResult13 = TimeSpan.FromSeconds(2);
        await pause.Task.WaitAsync(fromSecondsResult13);
        var tryCancelResult = gate.TryCancel();
        Assert.True(tryCancelResult);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
        Assert.True(session.CancellationToken.IsCancellationRequested);
        var getExecutionCountResult14 = executor.GetExecutionCount(node.Id);
        Assert.Equal(0, getExecutionCountResult14);
        Assert.Null(gate.CurrentBoundary);
    }

    [Fact]
    public async Task GlobalFlipflopKeepsReceivingTriggersWhileTheForegroundInvocationIsPaused()
    {
        var trigger = NodeDefinition.Create("trigger", NodeType.Flipflop, "Trigger");
        var action = NodeDefinition.Create("action", NodeType.Action, "Action");
        IEnumerable<ConnectionDefinition> evaluatedValue15 = [ConnectionDefinition.Execution(trigger.Id, "success", action.Id, "execute", ExecutionBranch.Success)];
        var definition = CreateDefinition([trigger, action], evaluatedValue15, string.Empty);
        var firstPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseCount = 0;
        var gate = new DebugExecutionGate([action.Id], (boundary, _) =>
        {
            if (Interlocked.Increment(ref pauseCount) == 1)
            {
                firstPause.TrySetResult(boundary);
            }
            else
            {
                secondPause.TrySetResult(boundary);
            }

            return ValueTask.CompletedTask;
        });
        var flipflop = new QueuedFlipflopExecutor();
        var executor = new CapturingTriggerActionExecutor();
        var publisher = new RecordingPublisher();
        var executionPlanBuilder = new ExecutionPlanBuilder();
        var nodeExecutorRegistry = new NodeExecutorRegistry([flipflop, executor]);
        var debugInvocationScheduler16 = new DebugInvocationScheduler(maximumQueuedInvocations: 2);
        var runner = new FlowRunner(executionPlanBuilder, nodeExecutorRegistry, publisher, executionGate: gate, debugInvocationScheduler: debugInvocationScheduler16);
        await using var session = new FlowExecutionSession();
        var runAsyncResult17 = runner.RunAsync(definition, session);
        var runTask = runAsyncResult17.AsTask();
        await flipflop.WaitForWaitCountAsync(1);
        flipflop.Enqueue(10);
        var fromSecondsResult18 = TimeSpan.FromSeconds(2);
        var first = await firstPause.Task.WaitAsync(fromSecondsResult18);
        Assert.NotNull(first.InvocationId);
        Assert.Empty(executor.Values);
        AssertTriggerEventsUseInvocation(publisher.Events, trigger.Id, first.InvocationId.Value);
        await flipflop.WaitForWaitCountAsync(2);
        flipflop.Enqueue(20);
        await flipflop.WaitForWaitCountAsync(3);
        Assert.Empty(executor.Values);
        var tryContinueResult19 = gate.TryContinue();
        Assert.True(tryContinueResult19);
        var fromSecondsResult20 = TimeSpan.FromSeconds(2);
        var second = await secondPause.Task.WaitAsync(fromSecondsResult20);
        Assert.NotNull(second.InvocationId);
        Assert.NotEqual(first.InvocationId, second.InvocationId);
        AssertTriggerEventsUseInvocation(publisher.Events, trigger.Id, second.InvocationId.Value);
        Assert.Equal([10L], executor.Values);
        var tryContinueResult21 = gate.TryContinue();
        Assert.True(tryContinueResult21);
        await WaitUntilAsync(() => executor.Values.Length == 2);
        Assert.Equal([10L, 20L], executor.Values);
        session.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    private static FlowDefinition CreateDefinition(IEnumerable<NodeDefinition> nodes, IEnumerable<ConnectionDefinition> connections, string entryNodeId)
    {
        var newGuidResult = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue22 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, nodes, connections)];
        var createResult = FlowDefinition.Create(newGuidResult, 1, evaluatedValue22, entryNodeId);
        return createResult;
    }

    private static FlowRunner CreateRunner(CountingActionExecutor executor, DebugExecutionGate gate)
    {
        var executionPlanBuilder23 = new ExecutionPlanBuilder();
        var nodeExecutorRegistry24 = new NodeExecutorRegistry([executor]);
        FlowRunner evaluatedValue25 = new(executionPlanBuilder23, nodeExecutorRegistry24, executionGate: gate);
        return evaluatedValue25;
    }

    private static void AssertTriggerEventsUseInvocation(IReadOnlyCollection<RuntimeEvent> events, string nodeId, Guid invocationId)
    {
        var whereResult = events.Where(item => item.NodeId == nodeId && item.Type is "node.started" or "node.completed");
        var whereResult27 = whereResult.Where(item =>
        {
            var evaluatedValue26 = item.Payload.TryGetValue("triggerInvocationId", out var value) && value is Guid id && id == invocationId;
            return evaluatedValue26;
        });
        var matchingEvents = whereResult27.ToArray();
        Assert.Contains(matchingEvents, item => item.Type == "node.started");
        Assert.Contains(matchingEvents, item => item.Type == "node.completed");
    }

    private sealed class CountingActionExecutor : INodeExecutor
    {
        private readonly ConcurrentDictionary<string, int> _executionCounts = new(StringComparer.Ordinal);
        public NodeType NodeType => NodeType.Action;

        public int GetExecutionCount(string nodeId)
        {
            int evaluatedValue28 = _executionCounts.TryGetValue(nodeId, out var count) ? count : 0;
            return evaluatedValue28;
        }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            _executionCounts.AddOrUpdate(request.Node.Id, 1, static (_, count) => count + 1);
            var successResult = NodeExecutionResult.Success();
            var fromResultResult = ValueTask.FromResult(successResult);
            return fromResultResult;
        }
    }

    private sealed class CapturingTriggerActionExecutor : INodeExecutor
    {
        private readonly List<long> _values = [];
        private readonly object _sync = new();
        public NodeType NodeType => NodeType.Action;

        public long[] Values
        {
            get
            {
                lock (_sync)
                {
                    var toArrayResult1 = _values.ToArray();
                    return toArrayResult1;
                }
            }
        }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var readResult = request.Context.Read("trigger.data-out");
            var value = Assert.IsType<long>(readResult);
            lock (_sync)
            {
                _values.Add(value);
            }

            var successResult29 = NodeExecutionResult.Success();
            var fromResultResult30 = ValueTask.FromResult(successResult29);
            return fromResultResult30;
        }
    }

    private sealed class RecordingPublisher : IRunEventPublisher
    {
        private readonly ConcurrentQueue<RuntimeEvent> _events = new();
        public IReadOnlyCollection<RuntimeEvent> Events
        {
            get
            {
                var toArrayResult = _events.ToArray();
                return toArrayResult;
            }
        }

        public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            _events.Enqueue(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class QueuedFlipflopExecutor : INodeExecutor, IGlobalFlipflopExecutor
    {
        private readonly System.Threading.Channels.Channel<long> _triggers = System.Threading.Channels.Channel.CreateUnbounded<long>();
        private readonly TaskCompletionSource[] _waits = createwaitsInitialValue();
        private int _waitCount;
        public NodeType NodeType => NodeType.Flipflop;

        public void Enqueue(long value)
        {
            _triggers.Writer.TryWrite(value);
        }

        public async Task WaitForWaitCountAsync(int count)
        {
            while (Volatile.Read(ref _waitCount) < count)
            {
                var fromSecondsResult31 = TimeSpan.FromSeconds(2);
                await _waits[count - 1].Task.WaitAsync(fromSecondsResult31);
            }
        }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var waitForTriggerAsyncResult = WaitForTriggerAsync(request, cancellationToken);
            return waitForTriggerAsyncResult;
        }

        public async ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _waitCount) - 1;
            _waits[index].TrySetResult();
            var value = await _triggers.Reader.ReadAsync(cancellationToken);
            var dictionary = new Dictionary<string, object?>
            {
                ["result"] = value
            };
            var successResult32 = NodeExecutionResult.Success(dictionary);
            return successResult32;
        }

        private static TaskCompletionSource[] createwaitsInitialValue()
        {
            var rangeResult = Enumerable.Range(0, 8);
            var selectResult = rangeResult.Select(static _ =>
            {
                var taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return taskCompletionSource;
            });
            TaskCompletionSource[] waitsInitialValue = selectResult.ToArray();
            return waitsInitialValue;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected runtime condition was not reached. 未达到预期运行时条件。");
            }

            await Task.Delay(10);
        }
    }
}
