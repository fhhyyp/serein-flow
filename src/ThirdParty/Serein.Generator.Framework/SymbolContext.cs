using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Serein.Generator.Framework;

/// <summary>Semantic input valid only during analysis. Do not store this context in incremental models.</summary>
public sealed class SymbolContext<TSymbol> where TSymbol : class, ISymbol
{
    internal SymbolContext(TSymbol symbol, SyntaxNode declaration, AttributeData attribute, SemanticModel semanticModel, CancellationToken token)
    {
        Symbol = symbol;
        Declaration = declaration;
        Attribute = attribute;
        SemanticModel = semanticModel;
        CancellationToken = token;
    }

    public TSymbol Symbol { get; }
    public SyntaxNode Declaration { get; }
    public AttributeData Attribute { get; }
    public SemanticModel SemanticModel { get; }
    public Compilation Compilation => SemanticModel.Compilation;
    public CancellationToken CancellationToken { get; }
    public Location Location => Declaration switch
    {
        ClassDeclarationSyntax type => type.Identifier.GetLocation(),
        MethodDeclarationSyntax method => method.Identifier.GetLocation(),
        _ => Declaration.GetLocation()
    };
}
