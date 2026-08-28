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
        var node = NodeDefinition.Create(
            "action",
            NodeType.Action,
            "Action",
            parameters: [new NodeParameterDefinition("count", "42")]);
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
        var runTask = runner.RunAsync(definition, session).AsTask();
        var boundary = await pause.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(node.Id, boundary.NodeId);
        Assert.Equal(NodeType.Action, boundary.NodeType);
        Assert.Equal(1, boundary.Step);
        Assert.Equal(42L, boundary.Inputs["count"]);
        Assert.Equal(0, boundary.FrameDepth);
        Assert.Equal(0, executor.GetExecutionCount(node.Id));
        Assert.Equal(node.Id, gate.CurrentBoundary?.NodeId);

        Assert.True(gate.TryContinue());
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, executor.GetExecutionCount(node.Id));
        Assert.Null(gate.CurrentBoundary);
    }

    [Fact]
    public async Task StepExecutesThePausedNodeOnceAndPausesBeforeTheNextNode()
    {
        var first = NodeDefinition.Create("first", NodeType.Action, "First");
        var second = NodeDefinition.Create("second", NodeType.Action, "Second");
        var connection = ConnectionDefinition.Execution(
            first.Id,
            "success",
            second.Id,
            "execute",
            ExecutionBranch.Success);
        var definition = CreateDefinition([first, second], [connection], first.Id);
        var firstPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new DebugExecutionGate([first.Id], (boundary, _) =>
        {
            if (boundary.NodeId == first.Id)
                firstPause.TrySetResult(boundary);
            else if (boundary.NodeId == second.Id)
                secondPause.TrySetResult(boundary);
            return ValueTask.CompletedTask;
        });
        var executor = new CountingActionExecutor();
        var runner = CreateRunner(executor, gate);

        await using var session = new FlowExecutionSession();
        var runTask = runner.RunAsync(definition, session).AsTask();
        await firstPause.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(gate.TryStep());
        var nextBoundary = await secondPause.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(second.Id, nextBoundary.NodeId);
        Assert.Equal(1, executor.GetExecutionCount(first.Id));
        Assert.Equal(0, executor.GetExecutionCount(second.Id));

        Assert.True(gate.TryContinue());
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, executor.GetExecutionCount(first.Id));
        Assert.Equal(1, executor.GetExecutionCount(second.Id));
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
        var runTask = runner.RunAsync(definition, session).AsTask();
        await pause.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(gate.TryCancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.True(session.CancellationToken.IsCancellationRequested);
        Assert.Equal(0, executor.GetExecutionCount(node.Id));
        Assert.Null(gate.CurrentBoundary);
    }

    [Fact]
    public async Task GlobalFlipflopKeepsReceivingTriggersWhileTheForegroundInvocationIsPaused()
    {
        var trigger = NodeDefinition.Create("trigger", NodeType.Flipflop, "Trigger");
        var action = NodeDefinition.Create("action", NodeType.Action, "Action");
        var definition = CreateDefinition(
            [trigger, action],
            [ConnectionDefinition.Execution(trigger.Id, "success", action.Id, "execute", ExecutionBranch.Success)],
            string.Empty);
        var firstPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPause = new TaskCompletionSource<NodeExecutionBoundary>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseCount = 0;
        var gate = new DebugExecutionGate([action.Id], (boundary, _) =>
        {
            if (Interlocked.Increment(ref pauseCount) == 1)
                firstPause.TrySetResult(boundary);
            else
                secondPause.TrySetResult(boundary);
            return ValueTask.CompletedTask;
        });
        var flipflop = new QueuedFlipflopExecutor();
        var executor = new CapturingTriggerActionExecutor();
        var publisher = new RecordingPublisher();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([flipflop, executor]),
            publisher,
            executionGate: gate,
            debugInvocationScheduler: new DebugInvocationScheduler(maximumQueuedInvocations: 2));

        await using var session = new FlowExecutionSession();
        var runTask = runner.RunAsync(definition, session).AsTask();
        await flipflop.WaitForWaitCountAsync(1);

        flipflop.Enqueue(10);
        var first = await firstPause.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(first.InvocationId);
        Assert.Empty(executor.Values);
        AssertTriggerEventsUseInvocation(publisher.Events, trigger.Id, first.InvocationId.Value);

        await flipflop.WaitForWaitCountAsync(2);
        flipflop.Enqueue(20);
        await flipflop.WaitForWaitCountAsync(3);
        Assert.Empty(executor.Values);

        Assert.True(gate.TryContinue());
        var second = await secondPause.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(second.InvocationId);
        Assert.NotEqual(first.InvocationId, second.InvocationId);
        AssertTriggerEventsUseInvocation(publisher.Events, trigger.Id, second.InvocationId.Value);
        Assert.Equal([10L], executor.Values);

        Assert.True(gate.TryContinue());
        await WaitUntilAsync(() => executor.Values.Length == 2);
        Assert.Equal([10L, 20L], executor.Values);

        session.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    private static FlowDefinition CreateDefinition(
        IEnumerable<NodeDefinition> nodes,
        IEnumerable<ConnectionDefinition> connections,
        string entryNodeId)
        => FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, nodes, connections)],
            entryNodeId);

    private static FlowRunner CreateRunner(CountingActionExecutor executor, DebugExecutionGate gate)
        => new(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([executor]),
            executionGate: gate);

    private static void AssertTriggerEventsUseInvocation(
        IReadOnlyCollection<RuntimeEvent> events,
        string nodeId,
        Guid invocationId)
    {
        var matchingEvents = events
            .Where(item => item.NodeId == nodeId && item.Type is "node.started" or "node.completed")
            .Where(item => item.Payload.TryGetValue("triggerInvocationId", out var value) && value is Guid id && id == invocationId)
            .ToArray();

        Assert.Contains(matchingEvents, item => item.Type == "node.started");
        Assert.Contains(matchingEvents, item => item.Type == "node.completed");
    }

    private sealed class CountingActionExecutor : INodeExecutor
    {
        private readonly ConcurrentDictionary<string, int> _executionCounts = new(StringComparer.Ordinal);

        public NodeType NodeType => NodeType.Action;

        public int GetExecutionCount(string nodeId)
            => _executionCounts.TryGetValue(nodeId, out var count) ? count : 0;

        public ValueTask<NodeExecutionResult> ExecuteAsync(
            NodeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            _executionCounts.AddOrUpdate(request.Node.Id, 1, static (_, count) => count + 1);
            return ValueTask.FromResult(NodeExecutionResult.Success());
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
                    return _values.ToArray();
            }
        }

        public ValueTask<NodeExecutionResult> ExecuteAsync(
            NodeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            var value = Assert.IsType<long>(request.Context.Read("trigger.data-out"));
            lock (_sync)
                _values.Add(value);
            return ValueTask.FromResult(NodeExecutionResult.Success());
        }
    }

    private sealed class RecordingPublisher : IRunEventPublisher
    {
        private readonly ConcurrentQueue<RuntimeEvent> _events = new();

        public IReadOnlyCollection<RuntimeEvent> Events => _events.ToArray();

        public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            _events.Enqueue(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class QueuedFlipflopExecutor : INodeExecutor, IGlobalFlipflopExecutor
    {
        private readonly System.Threading.Channels.Channel<long> _triggers = System.Threading.Channels.Channel.CreateUnbounded<long>();
        private readonly TaskCompletionSource[] _waits = Enumerable.Range(0, 8)
            .Select(static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        private int _waitCount;

        public NodeType NodeType => NodeType.Flipflop;

        public void Enqueue(long value) => _triggers.Writer.TryWrite(value);

        public async Task WaitForWaitCountAsync(int count)
        {
            while (Volatile.Read(ref _waitCount) < count)
                await _waits[count - 1].Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
            => WaitForTriggerAsync(request, cancellationToken);

        public async ValueTask<NodeExecutionResult> WaitForTriggerAsync(
            NodeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _waitCount) - 1;
            _waits[index].TrySetResult();
            var value = await _triggers.Reader.ReadAsync(cancellationToken);
            return NodeExecutionResult.Success(new Dictionary<string, object?> { ["result"] = value });
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("The expected runtime condition was not reached. 未达到预期运行时条件。");
            await Task.Delay(10);
        }
    }
}
