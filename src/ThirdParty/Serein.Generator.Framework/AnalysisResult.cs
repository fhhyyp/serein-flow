using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

public enum AnalysisStatus { Skip, Success, Failure }

/// <summary>An immutable, value-comparable semantic result. Only successful results contain models.</summary>
public sealed class AnalysisResult<T> : IEquatable<AnalysisResult<T>>
{
    private readonly T model;

    internal AnalysisResult(AnalysisStatus status, T model, EquatableArray<DiagnosticInfo> diagnostics)
    {
        Status = status;
        this.model = model;
        Diagnostics = diagnostics;
    }

    public AnalysisStatus Status { get; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; }
    public T Model => Status == AnalysisStatus.Success ? model : throw new InvalidOperationException("Only successful analysis has a model.");

    public bool Equals(AnalysisResult<T>? other)
    {
        return other is not null && Status == other.Status && Diagnostics.Equals(other.Diagnostics)
            && (Status != AnalysisStatus.Success || EqualityComparer<T>.Default.Equals(model, other.model));
    }

    public override bool Equals(object? obj) => Equals(obj as AnalysisResult<T>);
    public override int GetHashCode() => unchecked((int)Status * 397 ^ Diagnostics.GetHashCode()
        ^ (Status == AnalysisStatus.Success && model is not null ? EqualityComparer<T>.Default.GetHashCode(model) : 0));
}

/// <summary>Invocation-local diagnostics. Errors stop model construction; warnings remain compatible with success.</summary>
public sealed class AnalysisContext
{
    private readonly List<DiagnosticInfo> diagnostics = new List<DiagnosticInfo>();

    public AnalysisContext(CancellationToken cancellationToken) => CancellationToken = cancellationToken;
    public CancellationToken CancellationToken { get; }
    public bool HasErrors => diagnostics.Any(item => item.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);

    public void ReportDiagnostic(Diagnostic diagnostic) => diagnostics.Add(DiagnosticInfo.From(diagnostic));
    public void ReportDiagnostic(DiagnosticInfo diagnostic) => diagnostics.Add(diagnostic);

    public void ReportDiagnostics(IEnumerable<Diagnostic> values)
    {
        foreach (var diagnostic in values)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ReportDiagnostic(diagnostic);
        }
    }

    public AnalysisResult<T> Success<T>(T model)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (HasErrors)
        {
            return Failure<T>();
        }
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }
        return new AnalysisResult<T>(AnalysisStatus.Success, model, Snapshot());
    }

    public AnalysisResult<T> Build<T>(Func<T> build)
    {
        CancellationToken.ThrowIfCancellationRequested();
        return HasErrors ? Failure<T>() : Success(build());
    }

    public AnalysisResult<T> Skip<T>()
    {
        CancellationToken.ThrowIfCancellationRequested();
        return HasErrors ? Failure<T>() : new AnalysisResult<T>(AnalysisStatus.Skip, default!, Snapshot());
    }

    public AnalysisResult<T> Failure<T>()
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (!HasErrors)
        {
            throw new InvalidOperationException("Failed analysis requires an error diagnostic.");
        }
        return new AnalysisResult<T>(AnalysisStatus.Failure, default!, Snapshot());
    }

    internal AnalysisResult<T> Complete<T>(AnalysisResult<T> result)
    {
        var merged = diagnostics.Concat(result.Diagnostics).Distinct();
        var values = new EquatableArray<DiagnosticInfo>(merged);
        var hasErrors = values.Any(item => item.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);
        var status = hasErrors ? AnalysisStatus.Failure : result.Status;
        var model = status == AnalysisStatus.Success ? result.Model : default!;
        return new AnalysisResult<T>(status, model, values);
    }

    private EquatableArray<DiagnosticInfo> Snapshot() => new EquatableArray<DiagnosticInfo>(diagnostics);
}
