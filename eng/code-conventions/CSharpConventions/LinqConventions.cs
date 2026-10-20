using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class LinqConventions
{
    private static readonly MetadataReference[] References = CreateReferences();

    public static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: ["System", "System.Collections.Generic", "System.Linq"]);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var implicitUsings = CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using System.IO; global using System.Net.Http;", parseOptions);
        var compilation = CSharpCompilation.Create("ConventionAnalysis", trees, References, options);
        compilation = compilation.AddSyntaxTrees(implicitUsings);
        return compilation;
    }

    public static bool IsLinqChain(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var declaringType = definition.ContainingType.ToDisplayString();
        if (declaringType is not ("System.Linq.Enumerable" or "System.Linq.Queryable"))
        {
            return false;
        }

        // Only exempt LINQ-to-LINQ receiver links. Factories, callbacks and
        // ordinary fluent methods inside or outside the chain remain checked.
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        ExpressionSyntax receiver = member.Expression;
        while (receiver is ParenthesizedExpressionSyntax parenthesized)
        {
            receiver = parenthesized.Expression;
        }

        if (receiver is not InvocationExpressionSyntax receiverInvocation)
        {
            return false;
        }

        var receiverInfo = model.GetSymbolInfo(receiverInvocation);
        if (receiverInfo.Symbol is not IMethodSymbol receiverMethod)
        {
            return false;
        }

        var receiverDefinition = receiverMethod.ReducedFrom ?? receiverMethod;
        var receiverType = receiverDefinition.ContainingType.ToDisplayString();
        return receiverType is "System.Linq.Enumerable" or "System.Linq.Queryable";
    }

    private static MetadataReference[] CreateReferences()
    {
        var platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var paths = platformAssemblies.Split(Path.PathSeparator);
        var references = new List<MetadataReference>();
        foreach (var path in paths)
        {
            var reference = MetadataReference.CreateFromFile(path);
            references.Add(reference);
        }

        var result = references.ToArray();
        return result;
    }
}
