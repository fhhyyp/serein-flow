using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Generator.Framework;

namespace ScriptLang.Generator;

/// <summary>Domain-only analysis; all Roslyn symbols are consumed within the transform.</summary>
internal sealed class PrototypeModel
{
    public const string ExtensionAttribute = "ScriptLang.PrototypeExtensionAttribute";
    public string Namespace { get; }
    public string ClassName { get; }
    public bool PushThis { get; }
    public bool HasTarget { get; }
    public IReadOnlyList<PrototypeMethod> Methods { get; }

    public PrototypeModel(INamedTypeSymbol type, AttributeData attribute)
    {
        Namespace = SymbolNames.Namespace(type);
        ClassName = SymbolNames.Identifier(type.Name);
        PushThis = AttributeReader.NamedValue(attribute, "PushThis", false);
        var jsNames = AttributeReader.NamedValue(attribute, "NamingFormat", 0) == 1;
        var methods = type.GetMembers().OfType<IMethodSymbol>().ToArray();
        HasTarget = methods.Any(method => method.Name == "IsTarget" && method.Parameters.Length == 1
            && method.ReturnType.SpecialType == SpecialType.System_Boolean
            && method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is MethodDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)));
        Methods = methods.OrderBy(method => method.ToDisplayString(), StringComparer.Ordinal)
            .SelectMany(method => method.GetAttributes()
                .Where(item => item.AttributeClass?.ToDisplayString() is "ScriptLang.PrototypeFunctionAttribute" or "ScriptLang.PrototypePropertyAttribute")
                .Select(item => new PrototypeMethod(method, item, jsNames))).ToArray();
    }
}

internal sealed class PrototypeMethod
{
    public string Name { get; }
    public string ScriptName { get; }
    public string VariableName { get; }
    public bool IsProperty { get; }
    public bool ReturnsVoid { get; }
    public bool IsTask { get; }
    public bool HasTaskResult { get; }
    public IReadOnlyList<PrototypeParameter> Parameters { get; }
    public bool Unsupported { get; }

    public PrototypeMethod(IMethodSymbol method, AttributeData attribute, bool jsNames)
    {
        Name = SymbolNames.Identifier(method.Name);
        var name = AttributeReader.NamedValue<string?>(attribute, "Name");
        ScriptName = string.IsNullOrWhiteSpace(name)
            ? (jsNames ? char.ToLowerInvariant(method.Name[0]) : char.ToUpperInvariant(method.Name[0])) + method.Name.Substring(1) : name!;
        IsProperty = attribute.AttributeClass?.Name == "PrototypePropertyAttribute";
        VariableName = SymbolNames.StableIdentifier("_prototype", method.ToDisplayString() + ":" + attribute.AttributeClass?.Name + ":" + ScriptName);
        ReturnsVoid = method.ReturnsVoid;
        if (method.ReturnType is INamedTypeSymbol result)
        {
            var definition = result.OriginalDefinition.ToDisplayString();
            IsTask = definition is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>"
                or "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask<TResult>";
            HasTaskResult = IsTask && result.TypeArguments.Length == 1;
        }
        Parameters = method.Parameters.Select(parameter => new PrototypeParameter(parameter)).ToArray();
        Unsupported = method.IsGenericMethod || method.IsAbstract || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None);
    }
}

internal sealed class PrototypeParameter
{
    public PrototypeParameter(IParameterSymbol parameter)
    {
        TypeName = SymbolNames.TypeName(parameter.Type);
        DisplayName = parameter.Type.Name;
        Optional = parameter.HasExplicitDefaultValue;
        IsEngine = parameter.Type.ToDisplayString() == "ScriptLang.ScriptEngine";
        IsScriptValue = AttributeReader.IsOrDerivesFrom(parameter.Type as INamedTypeSymbol, "ScriptLang.Runtime.Value");
    }
    public string TypeName { get; }
    public string DisplayName { get; }
    public bool Optional { get; }
    public bool IsEngine { get; }
    public bool IsScriptValue { get; }
}
