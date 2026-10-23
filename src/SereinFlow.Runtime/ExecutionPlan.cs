using System.Collections.Generic;
using System.Collections.ObjectModel;
using SereinFlow.Domain;
using SereinFlow.Contracts;

namespace SereinFlow.Runtime;

public sealed class ExecutionPlan
{
    private readonly Dictionary<(string NodeId, ExecutionBranch? Branch), IReadOnlyList<ConnectionDefinition>> _outgoing;
    private readonly Dictionary<(string NodeId, string PortId), IReadOnlyList<ConnectionDefinition>> _incomingData;
    private readonly HashSet<string> _incomingExecution;
    private readonly Dictionary<string, string> _canvasByNode;
    internal ExecutionPlan(FlowDefinition definition, IReadOnlyDictionary<string, NodeDefinition> nodes, IReadOnlyList<ConnectionDefinition> connections)
    {
        Definition = definition;
        Nodes = nodes;
        Connections = connections;
        _outgoing = connections
            .Where(static edge => edge.Kind == ConnectionKind.Execution)
            .GroupBy(static edge => (edge.FromNodeId, edge.Branch))
            .ToDictionary(static group => group.Key, static group =>
        {
            return (IReadOnlyList<ConnectionDefinition>)Sort(group);
        });
        _incomingData = connections
            .Where(static edge => edge.Kind == ConnectionKind.Data)
            .GroupBy(static edge => (edge.ToNodeId, edge.ToPortId))
            .ToDictionary(static group => group.Key, static group =>
        {
            return (IReadOnlyList<ConnectionDefinition>)Sort(group);
        });
        _incomingExecution = connections
            .Where(static edge => edge.Kind == ConnectionKind.Execution)
            .Select(static edge => edge.ToNodeId)
            .ToHashSet(StringComparer.Ordinal);
        _canvasByNode = definition.Canvases.SelectMany(static canvas =>
        {
            return canvas.Nodes.Select(node => (node.Id, CanvasId: canvas.Id));
        }).ToDictionary(static item => item.Id, static item => item.CanvasId, StringComparer.Ordinal);
    }

    public FlowDefinition Definition { get; }
    public IReadOnlyDictionary<string, NodeDefinition> Nodes { get; }
    public IReadOnlyList<ConnectionDefinition> Connections { get; }

    public IReadOnlyList<ConnectionDefinition> GetOutgoing(string nodeId, ExecutionBranch branch)
    {
        return _outgoing.GetValueOrDefault((nodeId, branch)) ?? Array.Empty<ConnectionDefinition>();
    }

    public IReadOnlyList<ConnectionDefinition> GetIncomingData(string nodeId, string portId)
    {
        return _incomingData.GetValueOrDefault((nodeId, portId)) ?? Array.Empty<ConnectionDefinition>();
    }

    public bool HasIncomingExecution(string nodeId)
    {
        return _incomingExecution.Contains(nodeId);
    }

    internal string GetCanvasId(string nodeId)
    {
        return _canvasByNode.GetValueOrDefault(nodeId) ?? string.Empty;
    }
    private static ReadOnlyCollection<ConnectionDefinition> Sort(IEnumerable<ConnectionDefinition> edges)
    {
        return Array.AsReadOnly(edges
            .OrderBy(static edge => edge.Priority)
            .ThenBy(static edge => edge.Id, StringComparer.Ordinal)
            .ToArray());
    }
}

/// <summary>
/// Builds indexed execution plans from flow definitions.
/// 根据流程定义构建索引化执行计划。
/// </summary>
public sealed class ExecutionPlanBuilder
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Builder is an injectable composition boundary. 构建器是可注入的组合边界。")]
    /// <summary>
    /// Builds an execution plan for a flow definition.
    /// 为流程定义构建执行计划。
    /// </summary>
    public ExecutionPlan Build(FlowDefinition definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        }

        var nodes = definition.Canvases.SelectMany(canvas => canvas.Nodes).ToDictionary(node => node.Id, StringComparer.Ordinal);
        var connections = definition.Canvases.SelectMany(canvas => canvas.Connections).ToArray();
        var diagnostics = definition.Validate().ToList();
        var hasGlobalFlipflop = nodes.Values.Any(node =>
        {
            return node.Type == NodeType.Flipflop && !connections.Any(connection => connection.Kind == ConnectionKind.Execution && connection.ToNodeId == node.Id);
        });
        if (string.IsNullOrWhiteSpace(definition.EntryNodeId) && !hasGlobalFlipflop)
        {
            var domainDiagnostic = new DomainDiagnostic(SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode, "A flow must have an entry node before it can be executed. 流程必须包含入口节点才能执行。", "entryNodeId");
            diagnostics.Add(domainDiagnostic);
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
            {
                continue;
            }

            var analysis = FlowCallReturnTypeAnalyzer.Analyze(initialPlan, runtime.TargetNodeId);
            enrichedNodes[node.Id] = node.WithRuntime(runtime with { StaticReturnType = analysis.ReturnType, IsDynamicReturnType = analysis.IsDynamic, });
        }
        // The normalized runtime metadata is kept on the immutable execution
        // plan, so Worker connectors and the executor use one deterministic
        // return-type result even when an older editor omitted the hint.
        // 将规范化的运行时元数据保存在不可变执行计划中；即使旧版编辑器未提供提示，Worker 连接器和执行器也使用同一确定的返回类型结果。
        return new ExecutionPlan(definition, enrichedNodes, connections);
    }

    private static void ValidateExecutionCycles(IReadOnlyDictionary<string, NodeDefinition> nodes, IReadOnlyList<ConnectionDefinition> connections)
    {
        var edges = connections
            .Where(connection => connection.Kind == ConnectionKind.Execution)
            .GroupBy(connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group =>
            {
                return group
                    .Select(item => item.ToNodeId)
                    .ToArray();
            }, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nodeId in nodes.Keys)
        {
            if (Visit(nodeId))
            {
                IReadOnlyList<DomainDiagnostic> diagnostics = [new DomainDiagnostic(FlowErrorCodes.CycleDetected, "Execution connections must form a directed acyclic graph. 执行连接必须构成有向无环图。", $"nodes.{nodeId}")];
                throw new DomainValidationException(diagnostics);
            }
        }

        bool Visit(string nodeId)
        {
            if (!visiting.Add(nodeId))
            {
                return true;
            }

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
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(nodeId);
            return false;
        }
    }

    private static void ValidateFlowCallTargets(FlowDefinition definition, IReadOnlyDictionary<string, NodeDefinition> nodes)
    {
        var canvasByNodeId = definition.Canvases
            .SelectMany(canvas =>
            {
                return canvas.Nodes.Select(node => new { node.Id, CanvasId = canvas.Id });
            })
            .ToDictionary(item => item.Id, item => item.CanvasId, StringComparer.Ordinal);
        foreach (var node in nodes.Values.Where(node => node.Type == NodeType.FlowCall))
        {
            if (string.IsNullOrWhiteSpace(node.Runtime?.TargetNodeId))
            {
                IReadOnlyList<DomainDiagnostic> diagnostics = [new DomainDiagnostic(FlowCallErrorCodes.TargetMissing, $"FlowCall node '{node.Id}' must define a target node. FlowCall 节点“{node.Id}”必须定义目标节点。", $"nodes.{node.Id}.runtime.targetNodeId")];
                throw new DomainValidationException(diagnostics);
            }

            if (!nodes.ContainsKey(node.Runtime.TargetNodeId))
            {
                IReadOnlyList<DomainDiagnostic> mappedDiagnostics = [new DomainDiagnostic(FlowCallErrorCodes.TargetMissing, $"FlowCall target node '{node.Runtime.TargetNodeId}' does not exist. FlowCall 目标节点“{node.Runtime.TargetNodeId}”不存在。", $"nodes.{node.Id}.runtime.targetNodeId")];
                throw new DomainValidationException(mappedDiagnostics);
            }

            if (node.Runtime.TargetFlowId is not null && node.Runtime.TargetFlowId != definition.Id)
            {
                IReadOnlyList<DomainDiagnostic> resolvedDiagnostics = [new DomainDiagnostic(FlowCallErrorCodes.TargetFlowUnavailable, "The target flow is not part of the current immutable run snapshot. 目标流程不在当前不可变运行快照中。", $"nodes.{node.Id}.runtime.targetFlowId")];
                throw new DomainValidationException(resolvedDiagnostics);
            }

            var target = nodes[node.Runtime.TargetNodeId];
            if (target.Runtime?.IsPublic != true)
            {
                IReadOnlyList<DomainDiagnostic> visibilityDiagnostics = [new DomainDiagnostic(FlowCallErrorCodes.TargetNotPublic, $"FlowCall target node '{target.Id}' is not public. FlowCall 目标节点“{target.Id}”不是公开节点。", $"nodes.{node.Id}.runtime.targetNodeId")];
                throw new DomainValidationException(visibilityDiagnostics);
            }

            if (!string.IsNullOrWhiteSpace(node.Runtime.TargetCanvasId) && (!canvasByNodeId.TryGetValue(target.Id, out var targetCanvasId) || !string.Equals(node.Runtime.TargetCanvasId, targetCanvasId, StringComparison.Ordinal)))
            {
                IReadOnlyList<DomainDiagnostic> canvasMismatchDiagnostics = [new DomainDiagnostic(FlowCallErrorCodes.TargetCanvasMismatch, "The FlowCall target canvas does not match the target node. FlowCall 目标画布与目标节点不匹配。", $"nodes.{node.Id}.runtime.targetCanvasId")];
                throw new DomainValidationException(canvasMismatchDiagnostics);
            }

            ValidateFlowCallParameterBindings(node, target);
        }
    }

    private static void ValidateFlowCallParameterBindings(NodeDefinition callNode, NodeDefinition targetNode)
    {
        var bindings = callNode.Runtime?.FlowCallParameterBindings ?? [];
        var callParameterIds = callNode.Parameters
            .Select(static parameter => parameter.Id)
            .ToHashSet(StringComparer.Ordinal);
        var targetParameterIds = targetNode.Parameters
            .Select(static parameter => parameter.Id)
            .ToHashSet(StringComparer.Ordinal);
        var boundCalls = new HashSet<string>(StringComparer.Ordinal);
        var boundTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!callParameterIds.Contains(binding.CallParameterId) || !targetParameterIds.Contains(binding.TargetParameterId) || !boundCalls.Add(binding.CallParameterId) || !boundTargets.Add(binding.TargetParameterId))
            {
                IReadOnlyList<DomainDiagnostic> diagnostics = [new DomainDiagnostic(FlowCallErrorCodes.ParameterBindingInvalid, "The FlowCall parameter binding is invalid. FlowCall 参数映射无效。", $"nodes.{callNode.Id}.runtime.flowCallParameterBindings")];
                throw new DomainValidationException(diagnostics);
            }
        }

        var missing = targetNode.Parameters.FirstOrDefault(parameter =>
        {
            return parameter.Required && !(bindings.Count == 0 ? callParameterIds.Contains(parameter.Id) : boundTargets.Contains(parameter.Id));
        });
        if (missing is not null)
        {
            IReadOnlyList<DomainDiagnostic> mappedDiagnostics = [new DomainDiagnostic(FlowCallErrorCodes.ParameterBindingMissing, $"Required target parameter '{missing.Name}' has no FlowCall input mapping. 目标必需参数“{missing.Name}”没有 FlowCall 输入映射。", $"nodes.{callNode.Id}.runtime.flowCallParameterBindings")];
            throw new DomainValidationException(mappedDiagnostics);
        }
    }

    private static void ValidateFlowCallCycles(IReadOnlyDictionary<string, NodeDefinition> nodes, IReadOnlyList<ConnectionDefinition> connections)
    {
        // A FlowCall introduces a call edge in addition to the visual
        // execution edges. Treat both as one conservative call graph so direct
        // and indirect recursion is rejected before a Worker is started.
        // FlowCall 除了可视化执行边还会引入调用边；合并检查可在 Worker 启动前拒绝直接和间接递归。
        var edges = connections
            .Where(static connection => connection.Kind == ConnectionKind.Execution)
            .GroupBy(static connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group =>
        {
            return group
                .Select(static item => item.ToNodeId)
                .ToArray();
        }, StringComparer.Ordinal);
        foreach (var node in nodes.Values.Where(static node => node.Type == NodeType.FlowCall))
        {
            if (!string.IsNullOrWhiteSpace(node.Runtime?.TargetNodeId))
            {
                if (!edges.TryGetValue(node.Id, out var outgoing))
                {
                    edges[node.Id] = [node.Runtime.TargetNodeId];
                }
                else
                {
                    edges[node.Id] = [.. outgoing, node.Runtime.TargetNodeId];
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nodeId in nodes.Keys)
        {
            if (Visit(nodeId))
            {
                IReadOnlyList<DomainDiagnostic> diagnostics = [new DomainDiagnostic(FlowCallErrorCodes.CycleDetected, "FlowCall invocation graph must be acyclic. FlowCall 调用图必须无环。", $"nodes.{nodeId}")];
                throw new DomainValidationException(diagnostics);
            }
        }

        bool Visit(string nodeId)
        {
            if (!visiting.Add(nodeId))
            {
                return true;
            }

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
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(nodeId);
            return false;
        }
    }
}

/// <summary>
/// Represents flow call return type analysis data used by SereinFlow.
/// 表示 SereinFlow 使用的流程CallReturnTypeAnalysis数据。
/// </summary>
public sealed record FlowCallReturnTypeAnalysis(string ReturnType, bool IsDynamic);
/// <summary>
/// Performs the conservative static return-type analysis used by FlowCall
/// connector hints. Runtime values are still validated by the executor.
/// 为 FlowCall 连接器提示执行保守的静态返回类型分析；执行器仍会校验运行时值。
/// </summary>
public static class FlowCallReturnTypeAnalyzer
{
    /// <summary>
    /// Infers the return type of a flow call target.
    /// 推断流程调用目标的返回类型。
    /// </summary>
    public static FlowCallReturnTypeAnalysis Analyze(ExecutionPlan plan, string targetNodeId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(targetNodeId) || !plan.Nodes.ContainsKey(targetNodeId))
        {
            IReadOnlyList<DomainDiagnostic> diagnostics = [new DomainDiagnostic(FlowCallErrorCodes.TargetMissing, $"FlowCall target node '{targetNodeId}' does not exist. FlowCall 目标节点“{targetNodeId}”不存在。", "targetNodeId")];
            throw new DomainValidationException(diagnostics);
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([targetNodeId]);
        while (pending.Count > 0)
        {
            var nodeId = pending.Pop();
            if (!reachable.Add(nodeId))
            {
                continue;
            }

            foreach (var connection in plan.Connections.Where(connection => connection.Kind == ConnectionKind.Execution && connection.FromNodeId == nodeId))
            {
                pending.Push(connection.ToNodeId);
            }
        }

        var terminalTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nodeId in reachable)
        {
            var node = plan.Nodes[nodeId];
            var hasSuccessor = plan.Connections.Any(connection => connection.Kind == ConnectionKind.Execution && connection.FromNodeId == nodeId);
            if (hasSuccessor)
            {
                continue;
            }

            var returnType = node.Runtime?.StaticReturnType ?? node.Runtime?.ReturnType;
            if (!string.IsNullOrWhiteSpace(returnType) && !string.Equals(returnType, "System.Void", StringComparison.OrdinalIgnoreCase))
            {
                terminalTypes.Add(returnType);
            }
            else if (node.Script is not null && node.Script.Outputs.Count > 0)
            {
                terminalTypes.Add("System.Object");
            }
            else
            {
                terminalTypes.Add("void");
            }
        }

        if (terminalTypes.Count == 0 || terminalTypes.SetEquals(["void"]))
        {
            return new("void", false);
        }

        if (terminalTypes.Count == 1)
        {
            var single = terminalTypes.Single();
            return new(single, false);
        }
        return new("System.Object", true);
    }
}
