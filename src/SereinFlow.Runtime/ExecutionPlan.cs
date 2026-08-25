using SereinFlow.Domain;

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
            .Where(connection => connection.FromNodeId == nodeId && connection.Branch == branch)
            .OrderBy(connection => connection.Priority)
            .ThenBy(connection => connection.Id, StringComparer.Ordinal)
            .ToArray();
}

public sealed class ExecutionPlanBuilder
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Builder is an injectable composition boundary. 构建器是可注入的组合边界。")]
    public ExecutionPlan Build(FlowDefinition definition)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        var diagnostics = definition.Validate().ToList();
        if (string.IsNullOrWhiteSpace(definition.EntryNodeId))
        {
            diagnostics.Add(new DomainDiagnostic(
                DomainErrorCodes.UnknownEntryNode,
                "A flow must have an entry node before it can be executed. 流程必须包含入口节点才能执行。",
                "entryNodeId"));
        }

        if (diagnostics.Count > 0)
        {
            throw new DomainValidationException(diagnostics);
        }

        var nodes = definition.Canvases
            .SelectMany(canvas => canvas.Nodes)
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var connections = definition.Canvases
            .SelectMany(canvas => canvas.Connections)
            .ToArray();
        return new ExecutionPlan(definition, nodes, connections);
    }
}
