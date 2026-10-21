using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

public enum AttributeMatch { Exact, IncludeDerived }

/// <summary>Declares pipelines during Initialize; no reflection or runtime handler discovery is involved.</summary>
public sealed class GeneratorBuilder
{
    internal GeneratorBuilder(IncrementalGeneratorInitializationContext context) => Context = context;
    internal IncrementalGeneratorInitializationContext Context { get; }

    public void AddInitializationSource(string hintName, string text) => Context.RegisterInitializationSource(hintName, text);

    public SymbolPipeline<INamedTypeSymbol> ForClassAttribute(string metadataName, AttributeMatch match = AttributeMatch.Exact)
        => new SymbolPipeline<INamedTypeSymbol>(this, metadataName, match, true);

    public SymbolPipeline<IMethodSymbol> ForMethodAttribute(string metadataName, AttributeMatch match = AttributeMatch.Exact)
        => new SymbolPipeline<IMethodSymbol>(this, metadataName, match, false);
}
