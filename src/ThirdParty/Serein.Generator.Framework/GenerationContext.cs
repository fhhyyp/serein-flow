using System.Collections.Generic;
using System;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

/// <summary>Per-invocation output and diagnostic collection. Never shared between runs.</summary>
public sealed class GenerationContext
{
    private readonly List<GeneratedSource> sources = new List<GeneratedSource>();
    private readonly List<DiagnosticInfo> diagnostics = new List<DiagnosticInfo>();
    private readonly HashSet<string> hintNames = new HashSet<string>(StringComparer.Ordinal);
    public GenerationContext(CancellationToken cancellationToken) => CancellationToken = cancellationToken;
    public CancellationToken CancellationToken { get; }
    public void AddSource(string hintName, string text)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (!hintNames.Add(hintName)) throw new InvalidOperationException("Duplicate generated hint name: " + hintName);
        sources.Add(new GeneratedSource(hintName, text));
    }
    public void ReportDiagnostic(Diagnostic diagnostic) => diagnostics.Add(DiagnosticInfo.From(diagnostic));
    public void ReportDiagnostic(DiagnosticInfo diagnostic) => diagnostics.Add(diagnostic);
    internal GenerationResult Complete() => new GenerationResult(new EquatableArray<GeneratedSource>(sources), new EquatableArray<DiagnosticInfo>(diagnostics));
}
