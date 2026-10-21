using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Generator.Framework;
using SereinFlow.Mcp.Generator.Extensions;
using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator;

/// <summary>增量分析工具声明，构建缓存模型并委托扩展方法输出代码。</summary>
[Generator]
public sealed partial class McpToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterInitializationSource("McpToolkits.g.cs", _attributeText);
        context.RegisterInitializationSource("SereinFlowMcpToolkits.g.cs", _sereinFlowAttributeText);

        // Semantic matching preserves custom attributes derived from McpToolAttribute.
        var toolCaches = context.SyntaxProvider.CreateSyntaxProvider(Predicate, Transform)
            .Where(static cache => cache is not null)
            .Select(static (cache, _) => cache!).WithTrackingName("McpTools");
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? "McpHost");
        var registryCaches = toolCaches.Collect().Combine(assemblyName)
            .Select(static (input, token) => GenerationResult.Create("MCP registry", output =>
                output.AddSource("McpToolRegistry.g.cs", input.Left.GenerateRegistry(input.Right, output)), token));
        context.RegisterGeneratedOutput(registryCaches, "McpRegistryOutput");
        var hosts = context.SyntaxProvider.ForAttributeWithMetadataName("SereinFlow.Mcp.Framework.McpHostAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntax, token) => GenerationResult.Create(syntax.TargetSymbol.Name, output =>
            {
                var cache = ((INamedTypeSymbol)syntax.TargetSymbol).BuildHostCache((ClassDeclarationSyntax)syntax.TargetNode,
                    syntax.SemanticModel.Compilation.AssemblyName ?? "McpHost");
                if (cache is null) return;
                var code = cache.GenerateCode(output);
                if (code is not null) output.AddSource(cache.HintName, code);
            }, token));
        context.RegisterGeneratedOutput(hosts, "McpHostOutput");
    }

    private static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && node is MethodDeclarationSyntax method && method.AttributeLists.Count > 0;
    }

    private static McpToolAnalysis? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var method = context.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax)context.Node, cancellationToken) as IMethodSymbol;
        if (method is null || AttributeReader.Find(method, "SereinFlow.Mcp.Framework.McpToolAttribute", includeDerived: true) is null) return null;
        var location = SourceLocation.From(method.Locations.FirstOrDefault());
        var name = string.Empty;
        var entry = string.Empty;
        EquatableArray<GeneratedSource> support = default;
        var result = GenerationResult.Create(method.ToDisplayString(), output =>
        {
            var cache = method.BuildCache()!;
            name = cache.Name;
            foreach (var diagnostic in cache.Diagnostics) output.ReportDiagnostic(diagnostic);
            if (cache.Diagnostics.Count != 0) return;
            var registry = new McpToolRegistryCache("", System.Collections.Immutable.ImmutableArray.Create(cache));
            registry.BuildValidationCache();
            entry = registry.GenerateEntry(cache, output);
            support = new EquatableArray<GeneratedSource>(registry.GenerateSupport(output));
        }, cancellationToken);
        return new McpToolAnalysis(name, location, entry, support, result);
    }
}
