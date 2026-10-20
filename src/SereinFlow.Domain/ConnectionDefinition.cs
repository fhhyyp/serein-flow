namespace SereinFlow.Domain;
public sealed class ConnectionDefinition
{
    private ConnectionDefinition(string id, string fromNodeId, string fromPortId, string toNodeId, string toPortId, ConnectionKind kind, ExecutionBranch? branch, DataSource? dataSource, int priority)
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

    public static ConnectionDefinition Execution(string fromNodeId, string fromPortId, string toNodeId, string toPortId, ExecutionBranch branch, int priority = 0, string? id = null)
    {
        var validateResult = Validate(id ?? $"{fromNodeId}:{fromPortId}->{toNodeId}:{toPortId}", nameof(id));
        var validateResult1 = Validate(fromNodeId, nameof(fromNodeId));
        var validateResult2 = Validate(fromPortId, nameof(fromPortId));
        var validateResult3 = Validate(toNodeId, nameof(toNodeId));
        var validateResult4 = Validate(toPortId, nameof(toPortId));
        var connectionDefinition = new ConnectionDefinition(validateResult, validateResult1, validateResult2, validateResult3, validateResult4, ConnectionKind.Execution, branch, null, priority);
        return connectionDefinition;
    }

    public static ConnectionDefinition Data(string fromNodeId, string fromPortId, string toNodeId, string toPortId, DataSource dataSource, int priority = 0, string? id = null)
    {
        var validateResult5 = Validate(id ?? $"{fromNodeId}:{fromPortId}->{toNodeId}:{toPortId}", nameof(id));
        var validateResult6 = Validate(fromNodeId, nameof(fromNodeId));
        var validateResult7 = Validate(fromPortId, nameof(fromPortId));
        var validateResult8 = Validate(toNodeId, nameof(toNodeId));
        var validateResult9 = Validate(toPortId, nameof(toPortId));
        var connectionDefinition10 = new ConnectionDefinition(validateResult5, validateResult6, validateResult7, validateResult8, validateResult9, ConnectionKind.Data, null, dataSource, priority);
        return connectionDefinition10;
    }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Connection identifiers and endpoints cannot be empty. 连接标识和端点不能为空。", parameterName);
        }

        var trimResult = value.Trim();
        return trimResult;
    }
}
