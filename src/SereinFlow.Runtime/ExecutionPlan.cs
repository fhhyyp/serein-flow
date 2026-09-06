using SereinFlow.Domain;
using SereinFlow.Contracts;

namespace SereinFlow.Runtime;

public sealed class ExecutionPlan
{
    internal ExecutionPlan(
        FlowDefinition definition,
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlyList<ConnectionDefinition> connections)
    {
        Definition = definition;
        Nodes = nodes;
        Connections = connections;
    }

    public FlowDefinition Definition { get; }

    public IReadOnlyDictionary<string, NodeDefinition> Nodes { get; }

    public IReadOnlyList<ConnectionDefinition> Connections { get; }

    public IReadOnlyList<ConnectionDefinition> GetOutgoing(string nodeId, ExecutionBranch branch)
        => Connections
            .Where(connection => connection.Kind == ConnectionKind.Execution
                && connection.FromNodeId == nodeId
                && connection.Branch == branch)
            .OrderBy(connection => connection.Priority)
            .ThenBy(connection => connection.Id, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<ConnectionDefinition> GetIncomingData(string nodeId, string portId)
        => Connections
            .Where(connection => connection.Kind == ConnectionKind.Data
                && connection.ToNodeId == nodeId
                && (connection.ToPortId == portId || connection.ToPortId.Equals(portId, StringComparison.Ordinal)))
            .OrderBy(connection => connection.Priority)
            .ThenBy(connection => connection.Id, StringComparer.Ordinal)
            .ToArray();

    public bool HasIncomingExecution(string nodeId)
        => Connections.Any(connection => connection.Kind == ConnectionKind.Execution && connection.ToNodeId == nodeId);
}

public sealed class ExecutionPlanBuilder
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Builder is an injectable composition boundary. 构建器是可注入的组合边界。")]
    public ExecutionPlan Build(FlowDefinition definition)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        var nodes = definition.Canvases
            .SelectMany(canvas => canvas.Nodes)
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var connections = definition.Canvases
            .SelectMany(canvas => canvas.Connections)
            .ToArray();
        var diagnostics = definition.Validate().ToList();
        var hasGlobalFlipflop = nodes.Values.Any(node =>
            node.Type == NodeType.Flipflop
            && !connections.Any(connection => connection.Kind == ConnectionKind.Execution && connection.ToNodeId == node.Id));
        if (string.IsNullOrWhiteSpace(definition.EntryNodeId) && !hasGlobalFlipflop)
        {
            diagnostics.Add(new DomainDiagnostic(
                SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode,
                "A flow must have an entry node before it can be executed. 流程必须包含入口节点才能执行。",
                "entryNodeId"));
        }

        if (diagnostics.Count > 0)
        {
            throw new DomainValidationException(diagnostics);
        }

        ValidateExecutionCycles(nodes, connections);
        ValidateFlowCallTargets(definition, nodes);
        ValidateFlowCallCycles(nodes, connections);
        var initialPlan = new ExecutionPlan(definition, nodes, connections);
        var enrichedNodes = nodes.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        foreach (var node in nodes.Values.Where(static node => node.Type == NodeType.FlowCall))
        {
            var runtime = node.Runtime;
            if (runtime is null || string.IsNullOrWhiteSpace(runtime.TargetNodeId))
                continue;

            var analysis = FlowCallReturnTypeAnalyzer.Analyze(initialPlan, runtime.TargetNodeId);
            enrichedNodes[node.Id] = node.WithRuntime(runtime with
            {
                StaticReturnType = analysis.ReturnType,
                IsDynamicReturnType = analysis.IsDynamic,
            });
        }

        // The normalized runtime metadata is kept on the immutable execution
        // plan, so Worker connectors and the executor use one deterministic
        // return-type result even when an older editor omitted the hint.
        return new ExecutionPlan(definition, enrichedNodes, connections);
    }

    private static void ValidateExecutionCycles(
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlyList<ConnectionDefinition> connections)
    {
        var edges = connections
            .Where(connection => connection.Kind == ConnectionKind.Execution)
            .GroupBy(connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.ToNodeId).ToArray(), StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string nodeId)
        {
            if (!visiting.Add(nodeId))
                return true;
            if (!visited.Add(nodeId))
            {
                visiting.Remove(nodeId);
                return false;
            }

            if (edges.TryGetValue(nodeId, out var nextNodes))
            {
                foreach (var next in nextNodes)
                {
                    if (Visit(next))
                        return true;
                }
            }

            visiting.Remove(nodeId);
            return false;
        }

        foreach (var nodeId in nodes.Keys)
        {
            if (Visit(nodeId))
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowErrorCodes.CycleDetected,
                        "Execution connections must form a directed acyclic graph. 执行连接必须构成有向无环图。",
                        $"nodes.{nodeId}")]);
            }
        }
    }

    private static void ValidateFlowCallTargets(FlowDefinition definition, IReadOnlyDictionary<string, NodeDefinition> nodes)
    {
        var canvasByNodeId = definition.Canvases
            .SelectMany(canvas => canvas.Nodes.Select(node => new { node.Id, CanvasId = canvas.Id }))
            .ToDictionary(item => item.Id, item => item.CanvasId, StringComparer.Ordinal);
        foreach (var node in nodes.Values.Where(node => node.Type == NodeType.FlowCall))
        {
            if (string.IsNullOrWhiteSpace(node.Runtime?.TargetNodeId))
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowCallErrorCodes.TargetMissing,
                        $"FlowCall node '{node.Id}' must define a target node. FlowCall 节点“{node.Id}”必须定义目标节点。",
                        $"nodes.{node.Id}.runtime.targetNodeId")]);
            }

            if (!nodes.ContainsKey(node.Runtime.TargetNodeId))
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowCallErrorCodes.TargetMissing,
                        $"FlowCall target node '{node.Runtime.TargetNodeId}' does not exist. FlowCall 目标节点“{node.Runtime.TargetNodeId}”不存在。",
                        $"nodes.{node.Id}.runtime.targetNodeId")]);
            }

            if (node.Runtime.TargetFlowId is not null && node.Runtime.TargetFlowId != definition.Id)
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowCallErrorCodes.TargetFlowUnavailable,
                        "The target flow is not part of the current immutable run snapshot. 目标流程不在当前不可变运行快照中。",
                        $"nodes.{node.Id}.runtime.targetFlowId")]);
            }

            var target = nodes[node.Runtime.TargetNodeId];
            if (target.Runtime?.IsPublic != true)
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowCallErrorCodes.TargetNotPublic,
                        $"FlowCall target node '{target.Id}' is not public. FlowCall 目标节点“{target.Id}”不是公开节点。",
                        $"nodes.{node.Id}.runtime.targetNodeId")]);
            }

            if (!string.IsNullOrWhiteSpace(node.Runtime.TargetCanvasId)
                && (!canvasByNodeId.TryGetValue(target.Id, out var targetCanvasId)
                    || !string.Equals(node.Runtime.TargetCanvasId, targetCanvasId, StringComparison.Ordinal)))
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowCallErrorCodes.TargetCanvasMismatch,
                        "The FlowCall target canvas does not match the target node. FlowCall 目标画布与目标节点不匹配。",
                        $"nodes.{node.Id}.runtime.targetCanvasId")]);
            }

            ValidateFlowCallParameterBindings(node, target);
        }
    }

    private static void ValidateFlowCallParameterBindings(NodeDefinition callNode, NodeDefinition targetNode)
    {
        var bindings = callNode.Runtime?.FlowCallParameterBindings ?? [];
        var callParameterIds = callNode.Parameters.Select(static parameter => parameter.Id).ToHashSet(StringComparer.Ordinal);
        var targetParameterIds = targetNode.Parameters.Select(static parameter => parameter.Id).ToHashSet(StringComparer.Ordinal);
        var boundCalls = new HashSet<string>(StringComparer.Ordinal);
        var boundTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!callParameterIds.Contains(binding.CallParameterId)
                || !targetParameterIds.Contains(binding.TargetParameterId)
                || !boundCalls.Add(binding.CallParameterId)
                || !boundTargets.Add(binding.TargetParameterId))
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                    FlowCallErrorCodes.ParameterBindingInvalid,
                        "The FlowCall parameter binding is invalid. FlowCall 参数映射无效。",
                        $"nodes.{callNode.Id}.runtime.flowCallParameterBindings")]);
            }
        }

        var missing = targetNode.Parameters.FirstOrDefault(parameter =>
            parameter.Required
            && !(bindings.Count == 0
                ? callParameterIds.Contains(parameter.Id)
                : boundTargets.Contains(parameter.Id)));
        if (missing is not null)
        {
            throw new DomainValidationException([
                new DomainDiagnostic(
                    FlowCallErrorCodes.ParameterBindingMissing,
                    $"Required target parameter '{missing.Name}' has no FlowCall input mapping. 目标必需参数“{missing.Name}”没有 FlowCall 输入映射。",
                    $"nodes.{callNode.Id}.runtime.flowCallParameterBindings")]);
        }
    }

    private static void ValidateFlowCallCycles(
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlyList<ConnectionDefinition> connections)
    {
        // A FlowCall introduces a call edge in addition to the visual
        // execution edges. Treat both as one conservative call graph so direct
        // and indirect recursion is rejected before a Worker is started.
        // FlowCall 除了可视化执行边还会引入调用边；合并检查可在 Worker 启动前拒绝直接和间接递归。
        var edges = connections
            .Where(static connection => connection.Kind == ConnectionKind.Execution)
            .GroupBy(static connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(static item => item.ToNodeId).ToArray(), StringComparer.Ordinal);
        foreach (var node in nodes.Values.Where(static node => node.Type == NodeType.FlowCall))
        {
            if (!string.IsNullOrWhiteSpace(node.Runtime?.TargetNodeId))
            {
                if (!edges.TryGetValue(node.Id, out var outgoing))
                    edges[node.Id] = [node.Runtime.TargetNodeId];
                else
                    edges[node.Id] = [.. outgoing, node.Runtime.TargetNodeId];
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string nodeId)
        {
            if (!visiting.Add(nodeId))
                return true;
            if (!visited.Add(nodeId))
            {
                visiting.Remove(nodeId);
                return false;
            }
            if (edges.TryGetValue(nodeId, out var outgoing))
            {
                foreach (var next in outgoing)
                {
                    if (Visit(next))
                        return true;
                }
            }
            visiting.Remove(nodeId);
            return false;
        }

        foreach (var nodeId in nodes.Keys)
        {
            if (Visit(nodeId))
            {
                throw new DomainValidationException([
                    new DomainDiagnostic(
                        FlowCallErrorCodes.CycleDetected,
                        "FlowCall invocation graph must be acyclic. FlowCall 调用图必须无环。",
                        $"nodes.{nodeId}")]);
            }
        }
    }
}

public sealed record FlowCallReturnTypeAnalysis(string ReturnType, bool IsDynamic);

/// <summary>
/// Performs the conservative static return-type analysis used by FlowCall
/// connector hints. Runtime values are still validated by the executor.
/// </summary>
public static class FlowCallReturnTypeAnalyzer
{
    public static FlowCallReturnTypeAnalysis Analyze(ExecutionPlan plan, string targetNodeId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(targetNodeId) || !plan.Nodes.ContainsKey(targetNodeId))
            throw new DomainValidationException([
                new DomainDiagnostic(
                        FlowCallErrorCodes.TargetMissing,
                    $"FlowCall target node '{targetNodeId}' does not exist. FlowCall 目标节点“{targetNodeId}”不存在。",
                    "targetNodeId")]);

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([targetNodeId]);
        while (pending.Count > 0)
        {
            var nodeId = pending.Pop();
            if (!reachable.Add(nodeId))
                continue;
            foreach (var connection in plan.Connections.Where(connection =>
                         connection.Kind == ConnectionKind.Execution && connection.FromNodeId == nodeId))
                pending.Push(connection.ToNodeId);
        }

        var terminalTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nodeId in reachable)
        {
            var node = plan.Nodes[nodeId];
            var hasSuccessor = plan.Connections.Any(connection =>
                connection.Kind == ConnectionKind.Execution && connection.FromNodeId == nodeId);
            if (hasSuccessor)
                continue;

            var returnType = node.Runtime?.StaticReturnType ?? node.Runtime?.ReturnType;
            if (!string.IsNullOrWhiteSpace(returnType) && !string.Equals(returnType, "System.Void", StringComparison.OrdinalIgnoreCase))
                terminalTypes.Add(returnType);
            else if (node.Script is not null && node.Script.Outputs.Count > 0)
                terminalTypes.Add("System.Object");
            else
                terminalTypes.Add("void");
        }

        if (terminalTypes.Count == 0 || terminalTypes.SetEquals(["void"]))
            return new("void", false);
        if (terminalTypes.Count == 1)
            return new(terminalTypes.Single(), false);
        return new("System.Object", true);
    }
}
