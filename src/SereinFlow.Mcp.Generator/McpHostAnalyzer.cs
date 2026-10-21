using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serein.Generator.Framework;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator;

internal static class McpHostAnalyzer
{
    internal static SymbolRule<INamedTypeSymbol> RegistrationName { get; } = new SymbolRule<INamedTypeSymbol>(input =>
    {
        var name = GetRegistrationName(input.Attribute);
        if (!SyntaxFacts.IsValidIdentifier(name))
        {
            return "the registration method name must be a valid C# identifier";
        }
        if (name == input.Symbol.Name || name is "ConfigureServices" or "ConfigureJsonOptions" or "ConfigureCatalog")
        {
            return "the registration method name conflicts with the host class or a configuration hook";
        }
        return null;
    });

    internal static AnalysisResult<McpHostCache> Analyze(SymbolContext<INamedTypeSymbol> input, AnalysisContext context)
    {
        var assemblyName = input.Compilation.AssemblyName ?? "McpHost";
        var registryType = "global::" + string.Join(".", assemblyName.Split('.').Select(SymbolNames.Escape)) + ".Generated.McpToolRegistry";
        var accessibility = input.Symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var cache = new McpHostCache(SymbolNames.Namespace(input.Symbol), input.Symbol.Name, accessibility,
            GetRegistrationName(input.Attribute), registryType);
        return context.Success(cache);
    }

    private static string GetRegistrationName(AttributeData attribute)
        => attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "AddMcpTools";
}
