using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Serein.Generator.Framework;

/// <summary>A pure semantic rule returning a reason on failure, or null on success.</summary>
public sealed class SymbolRule<TSymbol> where TSymbol : class, ISymbol
{
    private readonly Func<SymbolContext<TSymbol>, string?> validate;
    public SymbolRule(Func<SymbolContext<TSymbol>, string?> validate)
        => this.validate = validate ?? throw new ArgumentNullException(nameof(validate));
    internal string? Validate(SymbolContext<TSymbol> context) => validate(context);
}

public static class TypeRules
{
    public static SymbolRule<INamedTypeSymbol> TopLevel { get; } = new SymbolRule<INamedTypeSymbol>(
        input => input.Symbol.ContainingType is null ? null : "the type must be top-level");
    public static SymbolRule<INamedTypeSymbol> NonGeneric { get; } = new SymbolRule<INamedTypeSymbol>(
        input => input.Symbol.IsGenericType ? "the type must be nongeneric" : null);
    public static SymbolRule<INamedTypeSymbol> Partial { get; } = new SymbolRule<INamedTypeSymbol>(
        input => input.Declaration is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            ? null : "the declaration must be partial");
    public static SymbolRule<INamedTypeSymbol> Static { get; } = new SymbolRule<INamedTypeSymbol>(
        input => input.Symbol.IsStatic ? null : "the type must be static");
    public static SymbolRule<INamedTypeSymbol> Instance { get; } = new SymbolRule<INamedTypeSymbol>(
        input => input.Symbol.IsStatic ? "the type must be an instance type" : null);
}
