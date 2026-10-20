using System.Collections.Generic;
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
        first.Variables.Set("value", 42);
        var readResult = first.Read("value");
        Assert.Equal(42, readResult);
        var readResult1 = second.Read("value");
        Assert.Null(readResult1);
        var getResult = first.Variables.Get<int>("value");
        Assert.Equal(42, getResult);
        var containResult = second.Variables.Contain("value");
        Assert.False(containResult);
        var nextSequenceResult = first.NextSequence();
        Assert.Equal(1, nextSequenceResult);
        var nextSequenceResult2 = second.NextSequence();
        Assert.Equal(1, nextSequenceResult2);
        await first.DisposeAsync();
        await first.DisposeAsync();
        Assert.Equal(1, disposable.DisposeCount);
    }

    [Fact]
    public async Task CancellationIsIdempotentAndOwnedByTheSession()
    {
        await using var session = new FlowExecutionSession();
        var cancelResult = session.Cancel();
        Assert.True(cancelResult);
        var cancelResult3 = session.Cancel();
        Assert.False(cancelResult3);
        Assert.True(session.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ConcurrentRunsDoNotShareContextValues()
    {
        var node = NodeDefinition.Create("action", NodeType.Action, "Action");
        var newGuidResult = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult, 1, evaluatedValue, "action");
        var executionPlanBuilder = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue4 = [new ContextWritingExecutor()];
        var nodeExecutorRegistry = new NodeExecutorRegistry(evaluatedValue4);
        var runner = new FlowRunner(executionPlanBuilder, nodeExecutorRegistry);
        await using var first = new FlowExecutionSession();
        await using var second = new FlowExecutionSession();
        first.Write("run", "first");
        second.Write("run", "second");
        var runAsyncResult = runner.RunAsync(definition, first);
        var asTaskResult = runAsyncResult.AsTask();
        var runAsyncResult5 = runner.RunAsync(definition, second);
        var asTaskResult6 = runAsyncResult5.AsTask();
        await Task.WhenAll(asTaskResult, asTaskResult6);
        var readResult7 = first.Read("observed");
        Assert.Equal("first", readResult7);
        var readResult8 = second.Read("observed");
        Assert.Equal("second", readResult8);
    }

    [Fact]
    public async Task TerminalEventRetainsResolvedInputsForTraceability()
    {
        IEnumerable<NodeParameterDefinition> evaluatedValue9 = [new NodeParameterDefinition("count", "42")];
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: evaluatedValue9);
        var newGuidResult10 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue11 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult10, 1, evaluatedValue11, "action");
        var publisher = new RecordingPublisher();
        var executionPlanBuilder12 = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue13 = [new ContextWritingExecutor()];
        var nodeExecutorRegistry14 = new NodeExecutorRegistry(evaluatedValue13);
        var runner = new FlowRunner(executionPlanBuilder12, nodeExecutorRegistry14, publisher);
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
        var newGuidResult15 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue16 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult15, 1, evaluatedValue16, "action");
        var executor = new CapturingExecutionIdentityExecutor();
        var publisher = new RecordingPublisher();
        var executionPlanBuilder17 = new ExecutionPlanBuilder();
        var nodeExecutorRegistry18 = new NodeExecutorRegistry([executor]);
        var runner = new FlowRunner(executionPlanBuilder17, nodeExecutorRegistry18, publisher);
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
        var tryBeginStepResult = session.TryBeginStep("node-a", out var firstStep, out var firstId, out _);
        Assert.True(tryBeginStepResult);
        var tryBeginStepResult19 = session.TryBeginStep("node-a", out var secondStep, out var secondId, out _);
        Assert.True(tryBeginStepResult19);
        Assert.Equal(1, firstStep);
        Assert.Equal(2, secondStep);
        Assert.NotEqual(Guid.Empty, firstId);
        Assert.NotEqual(Guid.Empty, secondId);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task BindingFailureRetainsInputsResolvedBeforeTheMissingParameter()
    {
        IEnumerable<NodeParameterDefinition> evaluatedValue20 = [new NodeParameterDefinition("first", "10"), new NodeParameterDefinition("second", null, DataSource.ProjectInput, required: true, projectInputKey: "second")];
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: evaluatedValue20);
        var newGuidResult21 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue22 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult21, 1, evaluatedValue22, "action");
        var publisher = new RecordingPublisher();
        var executionPlanBuilder23 = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue24 = [new ContextWritingExecutor()];
        var nodeExecutorRegistry25 = new NodeExecutorRegistry(evaluatedValue24);
        var runner = new FlowRunner(executionPlanBuilder23, nodeExecutorRegistry25, publisher);
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
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: [parameter]);
        var newGuidResult26 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue27 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult26, 1, evaluatedValue27, node.Id);
        var executionPlanBuilder28 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder28.Build(definition);
        await using var session = new FlowExecutionSession();
        var dataConnectionResolver = new DataConnectionResolver();
        var inputs = dataConnectionResolver.Resolve(node, plan, session);
        Assert.DoesNotContain(parameter.Id, inputs.Keys);
    }

    [Fact]
    public async Task WhitespaceOptionalLiteralIsOmittedSoTheWorkerCanApplyTheMethodDefault()
    {
        var parameter = new NodeParameterDefinition("retryCount", "  ", DataSource.Literal, required: false);
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: [parameter]);
        var newGuidResult29 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue30 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult29, 1, evaluatedValue30, node.Id);
        var executionPlanBuilder31 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder31.Build(definition);
        await using var session = new FlowExecutionSession();
        var dataConnectionResolver32 = new DataConnectionResolver();
        var inputs = dataConnectionResolver32.Resolve(node, plan, session);
        Assert.DoesNotContain(parameter.Id, inputs.Keys);
    }

    [Fact]
    public async Task EmptyStringLiteralRemainsAnExplicitEmptyString()
    {
        var parameter = new NodeParameterDefinition("value", "\"\"", DataSource.Literal, required: false);
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: [parameter]);
        var newGuidResult33 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue34 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult33, 1, evaluatedValue34, node.Id);
        var executionPlanBuilder35 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder35.Build(definition);
        await using var session = new FlowExecutionSession();
        var dataConnectionResolver36 = new DataConnectionResolver();
        var inputs = dataConnectionResolver36.Resolve(node, plan, session);
        Assert.Equal(string.Empty, inputs[parameter.Id]);
    }

    [Fact]
    public async Task DataConnectionReadsTheConnectedNodeOutputInsteadOfTheMostRecentlyExecutedNode()
    {
        var producer = NodeDefinition.Create("producer", NodeType.Action, "Producer");
        IEnumerable<NodeParameterDefinition> evaluatedValue37 = [new NodeParameterDefinition("value", null, DataSource.PreviousNode, sourceNodeId: "other", sourcePortId: "data-out")];
        var consumer = NodeDefinition.Create("consumer", NodeType.Action, "Consumer", parameters: evaluatedValue37);
        var newGuidResult38 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue40 = [resolveCreateResult()];
        var definition = FlowDefinition.Create(newGuidResult38, 1, evaluatedValue40, producer.Id);
        var executionPlanBuilder41 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder41.Build(definition);
        await using var session = new FlowExecutionSession();
        session.Write("producer.data-out", "connected-output");
        session.Write("other.data-out", "configured-fallback");
        var dataConnectionResolver42 = new DataConnectionResolver();
        var inputs = dataConnectionResolver42.Resolve(consumer, plan, session);
        Assert.Equal("connected-output", inputs["value"]);
        CanvasDefinition resolveCreateResult()
        {
            IEnumerable<ConnectionDefinition> evaluatedValue39 = [ConnectionDefinition.Data(producer.Id, "data-out", consumer.Id, "value", DataSource.PreviousNode)];
            var createResult = CanvasDefinition.Create("main", CanvasLifecycle.Main, [producer, consumer], evaluatedValue39);
            return createResult;
        }
    }

    [Fact]
    public async Task ProjectInputReadsTheValueSuppliedForTheCurrentRun()
    {
        var parameter = new NodeParameterDefinition("userId", null, DataSource.ProjectInput, required: true, projectInputKey: "request.userId");
        var node = NodeDefinition.Create("consumer", NodeType.Action, "Consumer", parameters: [parameter]);
        var newGuidResult43 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue44 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult43, 1, evaluatedValue44, node.Id);
        var executionPlanBuilder45 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder45.Build(definition);
        await using var session = new FlowExecutionSession();
        session.Write("project.request.userId", 42L);
        var dataConnectionResolver46 = new DataConnectionResolver();
        var inputs = dataConnectionResolver46.Resolve(node, plan, session);
        Assert.Equal(42L, inputs[parameter.Id]);
    }

    [Fact]
    public async Task DynamicReferenceParsesJsonBeforeResolvingContextReferences()
    {
        IEnumerable<NodeParameterDefinition> evaluatedValue47 = [new NodeParameterDefinition("decimal", null, DataSource.Expression, expression: "1.5"), new NodeParameterDefinition("quoted", null, DataSource.Expression, expression: "\"a.b\""), new NodeParameterDefinition("reference", null, DataSource.Expression, expression: "producer.data-out"), new NodeParameterDefinition("jsonNull", null, DataSource.Expression, expression: "null")];
        var node = NodeDefinition.Create("consumer", NodeType.Action, "Consumer", parameters: evaluatedValue47);
        var newGuidResult48 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue49 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult48, 1, evaluatedValue49, node.Id);
        var executionPlanBuilder50 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder50.Build(definition);
        await using var session = new FlowExecutionSession();
        session.Write("producer.data-out", "producer-output");
        var dataConnectionResolver51 = new DataConnectionResolver();
        var inputs = dataConnectionResolver51.Resolve(node, plan, session);
        var isTypeResult = Assert.IsType<decimal>(inputs["decimal"]);
        Assert.Equal(1.5m, isTypeResult);
        Assert.Equal("a.b", inputs["quoted"]);
        Assert.Equal("producer-output", inputs["reference"]);
        Assert.Null(inputs["jsonNull"]);
    }

    [Fact]
    public async Task NodeLogIsPublishedBetweenNodeStartAndCompletionWithRuntimeMetadata()
    {
        var node = NodeDefinition.Create("action", NodeType.Action, "Action");
        var newGuidResult52 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue53 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], [])];
        var definition = FlowDefinition.Create(newGuidResult52, 1, evaluatedValue53, node.Id);
        var publisher = new RecordingPublisher();
        var executionPlanBuilder54 = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue55 = [new LoggingActionExecutor()];
        var nodeExecutorRegistry56 = new NodeExecutorRegistry(evaluatedValue55);
        var runner = new FlowRunner(executionPlanBuilder54, nodeExecutorRegistry56, publisher);
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
        var orderByResult = publisher.Events
            .Select(item => item.Sequence)
            .OrderBy(sequence => sequence);
        var selectResult57 = publisher.Events.Select(item => item.Sequence);
        Assert.Equal(orderByResult, selectResult57);
    }

    [Fact]
    public async Task FlowCallUsesAnIsolatedFrameAndOnlyPassesExplicitBindings()
    {
        IEnumerable<NodeParameterDefinition> evaluatedValue58 = [new NodeParameterDefinition("value", null, required: true, id: "target-value")];
        var nodeRuntimeDefinition = new NodeRuntimeDefinition(IsPublic: true, ReturnType: "System.Int64");
        var target = NodeDefinition.Create("target", NodeType.Action, "Target", parameters: evaluatedValue58, runtime: nodeRuntimeDefinition);
        IEnumerable<NodeParameterDefinition> evaluatedValue59 = [new NodeParameterDefinition("value", "7", id: "call-value")];
        IReadOnlyList<FlowCallParameterBinding> evaluatedValue60 = [new FlowCallParameterBinding("call-value", "target-value")];
        var nodeRuntimeDefinition61 = new NodeRuntimeDefinition(TargetNodeId: target.Id, FlowCallParameterBindings: evaluatedValue60);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", parameters: evaluatedValue59, runtime: nodeRuntimeDefinition61);
        var newGuidResult62 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue63 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var definition = FlowDefinition.Create(newGuidResult62, 1, evaluatedValue63, call.Id);
        var action = new CapturingActionExecutor();
        var executionPlanBuilder64 = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue65 = [action, new FlowCallNodeExecutor()];
        var nodeExecutorRegistry66 = new NodeExecutorRegistry(evaluatedValue65);
        var runner = new FlowRunner(executionPlanBuilder64, nodeExecutorRegistry66);
        await using var session = new FlowExecutionSession();
        session.Write("call.result", "must-not-leak");
        session.Variables.Set("shared", 42);
        var result = await runner.RunAsync(definition, session);
        Assert.True(result.IsSuccess);
        Assert.Equal(7L, action.Input);
        Assert.True(action.CallerOutputWasHidden);
        Assert.Equal(1, action.FrameDepth);
        var readResult67 = session.Read("call.result");
        Assert.Equal(7L, readResult67);
        var readResult68 = session.Read("target.result");
        Assert.Null(readResult68);
        Assert.Same(session.Variables, action.Variables);
        var getResult69 = session.Variables.Get<int>("shared");
        Assert.Equal(43, getResult69);
    }

    [Fact]
    public async Task FlowCallRejectsAValueThatDoesNotMatchTheStaticReturnType()
    {
        var nodeRuntimeDefinition70 = new NodeRuntimeDefinition(IsPublic: true, ReturnType: "System.Int32");
        var target = NodeDefinition.Create("target", NodeType.Action, "Target", runtime: nodeRuntimeDefinition70);
        var nodeRuntimeDefinition71 = new NodeRuntimeDefinition(TargetNodeId: target.Id);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", runtime: nodeRuntimeDefinition71);
        var newGuidResult72 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue73 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var definition = FlowDefinition.Create(newGuidResult72, 1, evaluatedValue73, call.Id);
        var executionPlanBuilder74 = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue75 = [new MismatchedReturnActionExecutor(), new FlowCallNodeExecutor()];
        var nodeExecutorRegistry76 = new NodeExecutorRegistry(evaluatedValue75);
        var runner = new FlowRunner(executionPlanBuilder74, nodeExecutorRegistry76);
        await using var session = new FlowExecutionSession();
        var result = await runner.RunAsync(definition, session);
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionBranch.Error, result.NextBranch);
        Assert.Equal(FlowCallErrorCodes.ReturnTypeMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task FlowCallAcceptsAValueAssignableToTheStaticReturnType()
    {
        var nodeRuntimeDefinition77 = new NodeRuntimeDefinition(IsPublic: true, ReturnType: typeof(BaseReturnValue).FullName);
        var target = NodeDefinition.Create("target", NodeType.Action, "Target", runtime: nodeRuntimeDefinition77);
        var nodeRuntimeDefinition78 = new NodeRuntimeDefinition(TargetNodeId: target.Id);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", runtime: nodeRuntimeDefinition78);
        var newGuidResult79 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue80 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var definition = FlowDefinition.Create(newGuidResult79, 1, evaluatedValue80, call.Id);
        var executionPlanBuilder81 = new ExecutionPlanBuilder();
        IEnumerable<INodeExecutor> evaluatedValue82 = [new AssignableReturnActionExecutor(), new FlowCallNodeExecutor()];
        var nodeExecutorRegistry83 = new NodeExecutorRegistry(evaluatedValue82);
        var runner = new FlowRunner(executionPlanBuilder81, nodeExecutorRegistry83);
        await using var session = new FlowExecutionSession();
        var result = await runner.RunAsync(definition, session);
        Assert.True(result.IsSuccess);
        Assert.IsType<DerivedReturnValue>(result.Outputs["result"]);
    }

    [Fact]
    public async Task CancellingAGlobalFlipflopPropagatesTheRunCancellationWithoutNodeError()
    {
        var trigger = NodeDefinition.Create("trigger", NodeType.Flipflop, "Trigger");
        var newGuidResult84 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue85 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [trigger], [])];
        var definition = FlowDefinition.Create(newGuidResult84, 1, evaluatedValue85, "");
        var executor = new WaitingFlipflopExecutor();
        var publisher = new RecordingPublisher();
        var executionPlanBuilder86 = new ExecutionPlanBuilder();
        var nodeExecutorRegistry87 = new NodeExecutorRegistry([executor]);
        var runner = new FlowRunner(executionPlanBuilder86, nodeExecutorRegistry87, publisher);
        using var cancellation = new CancellationTokenSource();
        await using var session = new FlowExecutionSession();
        var runAsyncResult88 = runner.RunAsync(definition, session, cancellation.Token);
        var runTask = runAsyncResult88.AsTask();
        var fromSecondsResult = TimeSpan.FromSeconds(2);
        await executor.Started.Task.WaitAsync(fromSecondsResult);
        Assert.Same(session.Variables, executor.Variables);
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
            var readResult89 = request.Context.Read("run");
            request.Context.Write("observed", readResult89);
            var successResult = NodeExecutionResult.Success();
            var fromResultResult = ValueTask.FromResult(successResult);
            return fromResultResult;
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
            var successResult90 = NodeExecutionResult.Success();
            var fromResultResult91 = ValueTask.FromResult(successResult90);
            return fromResultResult91;
        }
    }

    private sealed class CapturingActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;
        public object? Input { get; private set; }
        public bool CallerOutputWasHidden { get; private set; }
        public int FrameDepth { get; private set; }
        public SereinFlow.Library.LibraryVariableService? Variables { get; private set; }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Input = request.Inputs["target-value"];
            CallerOutputWasHidden = request.Context.Read("call.result") is null;
            FrameDepth = Assert.IsType<FlowExecutionSession>(request.Context).FrameDepth;
            Variables = Assert.IsType<FlowExecutionSession>(request.Context).Variables;
            Assert.Same(Variables, request.Runtime!.Variables);
            var evaluatedValue92 = Variables.Get<int>("shared") + 1;
            Variables.Set("shared", evaluatedValue92);
            var dictionary = new Dictionary<string, object?>
            {
                ["result"] = Input
            };
            var successResult93 = NodeExecutionResult.Success(dictionary);
            var fromResultResult94 = ValueTask.FromResult(successResult93);
            return fromResultResult94;
        }
    }

    private sealed class LoggingActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Assert.NotNull(request.Runtime);
            var nodeExecutionLogEntry = new NodeExecutionLogEntry("info", "Started", "Started");
            await request.Runtime.EventSink.PublishLogAsync(nodeExecutionLogEntry, cancellationToken);
            var successResult95 = NodeExecutionResult.Success();
            return successResult95;
        }
    }

    private sealed class MismatchedReturnActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var dictionary96 = new Dictionary<string, object?>
            {
                ["result"] = "not-an-int"
            };
            var successResult97 = NodeExecutionResult.Success(dictionary96);
            var fromResultResult98 = ValueTask.FromResult(successResult97);
            return fromResultResult98;
        }
    }

    private sealed class AssignableReturnActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var dictionary99 = new Dictionary<string, object?>
            {
                ["result"] = new DerivedReturnValue()
            };
            var successResult100 = NodeExecutionResult.Success(dictionary99);
            var fromResultResult101 = ValueTask.FromResult(successResult100);
            return fromResultResult101;
        }
    }

    private abstract class BaseReturnValue;
    private sealed class DerivedReturnValue : BaseReturnValue;
    private sealed class WaitingFlipflopExecutor : INodeExecutor, IGlobalFlipflopExecutor
    {
        public NodeType NodeType => NodeType.Flipflop;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SereinFlow.Library.LibraryVariableService? Variables { get; private set; }

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var waitForTriggerAsyncResult = WaitForTriggerAsync(request, cancellationToken);
            return waitForTriggerAsyncResult;
        }

        public async ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Variables = Assert.IsType<FlowExecutionSession>(request.Context).Variables;
            Assert.Same(Variables, request.Runtime!.Variables);
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var successResult102 = NodeExecutionResult.Success();
            return successResult102;
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
