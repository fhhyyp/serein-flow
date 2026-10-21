using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Generator.Framework;

namespace ScriptLang.Generator;

/// <summary>Domain-only analysis; all Roslyn symbols are consumed within the transform.</summary>
internal sealed class PrototypeModel : IEquatable<PrototypeModel>
{
    public const string ExtensionAttribute = "ScriptLang.PrototypeExtensionAttribute";
    public string Namespace { get; }
    public string ClassName { get; }
    public bool PushThis { get; }
    public bool HasTarget { get; }
    public EquatableArray<PrototypeMethod> Methods { get; }
    public string HintName { get; }
    public SourceLocation? Location { get; }

    public PrototypeModel(INamedTypeSymbol type, AttributeData attribute, Location location)
    {
        HintName = SymbolNames.HintName(type);
        Location = SourceLocation.From(location);
        Namespace = SymbolNames.Namespace(type);
        ClassName = SymbolNames.Identifier(type.Name);
        PushThis = AttributeReader.NamedValue(attribute, "PushThis", false);
        var jsNames = AttributeReader.NamedValue(attribute, "NamingFormat", 0) == 1;
        var methods = type.GetMembers().OfType<IMethodSymbol>().ToArray();
        HasTarget = methods.Any(method => method.Name == "IsTarget" && method.Parameters.Length == 1
            && method.ReturnType.SpecialType == SpecialType.System_Boolean
            && method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is MethodDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)));
        var members = methods.OrderBy(method => method.ToDisplayString(), StringComparer.Ordinal)
            .SelectMany(method => method.GetAttributes()
                .Where(item => item.AttributeClass?.ToDisplayString() is "ScriptLang.PrototypeFunctionAttribute" or "ScriptLang.PrototypePropertyAttribute")
                .Select(item => new PrototypeMethod(method, item, jsNames)));
        Methods = new EquatableArray<PrototypeMethod>(members);
    }

    public bool Equals(PrototypeModel? other) => other is not null && Namespace == other.Namespace && ClassName == other.ClassName
        && PushThis == other.PushThis && HasTarget == other.HasTarget && HintName == other.HintName
        && Equals(Location, other.Location) && Methods.Equals(other.Methods);
    public override bool Equals(object? obj) => Equals(obj as PrototypeModel);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(HintName);
}

internal enum PrototypeMemberKind { Property, Function, AsyncFunction }
internal enum PrototypeCallKind { Void, Value, AwaitVoid, AwaitValue }

internal sealed class PrototypeMethod : IEquatable<PrototypeMethod>
{
    public string Name { get; }
    public string ScriptName { get; }
    public string VariableName { get; }
    public PrototypeMemberKind Kind { get; }
    public PrototypeCallKind Call { get; }
    public string AsyncModifier { get; }
    public EquatableArray<PrototypeParameter> Parameters { get; }
    public bool Unsupported { get; }

    public PrototypeMethod(IMethodSymbol method, AttributeData attribute, bool jsNames)
    {
        Name = SymbolNames.Identifier(method.Name);
        var name = AttributeReader.NamedValue<string?>(attribute, "Name");
        ScriptName = string.IsNullOrWhiteSpace(name)
            ? (jsNames ? char.ToLowerInvariant(method.Name[0]) : char.ToUpperInvariant(method.Name[0])) + method.Name.Substring(1) : name!;
        var property = attribute.AttributeClass?.Name == "PrototypePropertyAttribute";
        VariableName = SymbolNames.StableIdentifier("_prototype", method.ToDisplayString() + ":" + attribute.AttributeClass?.Name + ":" + ScriptName);
        var isTask = false;
        var hasTaskResult = false;
        if (method.ReturnType is INamedTypeSymbol result)
        {
            var definition = result.OriginalDefinition.ToDisplayString();
            isTask = definition is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>"
                or "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask<TResult>";
            hasTaskResult = isTask && result.TypeArguments.Length == 1;
        }
        Kind = property ? PrototypeMemberKind.Property : isTask ? PrototypeMemberKind.AsyncFunction : PrototypeMemberKind.Function;
        Call = isTask ? hasTaskResult ? PrototypeCallKind.AwaitValue : PrototypeCallKind.AwaitVoid
            : method.ReturnsVoid ? PrototypeCallKind.Void : PrototypeCallKind.Value;
        AsyncModifier = isTask ? "async " : "";
        Parameters = new EquatableArray<PrototypeParameter>(method.Parameters.Select(parameter => new PrototypeParameter(parameter)));
        Unsupported = method.IsGenericMethod || method.IsAbstract || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None);
    }

    public bool Equals(PrototypeMethod? other) => other is not null && Name == other.Name && ScriptName == other.ScriptName
        && VariableName == other.VariableName && Kind == other.Kind && Call == other.Call && Unsupported == other.Unsupported
        && Parameters.Equals(other.Parameters);
    public override bool Equals(object? obj) => Equals(obj as PrototypeMethod);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(VariableName);
}

internal sealed class PrototypeParameter : IEquatable<PrototypeParameter>
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
    public bool Equals(PrototypeParameter? other) => other is not null && TypeName == other.TypeName && DisplayName == other.DisplayName
        && Optional == other.Optional && IsEngine == other.IsEngine && IsScriptValue == other.IsScriptValue;
    public override bool Equals(object? obj) => Equals(obj as PrototypeParameter);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeName);
}
