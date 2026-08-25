namespace SereinFlow.Domain;

public sealed class ConnectionDefinition
{
    private ConnectionDefinition(
        string id,
        string fromNodeId,
        string fromPortId,
        string toNodeId,
        string toPortId,
        ConnectionKind kind,
        ExecutionBranch? branch,
        DataSource? dataSource,
        int priority)
    {
        Id = id;
        FromNodeId = fromNodeId;
        FromPortId = fromPortId;
        ToNodeId = toNodeId;
        ToPortId = toPortId;
        Kind = kind;
        Branch = branch;
        DataSource = dataSource;
        Priority = priority;
    }

    public string Id { get; }

    public string FromNodeId { get; }

    public string FromPortId { get; }

    public string ToNodeId { get; }

    public string ToPortId { get; }

    public ConnectionKind Kind { get; }

    public ExecutionBranch? Branch { get; }

    public DataSource? DataSource { get; }

    public int Priority { get; }

    public static ConnectionDefinition Execution(
        string fromNodeId,
        string fromPortId,
        string toNodeId,
        string toPortId,
        ExecutionBranch branch,
        int priority = 0,
        string? id = null)
    {
        return new ConnectionDefinition(
            Validate(id ?? $"{fromNodeId}:{fromPortId}->{toNodeId}:{toPortId}", nameof(id)),
            Validate(fromNodeId, nameof(fromNodeId)),
            Validate(fromPortId, nameof(fromPortId)),
            Validate(toNodeId, nameof(toNodeId)),
            Validate(toPortId, nameof(toPortId)),
            ConnectionKind.Execution,
            branch,
            null,
            priority);
    }

    public static ConnectionDefinition Data(
        string fromNodeId,
        string fromPortId,
        string toNodeId,
        string toPortId,
        DataSource dataSource,
        int priority = 0,
        string? id = null)
    {
        return new ConnectionDefinition(
            Validate(id ?? $"{fromNodeId}:{fromPortId}->{toNodeId}:{toPortId}", nameof(id)),
            Validate(fromNodeId, nameof(fromNodeId)),
            Validate(fromPortId, nameof(fromPortId)),
            Validate(toNodeId, nameof(toNodeId)),
            Validate(toPortId, nameof(toPortId)),
            ConnectionKind.Data,
            null,
            dataSource,
            priority);
    }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Connection identifiers and endpoints cannot be empty. 连接标识和端点不能为空。", parameterName);
        }

        return value.Trim();
    }
}
