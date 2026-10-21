using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Serein.Generator.Framework;

public static class CSharpLiteral
{
    public static string RenderAttribute(AttributeData attribute, string? typeName = null)
    {
        var arguments = string.Join(", ", attribute.ConstructorArguments.Select(Render));
        var assignments = string.Join(", ", attribute.NamedArguments.Select(item => SymbolNames.Escape(item.Key) + " = " + Render(item.Value)));
        return $"new {typeName ?? SymbolNames.TypeName(attribute.AttributeClass!)}({arguments})" + (assignments.Length == 0 ? "" : " { " + assignments + " }");
    }
    public static string Render(TypedConstant constant)
    {
        if (constant.IsNull) return "null";
        if (constant.Kind == TypedConstantKind.Array)
            return $"new {SymbolNames.TypeName(((IArrayTypeSymbol)constant.Type!).ElementType)}[] {{ {string.Join(", ", constant.Values.Select(Render))} }}";
        if (constant.Kind == TypedConstantKind.Type) return $"typeof({SymbolNames.TypeName((ITypeSymbol)constant.Value!)})";
        if (constant.Kind == TypedConstantKind.Enum) return $"({SymbolNames.TypeName(constant.Type!)}){RenderValue(constant.Value)}";
        return RenderValue(constant.Value);
    }
    public static string RenderValue(object? value) => value switch
    {
        null => "null",
        string text => SymbolDisplay.FormatLiteral(text, true),
        char character => SymbolDisplay.FormatLiteral(character, true),
        bool flag => flag ? "true" : "false",
        float number when float.IsNaN(number) => "global::System.Single.NaN",
        float number when float.IsPositiveInfinity(number) => "global::System.Single.PositiveInfinity",
        float number when float.IsNegativeInfinity(number) => "global::System.Single.NegativeInfinity",
        float number => number.ToString("R", CultureInfo.InvariantCulture) + "F",
        double number when double.IsNaN(number) => "global::System.Double.NaN",
        double number when double.IsPositiveInfinity(number) => "global::System.Double.PositiveInfinity",
        double number when double.IsNegativeInfinity(number) => "global::System.Double.NegativeInfinity",
        double number => number.ToString("R", CultureInfo.InvariantCulture) + "D",
        decimal number => number.ToString(CultureInfo.InvariantCulture) + "M",
        ulong number => number.ToString(CultureInfo.InvariantCulture) + "UL",
        long number => number.ToString(CultureInfo.InvariantCulture) + "L",
        uint number => number.ToString(CultureInfo.InvariantCulture) + "U",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!
    };
}
