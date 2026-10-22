using Serein.Generator.Framework;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator.Extensions;

/// <summary>解析方法、特性和依赖，构建用于生成代码的工具数据模型。</summary>
internal static class McpToolCacheExtension
{
    private const string ContextAttribute = "SereinFlow.Mcp.Framework.McpContextAttribute";
    private static readonly DiagnosticDescriptor InvalidSignature = new DiagnosticDescriptor(
        "MCPGEN002", "Invalid MCP tool signature", "Tool '{0}': {1}", "McpGeneration", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidConstructor = new DiagnosticDescriptor(
        "MCPGEN003", "Invalid MCP tool constructor", "Tool class '{0}' must have exactly one accessible instance constructor", "McpGeneration", DiagnosticSeverity.Error, true);

    internal static McpToolCache BuildCache(this IMethodSymbol method, AttributeData attribute)
    {
        var cache = new McpToolCache
        {
            Name = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? string.Empty,
            Location = method.Locations.FirstOrDefault()
        };
        var error = CheckSignature(method, cache.Name);
        if (error is not null)
        {
            cache.Diagnostics.Add(Diagnostic.Create(InvalidSignature, cache.Location, cache.Name, error));
            return cache;
        }
        if (!ConfigureConstructor(method, cache)) return cache;
        var request = method.Parameters.SingleOrDefault(parameter => !IsContext(parameter));
        cache.HandlerTypeName = TypeName(method.ContainingType);
        cache.MethodName = Escape(method.Name);
        cache.IsStatic = method.IsStatic;
        cache.RequestTypeName = request is null ? null : TypeName(request.Type);
        cache.RequestType = request?.Type;
        cache.MetadataExpression = CSharpLiteral.RenderAttribute(attribute);
        cache.InvocationArguments = string.Join(", ", method.Parameters.Select(InvocationArgument));
        return cache;
    }

    private static bool ConfigureConstructor(IMethodSymbol method, McpToolCache cache)
    {
        if (method.IsStatic) return true;
        var constructors = method.ContainingType.InstanceConstructors.Where(IsAccessible).ToArray();
        if (constructors.Length != 1 || constructors[0].Parameters.Any(parameter => parameter.RefKind != RefKind.None))
        {
            cache.Diagnostics.Add(Diagnostic.Create(InvalidConstructor, cache.Location, method.ContainingType.Name));
            return false;
        }

        cache.ConstructorArguments = string.Join(", ", constructors[0].Parameters.Select(parameter =>
            $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{TypeName(parameter.Type)}>(provider)"));
        return true;
    }

    private static string InvocationArgument(IParameterSymbol parameter)
        => IsCancellation(parameter.Type) ? "cancellationToken"
            : IsContext(parameter) ? $"context.GetValue<{TypeName(parameter.Type)}>()" : "request";

    private static string? CheckSignature(IMethodSymbol method, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "a nonempty constant name is required";
        if (!IsAccessible(method)) return "tool methods must be public or internal";
        for (var owner = method.ContainingType; owner is not null; owner = owner.ContainingType)
        {
            if (owner.IsGenericType || !IsAccessible(owner)) return "containing types must be accessible and nongeneric";
        }
        if (method.IsGenericMethod || method.ReturnType is not INamedTypeSymbol result
            || result.OriginalDefinition.ToDisplayString() != "System.Threading.Tasks.Task<TResult>") return "return Task<T> from a nongeneric method";
        if (!method.IsStatic && method.ContainingType.IsAbstract) return "instance tool classes cannot be abstract";
        if (method.Parameters.Any(parameter => parameter.RefKind != RefKind.None)) return "ref, in and out parameters are unsupported";
        if (method.Parameters.Where(IsContext).GroupBy(parameter => TypeName(parameter.Type)).Any(group => group.Count() > 1)) return "context parameters must be unique";
        var requests = method.Parameters.Where(parameter => !IsContext(parameter)).ToArray();
        if (requests.Length > 1) return "accept at most one request object";
        if (requests.Length == 1)
        {
            var request = requests[0].Type;
            if (request.TypeKind != TypeKind.Class || request.SpecialType is SpecialType.System_String or SpecialType.System_Object
                || request.IsAbstract || request.AllInterfaces.Any(item => item.SpecialType == SpecialType.System_Collections_IEnumerable))
                return "the request must be a typed object contract; raw JsonElement arguments are unsupported";
            if (request.UsesPropertyBinding() && request is INamedTypeSymbol named)
            {
                if (!named.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0 && IsAccessible(constructor)))
                    return "McpBindProperties requests require an accessible parameterless constructor";
                for (var current = named; current is not null; current = current.BaseType)
                    if (current.GetMembers().OfType<IPropertySymbol>().Any(property => property.IsRequired || property.SetMethod?.IsInitOnly == true))
                        return "McpBindProperties requests use writable set properties and McpParameter.Required";
            }
        }
        return null;
    }

    private static bool IsAccessible(ISymbol symbol) => symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
    private static bool IsCancellation(ITypeSymbol type) => TypeName(type) == "global::System.Threading.CancellationToken";
    private static bool IsContext(IParameterSymbol parameter) => IsCancellation(parameter.Type)
        || parameter.GetAttributes().Any(attribute => AttributeReader.IsOrDerivesFrom(attribute.AttributeClass, ContextAttribute))
        || parameter.Type.GetAttributes().Any(attribute => AttributeReader.IsOrDerivesFrom(attribute.AttributeClass, ContextAttribute));
    private static string TypeName(ITypeSymbol type) => SymbolNames.TypeName(type);
    private static string Escape(string name) => SymbolNames.Escape(name);
}
