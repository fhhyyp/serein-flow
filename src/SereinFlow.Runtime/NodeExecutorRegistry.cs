using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class NodeExecutorRegistry
{
    private readonly Dictionary<NodeType, INodeExecutor> _executors;

    public NodeExecutorRegistry(IEnumerable<INodeExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);
        _executors = executors.ToDictionary(executor => executor.NodeType);
    }

    public INodeExecutor Get(NodeType nodeType)
        => _executors.TryGetValue(nodeType, out var executor)
            ? executor
            : throw new InvalidOperationException($"No executor is registered for node type '{nodeType}'.");
}
