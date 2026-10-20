using System.Collections.Generic;
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
        var newGuidResult = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue1 = [resolveCreateResult()];
        var definition = FlowDefinition.Create(newGuidResult, 1, evaluatedValue1, "node");
        Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder = new ExecutionPlanBuilder();
            var buildResult = executionPlanBuilder.Build(definition);
            return buildResult;
        });
        CanvasDefinition resolveCreateResult()
        {
            IEnumerable<ConnectionDefinition> evaluatedValue = [ConnectionDefinition.Execution("node", "out", "missing", "in", ExecutionBranch.Success)];
            var createResult = CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], evaluatedValue);
            return createResult;
        }
    }

    [Fact]
    public void BuilderRejectsAnEmptyEditorFlowBeforeExecution()
    {
        var newGuidResult4 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue5 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [], [])];
        var definition = FlowDefinition.Create(newGuidResult4, 1, evaluatedValue5, "");
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder2 = new ExecutionPlanBuilder();
            var buildResult3 = executionPlanBuilder2.Build(definition);
            return buildResult3;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode && diagnostic.Path == "entryNodeId");
    }

    [Fact]
    public void BuilderAllowsAListenerOnlyFlowWithoutAnOrdinaryEntryNode()
    {
        var trigger = NodeDefinition.Create("trigger", NodeType.Flipflop, "Trigger");
        var newGuidResult6 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue7 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [trigger], [])];
        var definition = FlowDefinition.Create(newGuidResult6, 1, evaluatedValue7, "");
        var executionPlanBuilder8 = new ExecutionPlanBuilder();
        var plan = executionPlanBuilder8.Build(definition);
        var evaluatedValue9 = Assert.Single(plan.Nodes).Key;
        Assert.Equal(trigger.Id, evaluatedValue9);
    }

    [Fact]
    public void BuilderOrdersOutgoingConnectionsByPriority()
    {
        var first = NodeDefinition.Create("first", NodeType.Action, "First");
        var second = NodeDefinition.Create("second", NodeType.Action, "Second");
        var third = NodeDefinition.Create("third", NodeType.Action, "Third");
        IEnumerable<ConnectionDefinition> evaluatedValue10 = [ConnectionDefinition.Execution("first", "out", "third", "in", ExecutionBranch.Success, priority: 2), ConnectionDefinition.Execution("first", "out", "second", "in", ExecutionBranch.Success, priority: 1)];
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [first, second, third], evaluatedValue10);
        var executionPlanBuilder11 = new ExecutionPlanBuilder();
        var newGuidResult12 = Guid.NewGuid();
        var createResult13 = FlowDefinition.Create(newGuidResult12, 1, [canvas], "first");
        var plan = executionPlanBuilder11.Build(createResult13);
        var evaluatedValue14 = plan.GetOutgoing("first", ExecutionBranch.Success)[0].ToNodeId;
        Assert.Equal("second", evaluatedValue14);
    }

    [Fact]
    public void BuilderRejectsCrossFlowFlowCallTargets()
    {
        var target = NodeDefinition.Create("target", NodeType.Action, "Target");
        var newGuidResult17 = Guid.NewGuid();
        var nodeRuntimeDefinition = new NodeRuntimeDefinition(TargetNodeId: "target", TargetFlowId: newGuidResult17);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", runtime: nodeRuntimeDefinition);
        var newGuidResult18 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue19 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var flow = FlowDefinition.Create(newGuidResult18, 1, evaluatedValue19, "call");
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder15 = new ExecutionPlanBuilder();
            var buildResult16 = executionPlanBuilder15.Build(flow);
            return buildResult16;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.TargetFlowUnavailable);
    }

    [Fact]
    public void BuilderRejectsExecutionCyclesAsStaticDagViolation()
    {
        var first = NodeDefinition.Create("first", NodeType.Action, "First");
        var second = NodeDefinition.Create("second", NodeType.Action, "Second");
        var newGuidResult22 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue26 = [resolveCreateResult23()];
        var flow = FlowDefinition.Create(newGuidResult22, 1, evaluatedValue26, "first");
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder20 = new ExecutionPlanBuilder();
            var buildResult21 = executionPlanBuilder20.Build(flow);
            return buildResult21;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowErrorCodes.CycleDetected);
        CanvasDefinition resolveCreateResult23()
        {
            IEnumerable<ConnectionDefinition> evaluatedValue24 = [ConnectionDefinition.Execution("first", "out", "second", "in", ExecutionBranch.Success), ConnectionDefinition.Execution("second", "out", "first", "in", ExecutionBranch.Success), ];
            var createResult25 = CanvasDefinition.Create("main", CanvasLifecycle.Main, [first, second], evaluatedValue24);
            return createResult25;
        }
    }

    [Fact]
    public void BuilderRejectsNonPublicFlowCallTargets()
    {
        var target = NodeDefinition.Create("target", NodeType.Action, "Target");
        var nodeRuntimeDefinition29 = new NodeRuntimeDefinition(TargetNodeId: target.Id);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", runtime: nodeRuntimeDefinition29);
        var newGuidResult30 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue31 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var definition = FlowDefinition.Create(newGuidResult30, 1, evaluatedValue31, call.Id);
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder27 = new ExecutionPlanBuilder();
            var buildResult28 = executionPlanBuilder27.Build(definition);
            return buildResult28;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.TargetNotPublic);
    }

    [Fact]
    public void BuilderRejectsFlowCallTargetsOutsideTheConfiguredCanvas()
    {
        var nodeRuntimeDefinition34 = new NodeRuntimeDefinition(IsPublic: true);
        var target = NodeDefinition.Create("target", NodeType.Action, "Target", runtime: nodeRuntimeDefinition34);
        var nodeRuntimeDefinition35 = new NodeRuntimeDefinition(TargetNodeId: target.Id, TargetCanvasId: "other");
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", runtime: nodeRuntimeDefinition35);
        var newGuidResult36 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue37 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var definition = FlowDefinition.Create(newGuidResult36, 1, evaluatedValue37, call.Id);
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder32 = new ExecutionPlanBuilder();
            var buildResult33 = executionPlanBuilder32.Build(definition);
            return buildResult33;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.TargetCanvasMismatch);
    }

    [Fact]
    public void BuilderRejectsMissingRequiredFlowCallParameterBinding()
    {
        IEnumerable<NodeParameterDefinition> evaluatedValue40 = [new NodeParameterDefinition("required", null, required: true, id: "target-required")];
        var nodeRuntimeDefinition41 = new NodeRuntimeDefinition(IsPublic: true);
        var target = NodeDefinition.Create("target", NodeType.Action, "Target", parameters: evaluatedValue40, runtime: nodeRuntimeDefinition41);
        IEnumerable<NodeParameterDefinition> evaluatedValue42 = [new NodeParameterDefinition("other", "1", id: "call-other")];
        var nodeRuntimeDefinition43 = new NodeRuntimeDefinition(TargetNodeId: target.Id);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", parameters: evaluatedValue42, runtime: nodeRuntimeDefinition43);
        var newGuidResult44 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue45 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call, target], [])];
        var definition = FlowDefinition.Create(newGuidResult44, 1, evaluatedValue45, call.Id);
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder38 = new ExecutionPlanBuilder();
            var buildResult39 = executionPlanBuilder38.Build(definition);
            return buildResult39;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.ParameterBindingMissing);
    }

    [Fact]
    public void BuilderRejectsDirectRecursiveFlowCall()
    {
        var nodeRuntimeDefinition48 = new NodeRuntimeDefinition(TargetNodeId: "call", IsPublic: true);
        var call = NodeDefinition.Create("call", NodeType.FlowCall, "Call", runtime: nodeRuntimeDefinition48);
        var newGuidResult49 = Guid.NewGuid();
        IEnumerable<CanvasDefinition> evaluatedValue50 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [call], [])];
        var definition = FlowDefinition.Create(newGuidResult49, 1, evaluatedValue50, call.Id);
        var exception = Assert.Throws<DomainValidationException>(() =>
        {
            var executionPlanBuilder46 = new ExecutionPlanBuilder();
            var buildResult47 = executionPlanBuilder46.Build(definition);
            return buildResult47;
        });
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == FlowCallErrorCodes.CycleDetected);
    }
}
