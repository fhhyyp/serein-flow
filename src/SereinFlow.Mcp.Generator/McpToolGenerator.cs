using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using SereinFlow.Mcp.Generator.Extensions;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator;

/// <summary>增量分析工具声明，构建缓存模型并委托扩展方法输出代码。</summary>
[Generator]
public sealed partial class McpToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("McpToolkits.g.cs", SourceText.From(_attributeText, Encoding.UTF8)));
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("SereinFlowMcpToolkits.g.cs", SourceText.From(_sereinFlowAttributeText, Encoding.UTF8)));

        var toolCaches = context.SyntaxProvider.CreateSyntaxProvider(Predicate, Transform)
            .Where(static cache => cache is not null)
            .Select(static (cache, _) => cache!);
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? "McpHost");
        var registryCaches = toolCaches.Collect().Combine(assemblyName)
            .Select(static (input, _) => new McpToolRegistryCache(input.Right, input.Left).BuildValidationCache());
        context.RegisterSourceOutput(registryCaches, GeneratorCode);
        var hosts = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
            static (syntax, cancellationToken) => (syntax.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntax.Node, cancellationToken) as INamedTypeSymbol)?
                .BuildHostCache((ClassDeclarationSyntax)syntax.Node, syntax.SemanticModel.Compilation.AssemblyName ?? "McpHost"))
            .Where(static cache => cache is not null).Select(static (cache, _) => cache!);
        context.RegisterSourceOutput(hosts, static (output, cache) =>
        {
            var code = cache.GenerateCode(output);
            if (code is not null) output.AddSource(cache.HintName, SourceText.From(code, Encoding.UTF8));
        });
    }

    private static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && node is MethodDeclarationSyntax method && method.AttributeLists.Count > 0;
    }

    private static McpToolCache? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var method = context.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax)context.Node, cancellationToken) as IMethodSymbol;
        return method?.BuildCache();
    }

    private static void GeneratorCode(SourceProductionContext context, McpToolRegistryCache cache)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var generatedCode = cache.GenerateCode(context);
        context.AddSource("McpToolRegistry.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
    }
}
