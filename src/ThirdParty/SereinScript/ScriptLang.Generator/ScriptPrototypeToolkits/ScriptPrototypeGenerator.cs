using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Generator.Framework;

namespace ScriptLang.Generator;

/// <summary>Generates script prototypes using the shared incremental generator framework.</summary>
[Generator]
public sealed partial class ScriptPrototypeGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterInitializationSource("ScriptPrototypeToolkits.g.cs", _attributeText);
        var results = context.SyntaxProvider.ForAttributeWithMetadataName(PrototypeModel.ExtensionAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntax, token) => GenerationResult.Create(syntax.TargetSymbol.Name, output =>
            {
                var type = (INamedTypeSymbol)syntax.TargetSymbol;
                var declaration = (ClassDeclarationSyntax)syntax.TargetNode;
                // Only the declaration containing the first marker emits for a partial type.
                var attribute = type.GetAttributes().First(item => item.AttributeClass?.ToDisplayString() == PrototypeModel.ExtensionAttribute);
                var marker = attribute.ApplicationSyntaxReference;
                if (marker is not null && (marker.SyntaxTree != declaration.SyntaxTree || !declaration.Span.Contains(marker.Span))) return;
                if (type.IsGenericType || type.ContainingType is not null || type.IsStatic || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    PrototypeEmitter.ReportUnsupportedType(output, declaration.Identifier.GetLocation(), type.Name);
                    return;
                }
                var model = new PrototypeModel(type, attribute);
                output.AddSource(SymbolNames.HintName(type), model.GenerateCode(output, declaration.Identifier.GetLocation()));
            }, token));
        context.RegisterGeneratedOutput(results, "ScriptPrototypeOutput");
    }
}
