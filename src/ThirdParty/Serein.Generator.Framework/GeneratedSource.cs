using System;

namespace Serein.Generator.Framework;

/// <summary>An immutable generated file.</summary>
public sealed class GeneratedSource : IEquatable<GeneratedSource>
{
    public GeneratedSource(string hintName, string text)
    {
        HintName = hintName;
        Text = text;
    }
    public string HintName { get; }
    public string Text { get; }
    public bool Equals(GeneratedSource? other) => other is not null && HintName == other.HintName && Text == other.Text;
    public override bool Equals(object? obj) => Equals(obj as GeneratedSource);
    public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(HintName) * 397 ^ StringComparer.Ordinal.GetHashCode(Text));
}
