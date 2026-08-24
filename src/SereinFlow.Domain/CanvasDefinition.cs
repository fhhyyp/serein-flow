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
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(connections);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Canvas ID cannot be empty.", nameof(id));
        }

        return new CanvasDefinition(id.Trim(), lifecycle, nodes.ToArray(), connections.ToArray());
    }
}
