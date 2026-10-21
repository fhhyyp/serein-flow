using System;
using Serein.Generator.Framework;

namespace SereinFlow.Mcp.Generator.Models;

/// <summary>Immutable semantic boundary: no symbols, syntax trees or mutable caches survive here.</summary>
internal sealed class McpToolAnalysis(string name, SourceLocation? location, string entry,
    EquatableArray<GeneratedSource> support, GenerationResult result) : IEquatable<McpToolAnalysis>
{
    public string Name { get; } = name;
    public SourceLocation? Location { get; } = location;
    public string Entry { get; } = entry;
    public EquatableArray<GeneratedSource> Support { get; } = support;
    public GenerationResult Result { get; } = result;
    public bool Equals(McpToolAnalysis? other) => other is not null && Name == other.Name && Equals(Location, other.Location)
        && Entry == other.Entry && Support.Equals(other.Support) && Result.Equals(other.Result);
    public override bool Equals(object? obj) => Equals(obj as McpToolAnalysis);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
}
