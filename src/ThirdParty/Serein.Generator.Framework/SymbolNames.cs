using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Serein.Generator.Framework;

public static class SymbolNames
{
    public static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public static string MetadataName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType) names.Push(current.MetadataName);
        var namespaces = new Stack<string>();
        for (var current = type.ContainingNamespace; !current.IsGlobalNamespace; current = current.ContainingNamespace) namespaces.Push(current.MetadataName);
        var prefix = string.Join(".", namespaces);
        return (prefix.Length == 0 ? "" : prefix + ".") + string.Join("+", names);
    }
    public static string Namespace(INamedTypeSymbol type) => type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
    public static string Escape(string identifier) => "@" + identifier;
    public static string Identifier(string identifier) => SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : Escape(identifier);
    public static string StableIdentifier(string prefix, string identity)
    {
        using (var hash = SHA256.Create())
            return prefix + string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(identity)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }
    public static string HintName(INamedTypeSymbol type) => StableIdentifier(type.Name + ".", TypeName(type)) + ".g.cs";
}
