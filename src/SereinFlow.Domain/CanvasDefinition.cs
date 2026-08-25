namespace SereinFlow.Domain;

public sealed class CanvasDefinition
{
    private CanvasDefinition(
        string id,
        CanvasLifecycle lifecycle,
        IReadOnlyList<NodeDefinition> nodes,
        IReadOnlyList<ConnectionDefinition> connections)
    {
        Id = id;
        Lifecycle = lifecycle;
        Nodes = nodes;
        Connections = connections;
    }

    public string Id { get; }

    public CanvasLifecycle Lifecycle { get; }

    public IReadOnlyList<NodeDefinition> Nodes { get; }

    public IReadOnlyList<ConnectionDefinition> Connections { get; }

    public static CanvasDefinition Create(
        string id,
        CanvasLifecycle lifecycle,
        IEnumerable<NodeDefinition> nodes,
        IEnumerable<ConnectionDefinition> connections)
    {
        if (nodes is null)
            throw new ArgumentNullException(nameof(nodes), "Canvas nodes cannot be null. 画布节点集合不能为空。");
        if (connections is null)
            throw new ArgumentNullException(nameof(connections), "Canvas connections cannot be null. 画布连接集合不能为空。");

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Canvas ID cannot be empty. 画布 ID 不能为空。", nameof(id));
        }

        return new CanvasDefinition(id.Trim(), lifecycle, nodes.ToArray(), connections.ToArray());
    }
}
