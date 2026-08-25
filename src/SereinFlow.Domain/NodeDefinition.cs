namespace SereinFlow.Domain;

public sealed record NodePosition(double X, double Y);

public sealed record PortDefinition
{
    public PortDefinition(string id, string name, PortDirection direction, bool required = false)
    {
        Id = Validate(id, nameof(id));
        Name = Validate(name, nameof(name));
        Direction = direction;
        Required = required;
    }

    public string Id { get; }

    public string Name { get; }

    public PortDirection Direction { get; }

    public bool Required { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Port identifiers and names cannot be empty. 端口标识和名称不能为空。", parameterName);
        }

        return value.Trim();
    }
}

public sealed record NodeParameterDefinition
{
    public NodeParameterDefinition(
        string name,
        string? valueJson,
        DataSource source = DataSource.Literal,
        bool required = false)
    {
        Name = Validate(name, nameof(name));
        ValueJson = valueJson;
        Source = source;
        Required = required;
    }

    public string Name { get; }

    public string? ValueJson { get; }

    public DataSource Source { get; }

    public bool Required { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Parameter names cannot be empty. 参数名称不能为空。", parameterName);
        }

        return value.Trim();
    }
}

public sealed class NodeDefinition
{
    private NodeDefinition(
        string id,
        NodeType type,
        string displayName,
        NodePosition position,
        IReadOnlyList<PortDefinition> ports,
        IReadOnlyList<NodeParameterDefinition> parameters,
        ScriptNodeDefinition? script)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        Position = position;
        Ports = ports;
        Parameters = parameters;
        Script = script;
    }

    public string Id { get; }

    public NodeType Type { get; }

    public string DisplayName { get; }

    public NodePosition Position { get; }

    public IReadOnlyList<PortDefinition> Ports { get; }

    public IReadOnlyList<NodeParameterDefinition> Parameters { get; }

    public ScriptNodeDefinition? Script { get; }

    public static NodeDefinition Create(
        string id,
        NodeType type,
        string displayName,
        NodePosition? position = null,
        IEnumerable<PortDefinition>? ports = null,
        IEnumerable<NodeParameterDefinition>? parameters = null,
        ScriptNodeDefinition? script = null)
    {
        return new NodeDefinition(
            Validate(id, nameof(id)),
            type,
            Validate(displayName, nameof(displayName)),
            position ?? new NodePosition(0, 0),
            (ports ?? []).ToArray(),
            (parameters ?? []).ToArray(),
            script);
    }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Node identifiers and display names cannot be empty. 节点标识和显示名称不能为空。", parameterName);
        }

        return value.Trim();
    }
}
