namespace SereinFlow.Domain;
/// <summary>
/// Represents a directed connection between flow node ports.
/// 表示流程节点端口之间的有向连线。
/// </summary>
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

    /// <summary>
    /// Identifies the connection.
    /// 连线标识。
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Identifies the source node.
    /// 源节点标识。
    /// </summary>
    public string FromNodeId { get; }
    /// <summary>
    /// Identifies the source port.
    /// 源端口标识。
    /// </summary>
    public string FromPortId { get; }
    /// <summary>
    /// Identifies the destination node.
    /// 目标节点标识。
    /// </summary>
    public string ToNodeId { get; }
    /// <summary>
    /// Identifies the destination port.
    /// 目标端口标识。
    /// </summary>
    public string ToPortId { get; }
    /// <summary>
    /// Stores whether this is an execution or data connection.
    /// 连线类型，区分执行连线和数据连线。
    /// </summary>
    public ConnectionKind Kind { get; }
    /// <summary>
    /// Stores the execution branch when applicable.
    /// 适用时记录执行分支。
    /// </summary>
    public ExecutionBranch? Branch { get; }
    /// <summary>
    /// Stores the data source when applicable.
    /// 适用时记录数据来源。
    /// </summary>
    public DataSource? DataSource { get; }
    /// <summary>
    /// Determines the connection evaluation order.
    /// 决定连线的求值顺序。
    /// </summary>
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
