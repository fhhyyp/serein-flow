using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Serein.Generator.Framework;

/// <summary>An immutable sequence with element-wise equality for incremental models.</summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly ImmutableArray<T> items;
    public EquatableArray(IEnumerable<T> values) => items = values.ToImmutableArray();
    public int Count => items.IsDefault ? 0 : items.Length;
    public T this[int index] => items[index];
    public bool Equals(EquatableArray<T> other) => this.SequenceEqual(other);
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this) hash = unchecked(hash * 31 + (item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(item)));
        return hash;
    }
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(items.IsDefault ? ImmutableArray<T>.Empty : items)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
