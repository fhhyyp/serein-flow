using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime.Tests;

public sealed class RuntimeSessionTests
{
    [Fact]
    public async Task SessionsIsolateContextSequenceAndResourceCleanup()
    {
        var first = new FlowExecutionSession();
        var second = new FlowExecutionSession();
        var disposable = new TrackingDisposable();
        first.Resources.Register(disposable);
        first.Write("value", 42);

        Assert.Equal(42, first.Read("value"));
        Assert.Null(second.Read("value"));
        Assert.Equal(1, first.NextSequence());
        Assert.Equal(1, second.NextSequence());

        await first.DisposeAsync();
        await first.DisposeAsync();

        Assert.Equal(1, disposable.DisposeCount);
    }

    [Fact]
    public async Task CancellationIsIdempotentAndOwnedByTheSession()
    {
        await using var session = new FlowExecutionSession();

        Assert.True(session.Cancel());
        Assert.False(session.Cancel());
        Assert.True(session.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ConcurrentRunsDoNotShareContextValues()
    {
        var node = NodeDefinition.Create("action", NodeType.Action, "Action");
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            "action");
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([new ContextWritingExecutor()]));

        await using var first = new FlowExecutionSession();
        await using var second = new FlowExecutionSession();
        first.Write("run", "first");
        second.Write("run", "second");

        await Task.WhenAll(runner.RunAsync(definition, first).AsTask(), runner.RunAsync(definition, second).AsTask());

        Assert.Equal("first", first.Read("observed"));
        Assert.Equal("second", second.Read("observed"));
    }

    [Fact]
    public async Task TerminalEventRetainsResolvedInputsForTraceability()
    {
        var node = NodeDefinition.Create(
            "action",
            NodeType.Action,
            "Action",
            parameters: [new NodeParameterDefinition("count", "42")]);
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            "action");
        var publisher = new RecordingPublisher();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([new ContextWritingExecutor()]),
            publisher);

        await using var session = new FlowExecutionSession();
        await runner.RunAsync(definition, session);

        var terminal = Assert.Single(publisher.Events, item => item.Type == "node.completed");
        var inputs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(terminal.Payload["inputs"]);

        Assert.Equal(42L, inputs["count"]);
    }

    [Fact]
    public async Task BindingFailureRetainsInputsResolvedBeforeTheMissingParameter()
    {
        var node = NodeDefinition.Create(
            "action",
            NodeType.Action,
            "Action",
            parameters:
            [
                new NodeParameterDefinition("first", "10"),
                new NodeParameterDefinition(
                    "second",
                    null,
                    DataSource.ProjectInput,
                    required: true,
                    projectInputKey: "second")
            ]);
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            "action");
        var publisher = new RecordingPublisher();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([new ContextWritingExecutor()]),
            publisher);

        await using var session = new FlowExecutionSession();
        await runner.RunAsync(definition, session);

        var terminal = Assert.Single(publisher.Events, item => item.Type == "node.failed");
        var inputs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(terminal.Payload["inputs"]);

        Assert.Equal(10L, inputs["first"]);
        Assert.DoesNotContain("second", inputs.Keys);
    }

    [Fact]
    public async Task NodeLogIsPublishedBetweenNodeStartAndCompletionWithRuntimeMetadata()
    {
        var node = NodeDefinition.Create("action", NodeType.Action, "Action");
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            node.Id);
        var publisher = new RecordingPublisher();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([new LoggingActionExecutor()]),
            publisher);

        await using var session = new FlowExecutionSession();
        await runner.RunAsync(definition, session);

        var startedIndex = publisher.Events.FindIndex(item => item.Type == "node.started");
        var logIndex = publisher.Events.FindIndex(item => item.Type == "node.log");
        var completedIndex = publisher.Events.FindIndex(item => item.Type == "node.completed");
        var log = Assert.Single(publisher.Events, item => item.Type == "node.log");

        Assert.True(startedIndex >= 0);
        Assert.True(startedIndex < logIndex);
        Assert.True(logIndex < completedIndex);
        Assert.Equal(node.Id, log.NodeId);
        Assert.Equal("info", log.Payload["level"]);
        Assert.Equal("Started", log.Payload["message"]);
        Assert.Equal(0, log.Payload["frameDepth"]);
        Assert.Equal(
            publisher.Events.Select(item => item.Sequence).OrderBy(sequence => sequence),
            publisher.Events.Select(item => item.Sequence));
    }

    [Fact]
    public async Task FlowCallUsesAnIsolatedFrameAndOnlyPassesExplicitBindings()
    {
        var target = NodeDefinition.Create(
            "target",
            NodeType.Action,
            "Target",
            parameters: [new NodeParameterDefinition("value", null, required: true, id: "target-value")],
            runtime: new NodeRuntimeDefinition(IsPublic: true, ReturnType: "System.Int64"));
        var call = NodeDefinition.Create(
            "call",
            NodeType.FlowCall,
            "Call",
            parameters: [new NodeParameterDefinition("value", "7", id: "call-value")],
            runtime: new NodeRuntimeDefinition(
                TargetNodeId: target.Id,
                FlowCallParameterBindings: [new FlowCallParameterBinding("call-value", "target-value")]));
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])],
            call.Id);
        var action = new CapturingActionExecutor();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([action, new FlowCallNodeExecutor()]));

        await using var session = new FlowExecutionSession();
        session.Write("call.result", "must-not-leak");
        var result = await runner.RunAsync(definition, session);

        Assert.True(result.IsSuccess);
        Assert.Equal(7L, action.Input);
        Assert.True(action.CallerOutputWasHidden);
        Assert.Equal(1, action.FrameDepth);
        Assert.Equal(7L, session.Read("call.result"));
        Assert.Null(session.Read("target.result"));
    }

    [Fact]
    public async Task FlowCallRejectsAValueThatDoesNotMatchTheStaticReturnType()
    {
        var target = NodeDefinition.Create(
            "target",
            NodeType.Action,
            "Target",
            runtime: new NodeRuntimeDefinition(IsPublic: true, ReturnType: "System.Int32"));
        var call = NodeDefinition.Create(
            "call",
            NodeType.FlowCall,
            "Call",
            runtime: new NodeRuntimeDefinition(TargetNodeId: target.Id));
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])],
            call.Id);
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([new MismatchedReturnActionExecutor(), new FlowCallNodeExecutor()]));

        await using var session = new FlowExecutionSession();
        var result = await runner.RunAsync(definition, session);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionBranch.Error, result.NextBranch);
        Assert.Equal("flowcall.return_type_mismatch", result.ErrorCode);
    }

    [Fact]
    public async Task CancellingAGlobalFlipflopPropagatesTheRunCancellationWithoutNodeError()
    {
        var trigger = NodeDefinition.Create("trigger", NodeType.Flipflop, "Trigger");
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [trigger], [])],
            "");
        var executor = new WaitingFlipflopExecutor();
        var publisher = new RecordingPublisher();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([executor]),
            publisher);
        using var cancellation = new CancellationTokenSource();
        await using var session = new FlowExecutionSession();

        var runTask = runner.RunAsync(definition, session, cancellation.Token).AsTask();
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
        Assert.Contains(publisher.Events, item => item.Type == "node.started" && item.NodeId == trigger.Id);
        Assert.DoesNotContain(publisher.Events, item => item.Type is "node.error" or "node.failed");
    }

    private sealed class ContextWritingExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            request.Context.Write("observed", request.Context.Read("run"));
            return ValueTask.FromResult(NodeExecutionResult.Success());
        }
    }

    private sealed class CapturingActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public object? Input { get; private set; }

        public bool CallerOutputWasHidden { get; private set; }

        public int FrameDepth { get; private set; }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Input = request.Inputs["target-value"];
            CallerOutputWasHidden = request.Context.Read("call.result") is null;
            FrameDepth = Assert.IsType<FlowExecutionSession>(request.Context).FrameDepth;
            return ValueTask.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, object?> { ["result"] = Input }));
        }
    }

    private sealed class LoggingActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Assert.NotNull(request.Runtime);
            await request.Runtime.EventSink.PublishLogAsync(
                new NodeExecutionLogEntry("info", "Started", "Started"),
                cancellationToken);
            return NodeExecutionResult.Success();
        }
    }

    private sealed class MismatchedReturnActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, object?> { ["result"] = "not-an-int" }));
    }

    private sealed class WaitingFlipflopExecutor : INodeExecutor, IGlobalFlipflopExecutor
    {
        public NodeType NodeType => NodeType.Flipflop;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
            => WaitForTriggerAsync(request, cancellationToken);

        public async ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return NodeExecutionResult.Success();
        }
    }

    private sealed class RecordingPublisher : IRunEventPublisher
    {
        public List<RuntimeEvent> Events { get; } = [];

        public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            Events.Add(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
