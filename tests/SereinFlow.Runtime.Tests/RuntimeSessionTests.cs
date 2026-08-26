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

    private sealed class ContextWritingExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            request.Context.Write("observed", request.Context.Read("run"));
            return ValueTask.FromResult(NodeExecutionResult.Success());
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
