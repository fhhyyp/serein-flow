using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SereinFlow.Mcp.Framework;

namespace SereinFlow.Mcp.Generator.Tests;

public sealed class McpIncrementalTests
{
    private const string Tools = """
        using System.Threading.Tasks;
        using SereinFlow.Mcp.Framework;
        namespace Sample;
        public static class Tools
        {
            [McpTool("first")] public static Task<string> First(Request request) => Task.FromResult(request.Name);
            [McpTool("second")] public static Task<string> Second(Request request) => Task.FromResult(request.Name);
        }
        """;
    private const string Request = """
        using SereinFlow.Mcp.Framework;
        namespace Sample;
        [McpBindProperties] public sealed class Request
        {
            [McpParameter(Required = true)] public string Name { get; set; } = "";
        }
        """;

    [Fact]
    public void SharedRequestCodeIsEmittedOnceAndChangesInAnotherFileInvalidateBinding()
    {
        var (driver, input) = Create(Tools, Request);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        Assert.Empty(output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        var source = Registry(driver);
        Assert.Equal(1, source.Split("McpRequestBinder.ValidateObject", StringSplitOptions.None).Length - 1);
        var original = input.SyntaxTrees.Last();
        var changed = CSharpSyntaxTree.ParseText(Request.Replace("public string Name", "public string Extra { get; set; } = \"\"; public string Name"), (CSharpParseOptions)original.Options, path: "Request.cs");
        driver = driver.RunGeneratorsAndUpdateCompilation(input.ReplaceSyntaxTree(original, changed), out output, out _);
        Assert.Empty(output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Contains("request.@Extra", Registry(driver));
    }

    [Fact]
    public void UnrelatedEditReusesToolsAndRegistryOutput()
    {
        var (driver, input) = Create(Tools, Request);
        driver = driver.RunGenerators(input);
        var unrelated = CSharpSyntaxTree.ParseText("class Unrelated { int Value => 1; }", (CSharpParseOptions)input.SyntaxTrees.First().Options);
        driver = driver.RunGenerators(input.AddSyntaxTrees(unrelated));
        foreach (var name in new[] { "McpTools", "McpRegistryOutput" })
            Assert.All(driver.GetRunResult().Results.Single().TrackedSteps[name].SelectMany(step => step.Outputs),
                output => Assert.Contains(output.Reason, new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged }));
    }

    [Fact]
    public void MarkerOnOnePartialHostDoesNotEmitMultipleDeclarations()
    {
        var (driver, input) = Create("""
            using SereinFlow.Mcp.Framework;
            namespace Sample;
            [McpHost] public static partial class Hosting { }
            public static partial class Hosting { }
            """);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        Assert.Empty(output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources.Where(source => source.HintName == "Sample.Hosting.g.cs"));
    }

    private static string Registry(GeneratorDriver driver) => driver.GetRunResult().Results.Single().GeneratedSources
        .Single(source => source.HintName == "McpToolRegistry.g.cs").SourceText.ToString();

    private static (GeneratorDriver Driver, CSharpCompilation Input) Create(params string[] sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IMcpToolMetadata).Assembly.Location)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly.Location)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var input = CSharpCompilation.Create("IncrementalHost", sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, options, path: $"Input{index}.cs")),
            paths.Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create([new McpToolGenerator().AsSourceGenerator()], parseOptions: options,
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));
        return (driver, input);
    }
}
