using System;

namespace SereinFlow.Mcp.Generator.Models;

/// <summary>Immutable host registration model; semantic symbols remain inside analysis.</summary>
internal sealed class McpHostCache(string @namespace, string className, string accessibility,
    string registrationMethodName, string registryTypeName) : IEquatable<McpHostCache>
{
    public string Namespace { get; } = @namespace;
    public string ClassName { get; } = className;
    public string Accessibility { get; } = accessibility;
    public string RegistrationMethodName { get; } = registrationMethodName;
    public string RegistryTypeName { get; } = registryTypeName;
    public string HintName => Namespace + "." + ClassName + ".g.cs";
    public bool Equals(McpHostCache? other) => other is not null && Namespace == other.Namespace && ClassName == other.ClassName
        && Accessibility == other.Accessibility && RegistrationMethodName == other.RegistrationMethodName && RegistryTypeName == other.RegistryTypeName;
    public override bool Equals(object? obj) => Equals(obj as McpHostCache);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(HintName);
}
