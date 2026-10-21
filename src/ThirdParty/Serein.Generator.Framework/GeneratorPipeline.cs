using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Serein.Generator.Framework;

/// <summary>Small composition helpers; domain generators retain control of discovery and aggregation.</summary>
public static class GeneratorPipeline
{
    public static void RegisterInitializationSource(this IncrementalGeneratorInitializationContext context, string hintName, string text)
        => context.RegisterPostInitializationOutput(output => output.AddSource(hintName, SourceText.From(text, Encoding.UTF8)));

    public static void RegisterGeneratedOutput(this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<GenerationResult> results, string trackingName)
        => context.RegisterSourceOutput(results.WithTrackingName(trackingName), static (output, result) => Emit(output, result));

    public static void RegisterGeneratedOutput(this IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<GenerationResult> result, string trackingName)
        => context.RegisterSourceOutput(result.WithTrackingName(trackingName), static (output, value) => Emit(output, value));

    private static void Emit(SourceProductionContext context, GenerationResult result)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        foreach (var diagnostic in result.Diagnostics) context.ReportDiagnostic(diagnostic.ToDiagnostic());
        foreach (var source in result.Sources) context.AddSource(source.HintName, SourceText.From(source.Text, Encoding.UTF8));
    }
}
