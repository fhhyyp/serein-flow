using System.Linq;
using Microsoft.CodeAnalysis;
using Serein.Generator.Framework;

namespace ScriptLang.Generator;

/// <summary>Owns script-specific validation; the emitter only receives successful normalized models.</summary>
internal static class PrototypeAnalyzer
{
    private static readonly DiagnosticDescriptor MissingTarget = new DiagnosticDescriptor(
        "Script001", "需要实现分部方法", "已为 {0} 类型创建继承 'IPrototype' 接口的分布类，需要在该类中实现具有 partial 修饰的 bool IsTarget(Value value) 接口方法",
        "MemberDefinition", DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor InvalidMember = new DiagnosticDescriptor(
        "PROTOTYPE002", "不支持的原型声明", "Prototype '{0}': {1}", "ScriptGeneration", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateName = new DiagnosticDescriptor(
        "PROTOTYPE003", "重复的原型成员名称", "Prototype '{0}' declares script member '{1}' more than once; use distinct Name aliases",
        "ScriptGeneration", DiagnosticSeverity.Error, true);

    internal static AnalysisResult<PrototypeModel> Analyze(SymbolContext<INamedTypeSymbol> input, AnalysisContext context)
    {
        var model = new PrototypeModel(input.Symbol, input.Attribute, input.Location);
        if (!model.HasTarget)
        {
            var diagnostic = Diagnostic.Create(MissingTarget, input.Location, input.Symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
        var duplicates = model.Methods.GroupBy(method => method.ScriptName).Where(group => group.Count() > 1);
        foreach (var duplicate in duplicates)
        {
            var diagnostic = Diagnostic.Create(DuplicateName, input.Location, model.ClassName, duplicate.Key);
            context.ReportDiagnostic(diagnostic);
        }
        foreach (var method in model.Methods)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var invalidProperty = method.Kind == PrototypeMemberKind.Property
                && (method.Call != PrototypeCallKind.Value || method.Parameters.Count != (model.PushThis ? 1 : 0));
            if (method.Unsupported || (model.PushThis && method.Parameters.Count == 0) || invalidProperty)
            {
                var reason = $"member {method.Name} must have a supported nongeneric signature and valid receiver";
                var diagnostic = Diagnostic.Create(InvalidMember, input.Location, model.ClassName, reason);
                context.ReportDiagnostic(diagnostic);
            }
        }
        return context.Success(model);
    }
}
