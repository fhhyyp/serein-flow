using SereinFlow.Contracts;
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

        var terminal = Assert.Single(publisher.Events, item => item.Type == NodeErrorCodes.Completed);
        var inputs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(terminal.Payload["inputs"]);

        Assert.Equal(42L, inputs["count"]);
    }

    [Fact]
    public async Task EachNodeExecutionGetsAStableIdentityInTheRequestAndEvents()
    {
        var node = NodeDefinition.Create("action", NodeType.Action, "Action");
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            "action");
        var executor = new CapturingExecutionIdentityExecutor();
        var publisher = new RecordingPublisher();
        var runner = new FlowRunner(
            new ExecutionPlanBuilder(),
            new NodeExecutorRegistry([executor]),
            publisher);

        await using var session = new FlowExecutionSession();
        await runner.RunAsync(definition, session);

        var started = Assert.Single(publisher.Events, item => item.Type == NodeErrorCodes.Started);
        var terminal = Assert.Single(publisher.Events, item => item.Type == NodeErrorCodes.Completed);
        var startedId = Assert.IsType<Guid>(started.Payload["executionId"]);
        var terminalId = Assert.IsType<Guid>(terminal.Payload["executionId"]);

        Assert.NotEqual(Guid.Empty, startedId);
        Assert.Equal(startedId, terminalId);
        Assert.Equal(startedId, executor.ExecutionId);
        Assert.Equal(1, executor.Step);
        Assert.Equal(startedId, executor.RuntimeExecutionId);
    }

    [Fact]
    public async Task RepeatedNodeStepsReceiveDifferentExecutionIds()
    {
        await using var session = new FlowExecutionSession();

        Assert.True(session.TryBeginStep("node-a", out var firstStep, out var firstId, out _));
        Assert.True(session.TryBeginStep("node-a", out var secondStep, out var secondId, out _));

        Assert.Equal(1, firstStep);
        Assert.Equal(2, secondStep);
        Assert.NotEqual(Guid.Empty, firstId);
        Assert.NotEqual(Guid.Empty, secondId);
        Assert.NotEqual(firstId, secondId);
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

        var terminal = Assert.Single(publisher.Events, item => item.Type == NodeErrorCodes.Failed);
        var inputs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(terminal.Payload["inputs"]);

        Assert.Equal(10L, inputs["first"]);
        Assert.DoesNotContain("second", inputs.Keys);
    }

    [Fact]
    public async Task EmptyOptionalLiteralIsOmittedSoTheWorkerCanApplyTheMethodDefault()
    {
        var parameter = new NodeParameterDefinition("retryCount", string.Empty, DataSource.Literal, required: false);
        var node = NodeDefinition.Create(
            "action",
            NodeType.Action,
            "Action",
            parameters: [parameter]);
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            node.Id);
        var plan = new ExecutionPlanBuilder().Build(definition);

        await using var session = new FlowExecutionSession();
        var inputs = new DataConnectionResolver().Resolve(node, plan, session);

        Assert.DoesNotContain(parameter.Id, inputs.Keys);
    }

    [Fact]
    public async Task DataConnectionReadsTheConnectedNodeOutputInsteadOfTheMostRecentlyExecutedNode()
    {
        var producer = NodeDefinition.Create("producer", NodeType.Action, "Producer");
        var consumer = NodeDefinition.Create(
            "consumer",
            NodeType.Action,
            "Consumer",
            parameters:
            [
                new NodeParameterDefinition(
                    "value",
                    null,
                    DataSource.PreviousNode,
                    sourceNodeId: "other",
                    sourcePortId: "data-out")
            ]);
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create(
                "main",
                CanvasLifecycle.Main,
                [producer, consumer],
                [ConnectionDefinition.Data(
                    producer.Id,
                    "data-out",
                    consumer.Id,
                    "value",
                    DataSource.PreviousNode)])],
            producer.Id);
        var plan = new ExecutionPlanBuilder().Build(definition);

        await using var session = new FlowExecutionSession();
        session.Write("producer.data-out", "connected-output");
        session.Write("other.data-out", "configured-fallback");

        var inputs = new DataConnectionResolver().Resolve(consumer, plan, session);

        Assert.Equal("connected-output", inputs["value"]);
    }

    [Fact]
    public async Task ProjectInputReadsTheValueSuppliedForTheCurrentRun()
    {
        var parameter = new NodeParameterDefinition(
            "userId",
            null,
            DataSource.ProjectInput,
            required: true,
            projectInputKey: "request.userId");
        var node = NodeDefinition.Create("consumer", NodeType.Action, "Consumer", parameters: [parameter]);
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            node.Id);
        var plan = new ExecutionPlanBuilder().Build(definition);

        await using var session = new FlowExecutionSession();
        session.Write("project.request.userId", 42L);

        var inputs = new DataConnectionResolver().Resolve(node, plan, session);

        Assert.Equal(42L, inputs[parameter.Id]);
    }

    [Fact]
    public async Task DynamicReferenceParsesJsonBeforeResolvingContextReferences()
    {
        var node = NodeDefinition.Create(
            "consumer",
            NodeType.Action,
            "Consumer",
            parameters:
            [
                new NodeParameterDefinition("decimal", null, DataSource.Expression, expression: "1.5"),
                new NodeParameterDefinition("quoted", null, DataSource.Expression, expression: "\"a.b\""),
                new NodeParameterDefinition("reference", null, DataSource.Expression, expression: "producer.data-out"),
                new NodeParameterDefinition("jsonNull", null, DataSource.Expression, expression: "null")
            ]);
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])],
            node.Id);
        var plan = new ExecutionPlanBuilder().Build(definition);

        await using var session = new FlowExecutionSession();
        session.Write("producer.data-out", "producer-output");

        var inputs = new DataConnectionResolver().Resolve(node, plan, session);

        Assert.Equal(1.5m, Assert.IsType<decimal>(inputs["decimal"]));
        Assert.Equal("a.b", inputs["quoted"]);
        Assert.Equal("producer-output", inputs["reference"]);
        Assert.Null(inputs["jsonNull"]);
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

        var startedIndex = publisher.Events.FindIndex(item => item.Type == NodeErrorCodes.Started);
        var logIndex = publisher.Events.FindIndex(item => item.Type == NodeErrorCodes.Log);
        var completedIndex = publisher.Events.FindIndex(item => item.Type == NodeErrorCodes.Completed);
        var log = Assert.Single(publisher.Events, item => item.Type == NodeErrorCodes.Log);

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
        Assert.Equal(FlowCallErrorCodes.ReturnTypeMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task FlowCallAcceptsAValueAssignableToTheStaticReturnType()
    {
        var target = NodeDefinition.Create(
            "target",
            NodeType.Action,
            "Target",
            runtime: new NodeRuntimeDefinition(
                IsPublic: true,
                ReturnType: typeof(BaseReturnValue).FullName));
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
            new NodeExecutorRegistry([new AssignableReturnActionExecutor(), new FlowCallNodeExecutor()]));

        await using var session = new FlowExecutionSession();
        var result = await runner.RunAsync(definition, session);

        Assert.True(result.IsSuccess);
        Assert.IsType<DerivedReturnValue>(result.Outputs["result"]);
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
        Assert.Contains(publisher.Events, item => item.Type == NodeErrorCodes.Started && item.NodeId == trigger.Id);
        Assert.DoesNotContain(publisher.Events, item => item.Type is NodeErrorCodes.Error or NodeErrorCodes.Failed);
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

    private sealed class CapturingExecutionIdentityExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public Guid ExecutionId { get; private set; }

        public int Step { get; private set; }

        public Guid RuntimeExecutionId { get; private set; }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            ExecutionId = request.ExecutionId;
            Step = request.Step;
            RuntimeExecutionId = request.Runtime?.Environment.ExecutionId ?? Guid.Empty;
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

    private sealed class AssignableReturnActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, object?> { ["result"] = new DerivedReturnValue() }));
    }

    private abstract class BaseReturnValue;

    private sealed class DerivedReturnValue : BaseReturnValue;

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
