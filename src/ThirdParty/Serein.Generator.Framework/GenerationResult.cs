using System;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

/// <summary>Immutable output boundary supporting incremental reuse without Roslyn symbols.</summary>
public sealed class GenerationResult : IEquatable<GenerationResult>
{
    private static readonly DiagnosticDescriptor UnexpectedFailure = new DiagnosticDescriptor(
        "SGENFW001", "Source generation failed", "Generation of '{0}' failed: {1}", "SourceGeneration", DiagnosticSeverity.Error, true);
    internal GenerationResult(EquatableArray<GeneratedSource> sources, EquatableArray<DiagnosticInfo> diagnostics)
    {
        Sources = sources;
        Diagnostics = diagnostics;
    }
    public EquatableArray<GeneratedSource> Sources { get; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; }
    public static GenerationResult Create(string target, Action<GenerationContext> generate, CancellationToken cancellationToken = default)
    {
        var context = new GenerationContext(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            generate(context);
            cancellationToken.ThrowIfCancellationRequested();
            return context.Complete();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Discard partially emitted sources; failures must remain visible to the consumer.
            var failed = new GenerationContext(cancellationToken);
            failed.ReportDiagnostic(Diagnostic.Create(UnexpectedFailure, Location.None, target, exception.ToString()));
            return failed.Complete();
        }
    }
    public bool Equals(GenerationResult? other) => other is not null && Sources.Equals(other.Sources) && Diagnostics.Equals(other.Diagnostics);
    public override bool Equals(object? obj) => Equals(obj as GenerationResult);
    public override int GetHashCode() => unchecked(Sources.GetHashCode() * 397 ^ Diagnostics.GetHashCode());
}
