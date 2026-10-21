using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ScriptLang.Generator;

namespace ScriptLang.Generator.Tests;

public sealed class PrototypeGeneratorTests
{
    private const string Prototype = """
        using System;
        using System.Collections.Generic;
        using ScriptLang;
        using ScriptLang.Runtime;
        namespace Sample
        {
            [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
            public partial class Example
            {
                public partial bool IsTarget(Value value) => value is StringValue;
                [PrototypeProperty(Name = "length")]
                private static Value Length(StringValue value) => value;
                [PrototypeFunction(Name = "echo")]
                private static Value Echo(StringValue value, Value argument) => argument;
            }
        }
        """;

    [Fact]
    public void EmitsLocalDeclarationsAndCompilableDirectPrototypeCalls()
    {
        var (driver, compilation) = Run(Prototype);
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(driver.GetRunResult().Diagnostics);
        var sources = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Contains(sources, item => item.HintName == "ScriptPrototypeToolkits.g.cs");
        var output = sources.Single(item => item.HintName != "ScriptPrototypeToolkits.g.cs").SourceText.ToString();
        Assert.Contains("partial class Example : global::ScriptLang.IPrototype", output);
        Assert.Contains("return Length(target);", output);
        Assert.Contains("var result = Echo(target, arg0);", output);
        Assert.Contains("_prototypeMethods[\"echo\"]", output);
        Assert.DoesNotContain("System.Reflection", output);
        Assert.Equal(Accessibility.Internal, compilation.GetTypeByMetadataName("ScriptLang.PrototypeExtensionAttribute")!.DeclaredAccessibility);
    }

    [Fact]
    public void MissingTargetMethodProducesExistingDiagnostic()
    {
        var (driver, _) = Run(Prototype.Replace("public partial bool IsTarget(Value value) => value is StringValue;", ""));
        Assert.Contains(driver.GetRunResult().Diagnostics, item => item.Id == "Script001");
    }

    [Fact]
    public void UnmarkedClassesDoNotProducePrototypeImplementations()
    {
        var (driver, compilation) = Run("namespace Sample { public class Plain { } }");
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources);
    }

    internal static (GeneratorDriver Driver, Compilation Compilation) Run(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ScriptEngine).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PrototypeTestHost", [CSharpSyntaxTree.ParseText(source, parseOptions)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ScriptPrototypeGenerator().AsSourceGenerator()],
            parseOptions: parseOptions, driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (driver, updated);
    }
}
