using Serein.Generator.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator.Extensions;

internal static class McpPropertyBindingCacheExtension
{
    internal static bool UsesPropertyBinding(this ITypeSymbol type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
            if (current.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "SereinFlow.Mcp.Framework.McpBindPropertiesAttribute")) return true;
        return false;
    }

    internal static void BuildPropertyBindingCache(this ITypeSymbol type, McpValidationCache cache)
    {
        if (!type.UsesPropertyBinding()) return;
        cache.BindingMethodName = "Bind" + cache.MethodName;
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            foreach (var property in current
                .GetMembers()
                .OfType<IPropertySymbol>()
                .OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (property.IsStatic || property.IsIndexer || property.SetMethod?.DeclaredAccessibility != Accessibility.Public || !names.Add(property.Name)) continue;
                var attribute = property.GetAttributes().FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == "SereinFlow.Mcp.Framework.McpParameterAttribute");
                var parameter = attribute is null ? "new global::SereinFlow.Mcp.Framework.McpParameterOptions()" : CSharpLiteral.RenderAttribute(attribute, "global::SereinFlow.Mcp.Framework.McpParameterOptions");
                var jsonName = property.GetAttributes().FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute")?.ConstructorArguments.FirstOrDefault().Value as string;
                var literal = SymbolDisplay.FormatLiteral(property.Name, true);
                var nullable = property.Type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                var scalar = nullable ? ((INamedTypeSymbol)property.Type).TypeArguments[0] : property.Type;
                var scalarName = scalar.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string read;
                if (scalar.TypeKind == TypeKind.Enum) read = $"Enum<{scalarName}>(value, name, parameter)";
                else if (scalarName == "global::System.Guid") read = nullable ? "ReadGuid(value, name, parameter)" : "RequiredGuid(value, name, parameter)";
                else if (scalarName == "global::System.Text.Json.JsonElement") read = "Json(value, name, parameter)";
                else if (scalarName == "global::System.DateTimeOffset") read = "DateTimeOffset(value, name, parameter)";
                else read = scalar.SpecialType switch
                {
                    SpecialType.System_String => "ReadString(value, name, parameter)!",
                    SpecialType.System_Int32 => "ReadInt32(value, name, parameter)",
                    SpecialType.System_Int64 => "ReadInt64(value, name, parameter)",
                    SpecialType.System_Boolean => "Boolean(value, name, parameter)",
                    _ => $"Value<{property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(value, name, parameter, options)"
                };
                cache.Bindings.Add(new McpPropertyBindingCache
                {
                    PropertyName = property.Name,
                    WireNameExpression = jsonName is null ? $"options.PropertyNamingPolicy?.ConvertName({literal}) ?? {literal}" : SymbolDisplay.FormatLiteral(jsonName, true),
                    ParameterExpression = parameter,
                    ReadExpression = (nullable ? "value.ValueKind == global::System.Text.Json.JsonValueKind.Null && parameter.AllowNull ? null : " : string.Empty)
                        + "global::SereinFlow.Mcp.Framework.McpArgumentReader." + read,
                    Required = attribute?.NamedArguments.Any(item => item.Key == "Required" && item.Value.Value is true) == true,
                    IgnoreWhitespace = attribute?.NamedArguments.Any(item => item.Key == "IgnoreWhitespace" && item.Value.Value is true) == true
                });
            }
        }
    }
}
