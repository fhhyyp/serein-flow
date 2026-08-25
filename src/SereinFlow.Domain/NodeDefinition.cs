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
        bool required = false,
        string? id = null,
        string? projectInputKey = null,
        string? expression = null,
        string? sourceNodeId = null,
        string? sourcePortId = null,
        string? valueKind = null)
    {
        Name = Validate(name, nameof(name));
        Id = string.IsNullOrWhiteSpace(id) ? Name : id.Trim();
        ValueJson = valueJson;
        Source = source;
        Required = required;
        ProjectInputKey = projectInputKey?.Trim();
        Expression = expression;
        SourceNodeId = sourceNodeId?.Trim();
        SourcePortId = sourcePortId?.Trim();
        ValueKind = valueKind?.Trim();
    }

    public string Id { get; }

    public string Name { get; }

    public string? ValueJson { get; }

    public DataSource Source { get; }

    public bool Required { get; }

    public string? ProjectInputKey { get; }

    public string? Expression { get; }

    public string? SourceNodeId { get; }

    public string? SourcePortId { get; }

    public string? ValueKind { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Parameter names cannot be empty. 参数名称不能为空。", parameterName);
        }

        return value.Trim();
    }
}

public sealed record NodeRuntimeDefinition(
    string? LibraryId = null,
    string? ClassName = null,
    string? MethodName = null,
    string? DllName = null,
    string? DllVersion = null,
    string? ReturnType = null,
    string? TargetNodeId = null,
    Guid? TargetFlowId = null,
    bool IsAwaitable = false,
    string? StaticReturnType = null,
    bool IsDynamicReturnType = false);

public sealed class NodeDefinition
{
    private NodeDefinition(
        string id,
        NodeType type,
        string displayName,
        NodePosition position,
        IReadOnlyList<PortDefinition> ports,
        IReadOnlyList<NodeParameterDefinition> parameters,
        ScriptNodeDefinition? script,
        NodeRuntimeDefinition? runtime)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        Position = position;
        Ports = ports;
        Parameters = parameters;
        Script = script;
        Runtime = runtime;
    }

    public string Id { get; }

    public NodeType Type { get; }

    public string DisplayName { get; }

    public NodePosition Position { get; }

    public IReadOnlyList<PortDefinition> Ports { get; }

    public IReadOnlyList<NodeParameterDefinition> Parameters { get; }

    public ScriptNodeDefinition? Script { get; }

    public NodeRuntimeDefinition? Runtime { get; }

    public NodeDefinition WithRuntime(NodeRuntimeDefinition? runtime)
        => Create(Id, Type, DisplayName, Position, Ports, Parameters, Script, runtime);

    public static NodeDefinition Create(
        string id,
        NodeType type,
        string displayName,
        NodePosition? position = null,
        IEnumerable<PortDefinition>? ports = null,
        IEnumerable<NodeParameterDefinition>? parameters = null,
        ScriptNodeDefinition? script = null,
        NodeRuntimeDefinition? runtime = null)
    {
        return new NodeDefinition(
            Validate(id, nameof(id)),
            type,
            Validate(displayName, nameof(displayName)),
            position ?? new NodePosition(0, 0),
            (ports ?? []).ToArray(),
            (parameters ?? []).ToArray(),
            script,
            runtime);
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
