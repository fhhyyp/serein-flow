using Serein.Generator.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator.Extensions;

/// <summary>缓存请求属性图，生成阶段通过属性直接访问嵌套对象。</summary>
internal static class McpValidationCacheExtension
{
    internal static McpToolRegistryCache BuildValidationCache(this McpToolRegistryCache registry)
    {
        var byType = new Dictionary<string, McpValidationCache>(StringComparer.Ordinal);
        foreach (var tool in registry.Tools.OrderBy(tool => tool.Name, StringComparer.Ordinal))
        {
            if (tool.Diagnostics.Count != 0 || tool.RequestType is null) continue;
            var validation = AddType(tool.RequestType, registry, byType);
            tool.ValidationMethodName = validation.MethodName;
            tool.BindingMethodName = validation.BindingMethodName;
        }
        return registry;
    }

    private static McpValidationCache AddType(ITypeSymbol type, McpToolRegistryCache registry, Dictionary<string, McpValidationCache> byType)
    {
        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (byType.TryGetValue(name, out var existing)) return existing;
        var cache = new McpValidationCache(name, SymbolNames.StableIdentifier("ValidateRequest", name));
        byType.Add(name, cache);
        registry.Validations.Add(cache);
        type.BuildPropertyBindingCache(cache);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (property.IsStatic || property.IsIndexer || property.GetMethod?.DeclaredAccessibility != Accessibility.Public || !names.Add(property.Name)) continue;
                var childType = property.Type;
                var collection = false;
                var dictionary = false;
                if (childType is IArrayTypeSymbol array)
                {
                    childType = array.ElementType;
                    collection = true;
                }
                else if (childType is INamedTypeSymbol named)
                {
                    var contracts = named.AllInterfaces.Concat(new[] { named }).ToArray();
                    var map = contracts.FirstOrDefault(item => item.OriginalDefinition.ToDisplayString() is
                        "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>");
                    var sequence = contracts.FirstOrDefault(item => item.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
                    if (map is not null)
                    {
                        childType = map.TypeArguments[1];
                        collection = dictionary = true;
                    }
                    else if (sequence is not null && childType.SpecialType != SpecialType.System_String)
                    {
                        childType = sequence.TypeArguments[0];
                        collection = true;
                    }
                }
                var childNamespace = childType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (childType.TypeKind != TypeKind.Class || childType.SpecialType != SpecialType.None
                    || childNamespace == "System" || childNamespace.StartsWith("System.", StringComparison.Ordinal)) continue;
                var child = AddType(childType, registry, byType);
                cache.Properties.Add(new McpValidationPropertyCache(property.Name, child.MethodName, collection, dictionary));
            }
        }
        return cache;
    }
}
