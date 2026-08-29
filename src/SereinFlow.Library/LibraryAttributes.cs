using System;

// These types are the standalone public SDK contract for uploaded node
// libraries. Keep their namespaces and member names stable: the server reads
// them from PE metadata without loading the uploaded assembly.
// 这些类型是上传节点类库使用的独立公开 SDK 契约。命名空间和成员名称必须保持稳定，
// 服务端会在不加载上传程序集的情况下从 PE 元数据读取它们。
namespace SereinFlow.Core.Api;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FlowLibraryAttribute : Attribute
{
    public FlowLibraryAttribute()
    {
    }

    public FlowLibraryAttribute(string name) => Name = name;

    public string? Name { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FlowNodeAttribute : Attribute
{
    /// <summary>
    /// Stable node contract identifier. When omitted, SereinFlow derives it
    /// from the library name (or declaring class name) and CLR method name.
    /// 稳定节点契约标识。未指定时，SereinFlow 根据类库名称（或声明类名）和 CLR 方法名派生。
    /// </summary>
    public string? Id { get; set; }

    public NodeType NodeType { get; set; } = NodeType.Action;

    public string? AnotherName { get; set; }

    public string? Desc { get; set; }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class NodeParamAttribute : Attribute
{
    /// <summary>
    /// Stable parameter contract identifier. When omitted, SereinFlow uses the
    /// CLR parameter name.
    /// 稳定参数契约标识。未指定时，SereinFlow 使用 CLR 参数名。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Previous stable IDs accepted when a parameter is intentionally renamed.
    /// 参数有意改名时可接受的旧稳定 ID。
    /// </summary>
    public string[]? Aliases { get; set; }

    public string? Name { get; set; }

    public bool IsExplicit { get; set; } = true;
}

public enum NodeType
{
    Action = 0,
    Flipflop = 1,
}

/// <summary>
/// Names used by the metadata-only library scanner.
/// 供仅元数据扫描器使用的名称。
/// </summary>
public static class LibraryAttributeContract
{
    public static readonly string FlowLibraryAttributeFullName = GetFullName<FlowLibraryAttribute>();
    public static readonly string FlowNodeAttributeFullName = GetFullName<FlowNodeAttribute>();
    public static readonly string NodeParamAttributeFullName = GetFullName<NodeParamAttribute>();
    public static readonly string ParamArrayAttributeFullName = typeof(ParamArrayAttribute).FullName ?? nameof(ParamArrayAttribute);

    public const string LibraryNamePropertyName = nameof(FlowLibraryAttribute.Name);
    public const string NodeContractIdPropertyName = nameof(FlowNodeAttribute.Id);
    public const string NodeTypePropertyName = nameof(FlowNodeAttribute.NodeType);
    public const string DisplayNamePropertyName = nameof(FlowNodeAttribute.AnotherName);
    public const string DescriptionPropertyName = nameof(FlowNodeAttribute.Desc);
    public const string ParameterContractIdPropertyName = nameof(NodeParamAttribute.Id);
    public const string ParameterAliasesPropertyName = nameof(NodeParamAttribute.Aliases);
    public const string ParameterNamePropertyName = nameof(NodeParamAttribute.Name);
    public const string IsExplicitPropertyName = nameof(NodeParamAttribute.IsExplicit);

    private static string GetFullName<T>()
        => typeof(T).FullName ?? typeof(T).Name;
}
