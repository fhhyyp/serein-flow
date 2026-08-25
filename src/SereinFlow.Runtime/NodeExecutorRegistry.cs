using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class NodeExecutorRegistry
{
    private readonly Dictionary<NodeType, INodeExecutor> _executors;

    public NodeExecutorRegistry(IEnumerable<INodeExecutor> executors)
    {
        if (executors is null)
            throw new ArgumentNullException(nameof(executors), "Node executors cannot be null. 节点执行器集合不能为空。");
        _executors = executors.ToDictionary(executor => executor.NodeType);
    }

    public INodeExecutor Get(NodeType nodeType)
        => _executors.TryGetValue(nodeType, out var executor)
            ? executor
            : throw new InvalidOperationException($"No executor is registered for node type '{nodeType}'. 未注册节点类型“{nodeType}”的执行器。");

    public IReadOnlyCollection<INodeExecutor> GetAll() => _executors.Values.ToArray();
}
