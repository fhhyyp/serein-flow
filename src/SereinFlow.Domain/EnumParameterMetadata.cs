namespace SereinFlow.Domain;

/// <summary>
/// Immutable enum member metadata retained by a node parameter definition.
/// 节点参数定义保留的不可变枚举成员元数据。
/// </summary>
public sealed record EnumValueOption
{
    public EnumValueOption(string name, string numericValue)
    {
        Name = Validate(name, nameof(name));
        NumericValue = Validate(numericValue, nameof(numericValue));
    }

    public string Name { get; }

    public string NumericValue { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Enum option names and values cannot be empty. 枚举选项名称和值不能为空。", parameterName);

        return value.Trim();
    }
}

/// <summary>
/// Metadata scanned from a library package without loading the assembly.
/// 由类库包安全扫描得到的元数据，不需要加载程序集。
/// </summary>
public sealed record EnumParameterMetadata
{
    public EnumParameterMetadata(
        string typeName,
        bool isFlags,
        string underlyingType,
        IEnumerable<EnumValueOption> options)
    {
        TypeName = Validate(typeName, nameof(typeName));
        IsFlags = isFlags;
        UnderlyingType = Validate(underlyingType, nameof(underlyingType));
        Options = (options ?? throw new ArgumentNullException(nameof(options)))
            .ToArray();

        if (Options.Count == 0)
            throw new ArgumentException("Enum metadata must contain at least one option. 枚举元数据至少包含一个选项。", nameof(options));
        if (Options.Select(static option => option.Name).Distinct(StringComparer.Ordinal).Count() != Options.Count)
            throw new ArgumentException("Enum option names must be unique. 枚举选项名称必须唯一。", nameof(options));
    }

    public string TypeName { get; }

    public bool IsFlags { get; }

    public string UnderlyingType { get; }

    public IReadOnlyList<EnumValueOption> Options { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Enum metadata fields cannot be empty. 枚举元数据字段不能为空。", parameterName);

        return value.Trim();
    }
}
