namespace SereinFlow.Domain;
/// <summary>
/// Represents node position data used by SereinFlow.
/// 表示 SereinFlow 使用的节点Position数据。
/// </summary>
public sealed record NodePosition(double X, double Y);
/// <summary>
/// Represents port definition data used by SereinFlow.
/// 表示 SereinFlow 使用的端口定义数据。
/// </summary>
public sealed record PortDefinition
{
    public PortDefinition(string id, string name, PortDirection direction, bool required = false)
    {
        Id = Validate(id, nameof(id));
        Name = Validate(name, nameof(name));
        Direction = direction;
        Required = required;
    }

    /// <summary>
    /// Identifies the node.
    /// 节点标识。
    /// </summary>
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

        var trimResult = value.Trim();
        return trimResult;
    }
}

/// <summary>
/// Represents node parameter definition data used by SereinFlow.
/// 表示 SereinFlow 使用的节点参数定义数据。
/// </summary>
public sealed record NodeParameterDefinition
{
    public NodeParameterDefinition(string name, string? valueJson, DataSource source = DataSource.Literal, bool required = false, string? id = null, string? projectInputKey = null, string? expression = null, string? sourceNodeId = null, string? sourcePortId = null, string? valueKind = null, string? description = null, bool isVariadic = false, string? variadicGroupId = null, string? elementType = null, VariadicParameterMode? variadicMode = null, EnumParameterMetadata? enumMetadata = null)
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
        Description = description?.Trim();
        IsVariadic = isVariadic;
        VariadicGroupId = variadicGroupId?.Trim();
        ElementType = elementType?.Trim();
        VariadicMode = variadicMode;
        EnumMetadata = enumMetadata;
    }

    /// <summary>
    /// Identifies the node.
    /// 节点标识。
    /// </summary>
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
    public string? Description { get; }
    public bool IsVariadic { get; }
    public string? VariadicGroupId { get; }
    public string? ElementType { get; }
    public VariadicParameterMode? VariadicMode { get; }
    public EnumParameterMetadata? EnumMetadata { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Parameter names cannot be empty. 参数名称不能为空。", parameterName);
        }

        var trimResult1 = value.Trim();
        return trimResult1;
    }
}

/// <summary>
/// Represents node runtime definition data used by SereinFlow.
/// 表示 SereinFlow 使用的节点Runtime定义数据。
/// </summary>
public sealed record NodeRuntimeDefinition(string? LibraryId = null, string? ClassName = null, string? MethodName = null, string? DllName = null, string? DllVersion = null, string? ReturnType = null, string? TargetNodeId = null, Guid? TargetFlowId = null, bool IsAwaitable = false, string? StaticReturnType = null, bool IsDynamicReturnType = false, string? TargetCanvasId = null, bool IsPublic = false, IReadOnlyList<FlowCallParameterBinding>? FlowCallParameterBindings = null, string? LibraryNodeContractId = null);
/// <summary>
/// Represents flow call parameter binding data used by SereinFlow.
/// 表示 SereinFlow 使用的流程Call参数Binding数据。
/// </summary>
public sealed record FlowCallParameterBinding(string CallParameterId, string TargetParameterId);
/// <summary>
/// Represents a node and its runtime configuration in a flow.
/// 表示流程中的节点及其运行时配置。
/// </summary>
public sealed class NodeDefinition
{
    private NodeDefinition(string id, NodeType type, string displayName, NodePosition position, IReadOnlyList<PortDefinition> ports, IReadOnlyList<NodeParameterDefinition> parameters, ScriptNodeDefinition? script, NodeRuntimeDefinition? runtime)
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

    /// <summary>
    /// Identifies the node.
    /// 节点标识。
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Stores the node kind.
    /// 节点类型。
    /// </summary>
    public NodeType Type { get; }
    /// <summary>
    /// Stores the node display name.
    /// 节点显示名称。
    /// </summary>
    public string DisplayName { get; }
    /// <summary>
    /// Stores the node position on the canvas.
    /// 节点在画布上的位置。
    /// </summary>
    public NodePosition Position { get; }
    /// <summary>
    /// Defines the node input and output ports.
    /// 定义节点的输入和输出端口。
    /// </summary>
    public IReadOnlyList<PortDefinition> Ports { get; }
    /// <summary>
    /// Defines the node parameters.
    /// 定义节点参数。
    /// </summary>
    public IReadOnlyList<NodeParameterDefinition> Parameters { get; }
    /// <summary>
    /// Stores the script definition when this is a script node.
    /// 脚本节点对应的脚本定义。
    /// </summary>
    public ScriptNodeDefinition? Script { get; }
    /// <summary>
    /// Stores the node runtime configuration.
    /// 节点运行时配置。
    /// </summary>
    public NodeRuntimeDefinition? Runtime { get; }

    public NodeDefinition WithRuntime(NodeRuntimeDefinition? runtime)
    {
        var createResult = Create(Id, Type, DisplayName, Position, Ports, Parameters, Script, runtime);
        return createResult;
    }

    public static NodeDefinition Create(string id, NodeType type, string displayName, NodePosition? position = null, IEnumerable<PortDefinition>? ports = null, IEnumerable<NodeParameterDefinition>? parameters = null, ScriptNodeDefinition? script = null, NodeRuntimeDefinition? runtime = null)
    {
        var validateResult = Validate(id, nameof(id));
        var validateResult2 = Validate(displayName, nameof(displayName));
        var evaluatedValue = position ?? new NodePosition(0, 0);
        var toArrayResult = (ports ?? []).ToArray();
        var toArrayResult3 = (parameters ?? []).ToArray();
        var nodeDefinition = new NodeDefinition(validateResult, type, validateResult2, evaluatedValue, toArrayResult, toArrayResult3, script, runtime);
        return nodeDefinition;
    }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Node identifiers and display names cannot be empty. 节点标识和显示名称不能为空。", parameterName);
        }

        var trimResult4 = value.Trim();
        return trimResult4;
    }
}
