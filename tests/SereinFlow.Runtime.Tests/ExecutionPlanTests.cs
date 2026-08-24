using SereinFlow.Domain;
using SereinFlow.Runtime;

namespace SereinFlow.Runtime.Tests;

public sealed class ExecutionPlanTests
{
    [Fact]
    public void BuilderRejectsInvalidFlowBeforeExecution()
    {
        var node = NodeDefinition.Create("node", NodeType.Action, "Node");
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [node],
            [ConnectionDefinition.Execution("node", "out", "missing", "in", ExecutionBranch.Success)])],
            "node");

        Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(definition));
    }

    [Fact]
    public void BuilderRejectsAnEmptyEditorFlowBeforeExecution()
    {
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [], [])],
            "");

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(definition));

        Assert.Contains(exception.Diagnostics, diagnostic =>
            diagnostic.Code == DomainErrorCodes.UnknownEntryNode && diagnostic.Path == "entryNodeId");
    }

    [Fact]
    public void BuilderOrdersOutgoingConnectionsByPriority()
    {
        var first = NodeDefinition.Create("first", NodeType.Action, "First");
        var second = NodeDefinition.Create("second", NodeType.Action, "Second");
        var third = NodeDefinition.Create("third", NodeType.Action, "Third");
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [first, second, third],
        [
            ConnectionDefinition.Execution("first", "out", "third", "in", ExecutionBranch.Success, priority: 2),
            ConnectionDefinition.Execution("first", "out", "second", "in", ExecutionBranch.Success, priority: 1)
        ]);
        var plan = new ExecutionPlanBuilder().Build(FlowDefinition.Create(Guid.NewGuid(), 1, [canvas], "first"));

        Assert.Equal("second", plan.GetOutgoing("first", ExecutionBranch.Success)[0].ToNodeId);
    }
}
