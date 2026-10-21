using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

/// <summary>Aggregates successful targets while retaining diagnostics from failed or skipped targets.</summary>
public sealed class CollectedPipeline<T>
{
    private readonly IncrementalGeneratorInitializationContext context;
    private readonly IncrementalValueProvider<ImmutableArray<AnalysisResult<T>>> values;
    private readonly Func<T, string>? key;
    private readonly Func<T, SourceLocation?>? location;
    private readonly DiagnosticDescriptor? duplicate;

    internal CollectedPipeline(IncrementalGeneratorInitializationContext context, IncrementalValueProvider<ImmutableArray<AnalysisResult<T>>> values,
        Func<T, string>? key = null, DiagnosticDescriptor? duplicate = null, Func<T, SourceLocation?>? location = null)
    {
        this.context = context;
        this.values = values;
        this.key = key;
        this.duplicate = duplicate;
        this.location = location;
    }

    /// <summary>Conflicting targets are diagnosed individually and excluded; unrelated targets continue.</summary>
    public CollectedPipeline<T> UniqueBy(Func<T, string> keySelector, DiagnosticDescriptor diagnostic, Func<T, SourceLocation?> locationSelector)
        => new CollectedPipeline<T>(context, values, keySelector, diagnostic, locationSelector);

    public void Emit(Action<EquatableArray<T>, GenerationContext> emit, string trackingName)
        => EmitWithAssemblyName((models, _, output) => emit(models, output), trackingName);

    public void EmitWithAssemblyName(Action<EquatableArray<T>, string, GenerationContext> emit, string trackingName, string fallbackAssemblyName = "Host")
    {
        var assembly = context.CompilationProvider.Select((compilation, _) => compilation.AssemblyName ?? fallbackAssemblyName);
        var output = values.Combine(assembly).Select((input, token) => GenerationResult.Create(trackingName, result =>
        {
            foreach (var analysis in input.Left)
            {
                foreach (var diagnostic in analysis.Diagnostics)
                {
                    result.ReportDiagnostic(diagnostic);
                }
            }
            var successful = input.Left.Where(item => item.Status == AnalysisStatus.Success).Select(item => item.Model);
            if (key is not null)
            {
                successful = successful.GroupBy(key, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                    .SelectMany(group =>
                    {
                        if (group.Count() == 1)
                        {
                            return group;
                        }
                        foreach (var item in group)
                        {
                            var diagnostic = Diagnostic.Create(duplicate!, location!(item)?.ToLocation(), group.Key);
                            result.ReportDiagnostic(diagnostic);
                        }
                        return Enumerable.Empty<T>();
                    });
            }
            var models = new EquatableArray<T>(successful);
            emit(models, input.Right, result);
        }, token));
        context.RegisterGeneratedOutput(output, trackingName);
    }
}
