using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Serein.Generator.Framework;

/// <summary>A diagnostic location without retaining a syntax tree or compilation.</summary>
public sealed class SourceLocation : IEquatable<SourceLocation>
{
    private SourceLocation(string path, TextSpan span, LinePositionSpan lines)
    {
        Path = path;
        Span = span;
        Lines = lines;
    }
    public string Path { get; }
    public TextSpan Span { get; }
    public LinePositionSpan Lines { get; }
    public static SourceLocation? From(Location? location)
    {
        if (location is null || location == Location.None || location.Kind == LocationKind.MetadataFile) return null;
        var lineSpan = location.GetLineSpan();
        return new SourceLocation(lineSpan.Path, location.SourceSpan, lineSpan.Span);
    }
    public Location ToLocation() => Location.Create(Path, Span, Lines);
    public bool Equals(SourceLocation? other) => other is not null && Path == other.Path && Span == other.Span && Lines == other.Lines;
    public override bool Equals(object? obj) => Equals(obj as SourceLocation);
    public override int GetHashCode() => unchecked((StringComparer.Ordinal.GetHashCode(Path) * 397 ^ Span.GetHashCode()) * 397 ^ Lines.GetHashCode());
}
