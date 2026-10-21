using Serein.Generator.Framework;

namespace ScriptLang.Generator;

/// <summary>Declares prototype discovery, common type requirements, domain analysis and per-type output.</summary>
[Microsoft.CodeAnalysis.Generator]
public sealed partial class ScriptPrototypeGenerator : FrameworkGenerator
{
    /// <inheritdoc />
    protected override void Configure(GeneratorBuilder builder)
    {
        builder.AddInitializationSource("ScriptPrototypeToolkits.g.cs", _attributeText);
        builder.ForClassAttribute(PrototypeModel.ExtensionAttribute)
            .Require(PrototypeAnalyzer.InvalidMember, TypeRules.TopLevel, TypeRules.Partial, TypeRules.NonGeneric, TypeRules.Instance)
            .Analyze(PrototypeAnalyzer.Analyze)
            .Named("ScriptPrototypeModels")
            .Emit(PrototypeEmitter.Emit, "ScriptPrototypeOutput");
    }
}
