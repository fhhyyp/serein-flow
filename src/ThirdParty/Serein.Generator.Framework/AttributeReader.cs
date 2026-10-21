using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

public static class AttributeReader
{
    public static bool IsOrDerivesFrom(INamedTypeSymbol? type, string metadataName)
    {
        for (; type is not null; type = type.BaseType)
            if (SymbolNames.MetadataName(type) == metadataName) return true;
        return false;
    }
    public static AttributeData? Find(ISymbol symbol, string metadataName, bool includeDerived = false)
        => symbol.GetAttributes().FirstOrDefault(attribute => includeDerived
            ? IsOrDerivesFrom(attribute.AttributeClass, metadataName)
            : attribute.AttributeClass is not null && SymbolNames.MetadataName(attribute.AttributeClass) == metadataName);
    public static T NamedValue<T>(AttributeData attribute, string memberName, T defaultValue = default!)
    {
        foreach (var argument in attribute.NamedArguments)
            if (argument.Key == memberName && argument.Value.Value is T value) return value;
        return defaultValue;
    }
}
