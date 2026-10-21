using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Serein.Generator.Framework;
using SereinFlow.Mcp.Generator.Extensions;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator;

/// <summary>Owns tool semantics and snapshots valid entries before the aggregate pipeline.</summary>
internal static class McpToolAnalyzer
{
    internal static AnalysisResult<McpToolAnalysis> Analyze(SymbolContext<IMethodSymbol> input, AnalysisContext context)
    {
        var cache = input.Symbol.BuildCache(input.Attribute);
        context.ReportDiagnostics(cache.Diagnostics);
        return context.Build(() =>
        {
            var output = new GenerationContext(context.CancellationToken);
            var registry = new McpToolRegistryCache("", ImmutableArray.Create(cache));
            registry.BuildValidationCache();
            var entry = registry.GenerateEntry(cache, output);
            var support = new EquatableArray<GeneratedSource>(registry.GenerateSupport(output));
            var location = SourceLocation.From(input.Location);
            return new McpToolAnalysis(cache.Name, location, entry, support);
        });
    }
}
