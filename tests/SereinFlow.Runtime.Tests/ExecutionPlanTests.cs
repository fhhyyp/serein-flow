using SereinFlow.Contracts;
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
            diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode && diagnostic.Path == "entryNodeId");
    }

    [Fact]
    public void BuilderAllowsAListenerOnlyFlowWithoutAnOrdinaryEntryNode()
    {
        var trigger = NodeDefinition.Create("trigger", NodeType.Flipflop, "Trigger");
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [trigger], [])],
            "");

        var plan = new ExecutionPlanBuilder().Build(definition);

        Assert.Equal(trigger.Id, Assert.Single(plan.Nodes).Key);
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

    [Fact]
    public void BuilderRejectsCrossFlowFlowCallTargets()
    {
        var target = NodeDefinition.Create("target", NodeType.Action, "Target");
        var call = NodeDefinition.Create(
            "call",
            NodeType.FlowCall,
            "Call",
            runtime: new NodeRuntimeDefinition(TargetNodeId: "target", TargetFlowId: Guid.NewGuid()));
        var flow = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])],
            "call");

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(flow));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.TargetFlowUnavailable);
    }

    [Fact]
    public void BuilderRejectsExecutionCyclesAsStaticDagViolation()
    {
        var first = NodeDefinition.Create("first", NodeType.Action, "First");
        var second = NodeDefinition.Create("second", NodeType.Action, "Second");
        var flow = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [first, second],
            [
                ConnectionDefinition.Execution("first", "out", "second", "in", ExecutionBranch.Success),
                ConnectionDefinition.Execution("second", "out", "first", "in", ExecutionBranch.Success),
            ])],
            "first");

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(flow));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowErrorCodes.CycleDetected);
    }

    [Fact]
    public void BuilderRejectsNonPublicFlowCallTargets()
    {
        var target = NodeDefinition.Create("target", NodeType.Action, "Target");
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

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(definition));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.TargetNotPublic);
    }

    [Fact]
    public void BuilderRejectsFlowCallTargetsOutsideTheConfiguredCanvas()
    {
        var target = NodeDefinition.Create(
            "target",
            NodeType.Action,
            "Target",
            runtime: new NodeRuntimeDefinition(IsPublic: true));
        var call = NodeDefinition.Create(
            "call",
            NodeType.FlowCall,
            "Call",
            runtime: new NodeRuntimeDefinition(TargetNodeId: target.Id, TargetCanvasId: "other"));
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])],
            call.Id);

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(definition));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.TargetCanvasMismatch);
    }

    [Fact]
    public void BuilderRejectsMissingRequiredFlowCallParameterBinding()
    {
        var target = NodeDefinition.Create(
            "target",
            NodeType.Action,
            "Target",
            parameters: [new NodeParameterDefinition("required", null, required: true, id: "target-required")],
            runtime: new NodeRuntimeDefinition(IsPublic: true));
        var call = NodeDefinition.Create(
            "call",
            NodeType.FlowCall,
            "Call",
            parameters: [new NodeParameterDefinition("other", "1", id: "call-other")],
            runtime: new NodeRuntimeDefinition(TargetNodeId: target.Id));
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])],
            call.Id);

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(definition));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.ParameterBindingMissing);
    }

    [Fact]
    public void BuilderRejectsDirectRecursiveFlowCall()
    {
        var call = NodeDefinition.Create(
            "call",
            NodeType.FlowCall,
            "Call",
            runtime: new NodeRuntimeDefinition(TargetNodeId: "call", IsPublic: true));
        var definition = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call], [])],
            call.Id);

        var exception = Assert.Throws<DomainValidationException>(() => new ExecutionPlanBuilder().Build(definition));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.CycleDetected);
    }
}
