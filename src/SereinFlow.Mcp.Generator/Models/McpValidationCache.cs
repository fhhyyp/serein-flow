using System.Collections.Generic;

namespace SereinFlow.Mcp.Generator.Models;

/// <summary>请求对象及其嵌套对象的直接访问校验模型。</summary>
internal sealed class McpValidationCache(string typeName, string methodName)
{
    public string TypeName { get; } = typeName;
    public string MethodName { get; } = methodName;
    public string? BindingMethodName { get; set; }
    public List<McpPropertyBindingCache> Bindings { get; } = new List<McpPropertyBindingCache>();
    public List<McpValidationPropertyCache> Properties { get; } = new List<McpValidationPropertyCache>();
}

internal sealed class McpValidationPropertyCache(string propertyName, string methodName, bool collection, bool dictionary)
{
    public string PropertyName { get; } = propertyName;
    public string MethodName { get; } = methodName;
    public bool IsCollection { get; } = collection;
    public bool IsDictionary { get; } = dictionary;
}
