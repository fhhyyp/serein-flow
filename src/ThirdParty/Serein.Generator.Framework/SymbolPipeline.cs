using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Serein.Generator.Framework;

/// <summary>Discovery and rules are composed before analysis so symbols never escape into the value pipeline.</summary>
public sealed class SymbolPipeline<TSymbol> where TSymbol : class, ISymbol
{
    private readonly GeneratorBuilder builder;
    private readonly string metadataName;
    private readonly AttributeMatch match;
    private readonly bool classes;
    private readonly ImmutableArray<RuleRegistration> rules;

    internal SymbolPipeline(GeneratorBuilder builder, string metadataName, AttributeMatch match, bool classes)
        : this(builder, metadataName, match, classes, ImmutableArray<RuleRegistration>.Empty) { }

    private SymbolPipeline(GeneratorBuilder builder, string metadataName, AttributeMatch match, bool classes, ImmutableArray<RuleRegistration> rules)
    {
        this.builder = builder;
        this.metadataName = metadataName;
        this.match = match;
        this.classes = classes;
        this.rules = rules;
    }

    /// <summary>The descriptor receives the symbol name and the first failed rule's reason.</summary>
    public SymbolPipeline<TSymbol> Require(DiagnosticDescriptor diagnostic, params SymbolRule<TSymbol>[] requirements)
    {
        var registration = new RuleRegistration(diagnostic, ImmutableArray.CreateRange(requirements));
        return new SymbolPipeline<TSymbol>(builder, metadataName, match, classes, rules.Add(registration));
    }

    public ModelPipeline<TModel> Analyze<TModel>(Func<SymbolContext<TSymbol>, AnalysisContext, AnalysisResult<TModel>> analyze)
    {
        var provider = match == AttributeMatch.Exact
            ? builder.Context.SyntaxProvider.ForAttributeWithMetadataName(metadataName,
                (node, _) => IsCandidate(node),
                (input, token) => Transform(input.TargetSymbol, input.TargetNode, input.SemanticModel, analyze, token))
            : builder.Context.SyntaxProvider.CreateSyntaxProvider(
                (node, _) => IsCandidate(node),
                (input, token) => Transform(input.SemanticModel.GetDeclaredSymbol(input.Node, token), input.Node, input.SemanticModel, analyze, token));
        var routed = provider.Where(result => result.Status != AnalysisStatus.Skip || result.Diagnostics.Count != 0);
        return new ModelPipeline<TModel>(builder.Context, routed);
    }

    private bool IsCandidate(SyntaxNode node)
    {
        return classes ? node is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count != 0
            : node is MethodDeclarationSyntax method && method.AttributeLists.Count != 0;
    }

    private AnalysisResult<TModel> Transform<TModel>(ISymbol? symbol, SyntaxNode declaration, SemanticModel semanticModel,
        Func<SymbolContext<TSymbol>, AnalysisContext, AnalysisResult<TModel>> analyze, CancellationToken token)
    {
        var analysis = new AnalysisContext(token);
        AnalysisResult<TModel>? result = null;
        var capture = GenerationResult.Create(symbol?.Name ?? metadataName, output =>
        {
            if (symbol is not TSymbol typed)
            {
                result = analysis.Skip<TModel>();
                return;
            }
            var attribute = AttributeReader.Find(typed, metadataName, match == AttributeMatch.IncludeDerived);
            if (attribute is null)
            {
                result = analysis.Skip<TModel>();
                return;
            }
            // A symbol can expose attributes from multiple partial declarations.
            // Only the part containing the first matching marker owns this target.
            var marker = attribute.ApplicationSyntaxReference;
            if (marker is not null && (marker.SyntaxTree != declaration.SyntaxTree || !declaration.Span.Contains(marker.Span)))
            {
                result = analysis.Skip<TModel>();
                return;
            }
            var input = new SymbolContext<TSymbol>(typed, declaration, attribute, semanticModel, token);
            foreach (var registration in rules)
            {
                foreach (var rule in registration.Rules)
                {
                    token.ThrowIfCancellationRequested();
                    var reason = rule.Validate(input);
                    if (reason is null)
                    {
                        continue;
                    }
                    var diagnostic = Diagnostic.Create(registration.Diagnostic, input.Location, typed.Name, reason);
                    analysis.ReportDiagnostic(diagnostic);
                    break;
                }
            }
            result = analysis.HasErrors ? analysis.Failure<TModel>() : analyze(input, analysis);
            if (result is null)
            {
                throw new InvalidOperationException("The analyzer must return Skip, Success or Failure.");
            }
            result = analysis.Complete(result);
        }, token);
        if (capture.Diagnostics.Count != 0)
        {
            foreach (var diagnostic in capture.Diagnostics)
            {
                analysis.ReportDiagnostic(diagnostic);
            }
            return analysis.Failure<TModel>();
        }
        return result!;
    }

    private sealed class RuleRegistration(DiagnosticDescriptor diagnostic, ImmutableArray<SymbolRule<TSymbol>> rules)
    {
        public DiagnosticDescriptor Diagnostic { get; } = diagnostic;
        public ImmutableArray<SymbolRule<TSymbol>> Rules { get; } = rules;
    }
}
