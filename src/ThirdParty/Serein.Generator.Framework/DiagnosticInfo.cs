using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

/// <summary>A value-comparable diagnostic snapshot, independent of a compilation.</summary>
public sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    private DiagnosticInfo(DiagnosticDescriptor descriptor, string message, SourceLocation? location,
        EquatableArray<SourceLocation> additionalLocations, EquatableArray<KeyValuePair<string, string?>> properties)
    {
        Descriptor = descriptor;
        Message = message;
        Location = location;
        AdditionalLocations = additionalLocations;
        Properties = properties;
    }
    public DiagnosticDescriptor Descriptor { get; }
    public string Message { get; }
    public SourceLocation? Location { get; }
    public EquatableArray<SourceLocation> AdditionalLocations { get; }
    public EquatableArray<KeyValuePair<string, string?>> Properties { get; }
    public static DiagnosticInfo From(Diagnostic diagnostic) => new DiagnosticInfo(
        new DiagnosticDescriptor(diagnostic.Id, diagnostic.Descriptor.Title, "{0}", diagnostic.Descriptor.Category,
            diagnostic.Severity, diagnostic.Descriptor.IsEnabledByDefault, diagnostic.Descriptor.Description,
            diagnostic.Descriptor.HelpLinkUri, diagnostic.Descriptor.CustomTags.ToArray()),
        diagnostic.GetMessage(CultureInfo.InvariantCulture), SourceLocation.From(diagnostic.Location),
        new EquatableArray<SourceLocation>(diagnostic.AdditionalLocations.Select(SourceLocation.From).OfType<SourceLocation>()),
        new EquatableArray<KeyValuePair<string, string?>>(diagnostic.Properties.OrderBy(property => property.Key, StringComparer.Ordinal)));
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location?.ToLocation(),
        AdditionalLocations.Select(location => location.ToLocation()), Properties.ToImmutableDictionary(), Message);
    public bool Equals(DiagnosticInfo? other) => other is not null && Descriptor.Equals(other.Descriptor)
        && Message == other.Message && Equals(Location, other.Location) && AdditionalLocations.Equals(other.AdditionalLocations) && Properties.Equals(other.Properties);
    public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);
    public override int GetHashCode() => unchecked(Descriptor.GetHashCode() * 397 ^ StringComparer.Ordinal.GetHashCode(Message));
}
