using Serein.Generator.Framework;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator.Extensions;

internal static class McpHostCacheExtension
{
    internal static McpHostCache? BuildHostCache(this INamedTypeSymbol type, ClassDeclarationSyntax declaration, string assemblyName)
    {
        var attribute = type.GetAttributes().FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == "SereinFlow.Mcp.Framework.McpHostAttribute");
        if (attribute is null) return null;
        var cache = new McpHostCache
        {
            Namespace = SymbolNames.Namespace(type),
            ClassName = type.Name,
            Accessibility = type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            RegistrationMethodName = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "AddMcpTools",
            RegistryTypeName = "global::" + string.Join(".", assemblyName.Split('.').Select(name => "@" + name)) + ".Generated.McpToolRegistry",
            Location = type.Locations.FirstOrDefault()
        };
        if (!type.IsStatic || type.IsGenericType || type.ContainingType is not null || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            cache.Error = "hosts must be top-level nongeneric partial static classes";
        else if (!SyntaxFacts.IsValidIdentifier(cache.RegistrationMethodName))
            cache.Error = "the registration method name must be a valid C# identifier";
        else if (cache.RegistrationMethodName == type.Name
            || cache.RegistrationMethodName is "ConfigureServices" or "ConfigureJsonOptions" or "ConfigureCatalog")
            cache.Error = "the registration method name conflicts with the host class or a configuration hook";
        return cache;
    }
}
