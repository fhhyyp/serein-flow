using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SereinFlow.Mcp.Framework;

namespace SereinFlow.Mcp.Generator.Tests;

public sealed class McpToolGeneratorTests
{
    private const string Header = """
        using System.Threading;
        using System.Threading.Tasks;
        using SereinFlow.Mcp.Framework;
        namespace Sample;
        """;

    [Fact]
    public void InitializationEmitsLocalDeclarationTypesWithoutToolsOrBusinessDependencies()
    {
        var (run, compilation) = Generate(Header);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var toolkit = Assert.Single(Assert.Single(run.Results).GeneratedSources, source => source.HintName == "McpToolkits.g.cs");
        Assert.DoesNotContain("SereinFlow.Contracts", toolkit.SourceText.ToString());
        foreach (var name in new[] { "McpToolAttribute", "McpContextAttribute", "McpBindPropertiesAttribute", "McpParameterAttribute", "McpHostAttribute" })
        {
            var fullName = "SereinFlow.Mcp.Framework." + name;
            var type = compilation.GetTypeByMetadataName(fullName);
            Assert.NotNull(type);
            Assert.Equal(Accessibility.Internal, type.DeclaredAccessibility);
            Assert.Equal(compilation.Assembly, type.ContainingAssembly, SymbolEqualityComparer.Default);
            Assert.Null(typeof(IMcpToolMetadata).Assembly.GetType(fullName));
        }
        Assert.Null(compilation.GetTypeByMetadataName("SereinFlow.Mcp.SereinFlowMcpToolAttribute"));
    }

    [Fact]
    public void HostEntryPointsAndOptionalConfigurationHooksAreGeneratedFromTheHostModel()
    {
        var (run, compilation) = Generate(Header + """
            [McpHost("AddSampleTools")]
            public static partial class Hosting
            {
                static partial void ConfigureJsonOptions(ref System.Text.Json.JsonSerializerOptions options)
                    => options = new System.Text.Json.JsonSerializerOptions();
            }
            """);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var host = Assert.Single(Assert.Single(run.Results).GeneratedSources, source => source.HintName == "Sample.Hosting.g.cs").SourceText.ToString();
        Assert.Contains("@AddSampleTools(this", host);
        Assert.Contains("new global::@GeneratorTestHost.Generated.McpToolRegistry()", host);
        Assert.Contains("ConfigureJsonOptions(ref jsonOptions)", host);
        Assert.Contains("ConfigureCatalog(catalog)", host);
        Assert.DoesNotContain("SereinFlow.Contracts", host);
        Assert.DoesNotContain("System.Reflection", host);
    }

    [Theory]
    [InlineData("[McpHost] public static class Hosting { }")]
    [InlineData("[McpHost] public partial class Hosting { }")]
    [InlineData("[McpHost(\"bad-name\")] public static partial class Hosting { }")]
    [InlineData("[McpHost(\"ConfigureServices\")] public static partial class Hosting { }")]
    [InlineData("[McpHost(\"Hosting\")] public static partial class Hosting { }")]
    public void InvalidHostDeclarationsProduceCompilerDiagnostics(string source)
    {
        var (run, _) = Generate(Header + source);
        Assert.Contains(run.Diagnostics, diagnostic => diagnostic.Id == "MCPGEN004" && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void GeneratedPropertyAttributesAreAvailableDuringSemanticAnalysis()
    {
        var (run, compilation) = Generate(Header + """
            [McpContext] public sealed record Caller(string Name);
            [McpBindProperties] public class Request
            {
                [McpParameter(Required = true, Trim = true)]
                public string Name { get; set; } = string.Empty;
            }
            public static class Tools
            {
                [McpTool("bound", Description = "Bind a name.")]
                public static Task<string> Read(Request request, Caller caller) => Task.FromResult(request.Name);
            }
            """);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var output = Assert.Single(Assert.Single(run.Results).GeneratedSources, source => source.HintName == "McpToolRegistry.g.cs").SourceText.ToString();
        Assert.Contains("McpArgumentReader.Require", output);
        Assert.Contains("McpParameterOptions", output);
        Assert.DoesNotContain("new global::SereinFlow.Mcp.Framework.McpParameterAttribute", output);
    }

    [Fact]
    public void SereinFlowHostOptInEmitsPolicyAttributeAgainstTheHostRuntimeInterface()
    {
        var (run, compilation) = Generate("""
            namespace SereinFlow.Contracts
            {
                public enum SereinFlowPermission { Read }
                public enum FlowVersionTrackDto { Development }
            }
            namespace SereinFlow.Mcp.Integration
            {
                public interface ISereinFlowMcpToolMetadata : Framework.IMcpToolMetadata
                {
                    Contracts.SereinFlowPermission[] Permissions { get; }
                    bool HasAuditTrack { get; }
                    Contracts.FlowVersionTrackDto AuditTrack { get; }
                }
            }
            namespace SereinFlow.Mcp
            {
                public static class Tools
                {
                    [SereinFlowMcpTool("read", Contracts.SereinFlowPermission.Read, Description = "Read.", HasAuditTrack = true)]
                    public static System.Threading.Tasks.Task<string> Read() => System.Threading.Tasks.Task.FromResult("");
                }
            }
            """, sereinFlowHost: true);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var type = compilation.GetTypeByMetadataName("SereinFlow.Mcp.SereinFlowMcpToolAttribute");
        Assert.NotNull(type);
        Assert.Equal(Accessibility.Internal, type.DeclaredAccessibility);
        Assert.Contains(type.AllInterfaces, item => item.Name == "ISereinFlowMcpToolMetadata");
        var output = Assert.Single(Assert.Single(run.Results).GeneratedSources, source => source.HintName == "McpToolRegistry.g.cs").SourceText.ToString();
        Assert.Contains("new global::SereinFlow.Mcp.SereinFlowMcpToolAttribute", output);
        Assert.Contains("@HasAuditTrack = true", output);
    }

    [Fact]
    public void EmitsCompilableDirectCallsConstructorFactoriesAndContextBinding()
    {
        var source = Header + """

            [McpContext] public sealed record Caller(string Name);
            public sealed record Request(string Name);
            public sealed class Service { }
            public sealed class Tools(Service service)
            {
                [McpTool("echo", Description = "Echo.")]
                public Task<string> Echo(Request request, Caller caller, CancellationToken cancellationToken)
                    => Task.FromResult(request.Name + caller.Name);
            }
            """;
        var (run, compilation) = Generate(source);
        Assert.Empty(run.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var output = Assert.Single(Assert.Single(run.Results).GeneratedSources, source => source.HintName == "McpToolRegistry.g.cs").SourceText.ToString();
        Assert.Contains("handler.@Echo(request, context.GetValue<global::Sample.Caller>(), cancellationToken)", output);
        var bound = output.IndexOf("McpRequestBinder.Bind<", StringComparison.Ordinal);
        var authorized = output.IndexOf("context.AuthorizeRequestAsync(request, cancellationToken)", StringComparison.Ordinal);
        var invoked = output.IndexOf("handler.@Echo(", StringComparison.Ordinal);
        Assert.True(bound >= 0 && authorized > bound && invoked > authorized);
        Assert.Contains("new global::Sample.Tools(", output);
        Assert.Contains("GetRequiredService<global::Sample.Service>(provider)", output);
        Assert.DoesNotContain("System.Reflection", output);
        Assert.DoesNotContain("Expression.Compile", output);
        Assert.DoesNotContain("MethodInfo", output);
    }

    [Theory]
    [InlineData("[McpTool(\"same\")] public static Task<string> First() => Task.FromResult(\"\"); [McpTool(\"same\")] public static Task<string> Second() => Task.FromResult(\"\");", "MCPGEN001")]
    [InlineData("[McpTool(\"bad\")] public static string Bad() => \"\";", "MCPGEN002")]
    [InlineData("[McpTool(\"bad\")] private static Task<string> Bad() => Task.FromResult(\"\");", "MCPGEN002")]
    [InlineData("[McpTool(\"bad\")] public static Task<string> Bad(Request first, Request second) => Task.FromResult(\"\");", "MCPGEN002")]
    [InlineData("[McpTool(\"bad\")] public static Task<string> Bad<T>() => Task.FromResult(\"\");", "MCPGEN002")]
    [InlineData("[McpTool(\"bad\")] public static Task<string> Bad(ref Request request) => Task.FromResult(\"\");", "MCPGEN002")]
    [InlineData("[McpTool(\"bad\")] public static Task<string> Bad(System.Text.Json.JsonElement arguments) => Task.FromResult(\"\");", "MCPGEN002")]
    public void InvalidDeclarationsProduceCompilerDiagnostics(string members, string diagnosticId)
    {
        var (run, _) = Generate(Header + "public sealed record Request(string Name); public sealed class Tools { " + members + " }");
        Assert.Contains(run.Diagnostics, diagnostic => diagnostic.Id == diagnosticId && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void AmbiguousConstructorsProduceCompilerDiagnostic()
    {
        var (run, _) = Generate(Header + """
            public class Tools
            {
                public Tools() { }
                public Tools(string value) { }
                [McpTool("tool")] public Task<string> Run() => Task.FromResult("");
            }
            """);
        Assert.Contains(run.Diagnostics, diagnostic => diagnostic.Id == "MCPGEN003");
    }

    [Fact]
    public void DerivedAttributesRetainConstructorAndNamedPolicyArguments()
    {
        var (run, compilation) = Generate(Header + """
            public enum Permission { Read, Write }
            internal sealed class PolicyAttribute(string name, params Permission[] permissions) : McpToolAttribute(name)
            {
                public bool Mutation { get; set; }
            }
            public static class Tools
            {
                [Policy("write", Permission.Write, Description = "Write.", Mutation = true)]
                public static Task<string> Write() => Task.FromResult("");
            }
            """);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var output = Assert.Single(Assert.Single(run.Results).GeneratedSources, source => source.HintName == "McpToolRegistry.g.cs").SourceText.ToString();
        Assert.Contains("new global::Sample.PolicyAttribute", output);
        Assert.Contains("@Mutation = true", output);
    }

    [Fact]
    public void NestedArrayPropertiesDoNotBreakRequestModelAnalysis()
    {
        var (run, compilation) = Generate(Header + """
            public sealed record Request(int[][] Values);
            public static class Tools
            {
                [McpTool("arrays", Description = "Read nested arrays.")]
                public static Task<int> Read(Request request) => Task.FromResult(request.Values.Length);
            }
            """);
        Assert.Empty(run.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("[McpBindProperties] public class Request(string name) { public string Name { get; set; } = name; }")]
    [InlineData("[McpBindProperties] public class Request { public string Name { get; init; } = string.Empty; }")]
    public void UnsupportedPropertyBindingShapesProduceCompilerDiagnostics(string request)
    {
        var (run, _) = Generate(Header + request + """
            public static class Tools
            {
                [McpTool("request", Description = "Read request.")]
                public static Task<string> Read(Request request) => Task.FromResult(request.Name);
            }
            """);
        Assert.Contains(run.Diagnostics, diagnostic => diagnostic.Id == "MCPGEN002");
    }

    private static (GeneratorDriverRunResult Run, Compilation Compilation) Generate(string source, bool sereinFlowHost = false)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview,
            preprocessorSymbols: sereinFlowHost ? ["SEREINFLOW_MCP_HOST"] : []);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IMcpToolMetadata).Assembly.Location)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly.Location)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create("GeneratorTestHost", [CSharpSyntaxTree.ParseText(source, parseOptions)],
            paths.Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new McpToolGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (driver.GetRunResult(), updated);
    }
}
