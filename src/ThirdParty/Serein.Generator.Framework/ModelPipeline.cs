using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

public sealed class ModelPipeline<T>
{
    private readonly IncrementalGeneratorInitializationContext context;
    private readonly IncrementalValuesProvider<AnalysisResult<T>> values;

    internal ModelPipeline(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<AnalysisResult<T>> values)
    {
        this.context = context;
        this.values = values;
    }

    public ModelPipeline<T> Named(string trackingName) => new ModelPipeline<T>(context, values.WithTrackingName(trackingName));

    public void Emit(Action<T, GenerationContext> emit, string trackingName)
    {
        var output = values.Select((analysis, token) => GenerationResult.Create(trackingName, result =>
        {
            foreach (var diagnostic in analysis.Diagnostics)
            {
                result.ReportDiagnostic(diagnostic);
            }
            if (analysis.Status == AnalysisStatus.Success)
            {
                emit(analysis.Model, result);
            }
        }, token));
        context.RegisterGeneratedOutput(output, trackingName);
    }

    public CollectedPipeline<T> Collect() => new CollectedPipeline<T>(context, values.Collect());
}
