using System;

// These attributes are part of the public library metadata contract. They
// live in the shared contracts assembly so uploaded node libraries do not need
// to duplicate (and potentially drift from) the host's definitions.
// 这些 Attributes 属于公开的类库元数据契约，统一放在共享 Contracts 程序集中，避免上传类库重复定义并与宿主产生偏差。
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
    public NodeType NodeType { get; set; } = NodeType.Action;

    public string? AnotherName { get; set; }

    public string? Desc { get; set; }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class NodeParamAttribute : Attribute
{
    public string? Name { get; set; }

    public bool IsExplicit { get; set; } = true;
}

public enum NodeType
{
    Action = 0,
    Flipflop = 1,
}

/// <summary>
/// Names used by the metadata-only library scanner. The scanner consumes this
/// contract instead of embedding attribute type/property names as strings.
/// 供仅元数据扫描器使用的名称；扫描器使用该契约，避免把 Attribute 类型名和属性名直接写成字符串。
/// </summary>
public static class LibraryAttributeContract
{
    public static readonly string FlowLibraryAttributeFullName = GetFullName<FlowLibraryAttribute>();
    public static readonly string FlowNodeAttributeFullName = GetFullName<FlowNodeAttribute>();
    public static readonly string NodeParamAttributeFullName = GetFullName<NodeParamAttribute>();
    public static readonly string ParamArrayAttributeFullName = typeof(ParamArrayAttribute).FullName ?? nameof(ParamArrayAttribute);

    public const string LibraryNamePropertyName = nameof(FlowLibraryAttribute.Name);
    public const string NodeTypePropertyName = nameof(FlowNodeAttribute.NodeType);
    public const string DisplayNamePropertyName = nameof(FlowNodeAttribute.AnotherName);
    public const string DescriptionPropertyName = nameof(FlowNodeAttribute.Desc);
    public const string ParameterNamePropertyName = nameof(NodeParamAttribute.Name);
    public const string IsExplicitPropertyName = nameof(NodeParamAttribute.IsExplicit);

    private static string GetFullName<T>()
        => typeof(T).FullName ?? typeof(T).Name;
}
