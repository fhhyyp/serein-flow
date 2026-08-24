using System;

// These deliberately mirror the attribute names used by the existing Serein
// library convention. The server scans metadata by attribute suffix, so the
// test library can be built without a dependency on the retired Workbench or
// Serein.Library projects.
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
