using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serein.Generator.Framework;

#pragma warning disable RS2008 // These diagnostic IDs are test fixtures, not shipped analyzer rules.

namespace Serein.Generator.Framework.Tests;

public sealed class DeclarativePipelineTests
{
    private const string AttributeSource = """
        namespace Sample;
        [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
        internal class MarkerAttribute(string name = "") : System.Attribute { public string Name { get; } = name; }
        """;
    private const string OneTarget = "namespace Sample; [Marker(\"one\")] public partial class First { }";
    private static readonly DiagnosticDescriptor Invalid = new("PIPE001", "Invalid target", "Target '{0}': {1}", "Tests", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Warning = new("PIPE002", "Target warning", "Target '{0}': {1}", "Tests", DiagnosticSeverity.Warning, true);
    private static readonly DiagnosticDescriptor Duplicate = new("PIPE003", "Duplicate target", "Duplicate '{0}'", "Tests", DiagnosticSeverity.Error, true);

    [Fact]
    public void SkipSuccessFailureAndWarningsHaveDistinctRouting()
    {
        var skipped = new AnalysisContext(default).Skip<string>();
        Assert.Equal(AnalysisStatus.Skip, skipped.Status);
        var context = new AnalysisContext(default);
        var warning = Diagnostic.Create(Warning, Location.None, "First", "warning");
        context.ReportDiagnostic(warning);
        var success = context.Success("value");
        Assert.Equal(AnalysisStatus.Success, success.Status);
        Assert.Equal("value", success.Model);
        Assert.Single(success.Diagnostics);
        var error = Diagnostic.Create(Invalid, Location.None, "First", "error");
        context.ReportDiagnostic(error);
        var factoryCalled = false;
        var failure = context.Build(() =>
        {
            factoryCalled = true;
            return "unreachable";
        });
        Assert.Equal(AnalysisStatus.Failure, failure.Status);
        Assert.False(factoryCalled);
        Assert.Equal(2, failure.Diagnostics.Count);
    }

    [Fact]
    public void PartialTargetsAreAnalyzedOnceAndCommonErrorsStopAnalysis()
    {
        const string source = """
            namespace Sample;
            [Marker] public partial class First { }
            [Marker] public partial class First { }
            [Marker] public class Invalid { }
            [System.Obsolete] public class Unrelated { }
            """;
        var generator = new SampleGenerator();
        var run = Run(generator, source);
        Assert.Equal(1, generator.AnalysisCount);
        var diagnostics = run.Driver.GetRunResult().Diagnostics;
        var invalid = Assert.Single(diagnostics);
        Assert.Equal("PIPE001", invalid.Id);
        Assert.Equal("Input0.cs", invalid.Location.GetLineSpan().Path);
        var sources = Sources(run.Driver);
        Assert.Contains(sources, item => item.HintName == "First.g.cs");
        Assert.DoesNotContain(sources, item => item.HintName == "Invalid.g.cs");
    }

    [Fact]
    public void WarningRulesKeepTheModelAndGeneratedCode()
    {
        var generator = new SampleGenerator(warningRule: true);
        var run = Run(generator, OneTarget);
        var diagnostic = Assert.Single(run.Driver.GetRunResult().Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(1, generator.AnalysisCount);
        AssertCompiles(run.Output);
        var sources = Sources(run.Driver);
        Assert.Contains(sources, item => item.HintName == "First.g.cs");
    }

    [Fact]
    public void PipelineKeepsRuleWarningsWhenAnalyzerReturnsAnotherContextResult()
    {
        var generator = new SampleGenerator(warningRule: true, separateContext: true);
        var run = Run(generator, OneTarget);
        var diagnostics = run.Driver.GetRunResult().Diagnostics;
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "PIPE002");
        AssertCompiles(run.Output);
    }

    [Fact]
    public void DiagnosticsReportedAfterResultCreationStillStopEmission()
    {
        var generator = new SampleGenerator(lateError: true);
        var run = Run(generator, OneTarget);
        var sources = Sources(run.Driver);
        Assert.DoesNotContain(sources, source => source.HintName == "First.g.cs");
        var diagnostics = run.Driver.GetRunResult().Diagnostics;
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "PIPE001");
    }

    [Theory]
    [InlineData(AttributeMatch.Exact, 1)]
    [InlineData(AttributeMatch.IncludeDerived, 2)]
    public void ExactAndDerivedMatchingPreserveAliasesAndIgnoreUnmarkedSubclasses(AttributeMatch match, int count)
    {
        const string source = """
            using Alias = Sample.MarkerAttribute;
            namespace Sample;
            internal sealed class CustomAttribute(string name) : MarkerAttribute(name);
            [Alias("one")] public partial class First { }
            [Custom("two")] public partial class Second { }
            public partial class Unmarked : First { }
            """;
        var generator = new SampleGenerator(match: match);
        var run = Run(generator, source);
        Assert.Equal(count, generator.AnalysisCount);
        AssertCompiles(run.Output);
        var sources = Sources(run.Driver);
        Assert.DoesNotContain(sources, item => item.HintName == "Unmarked.g.cs");
    }

    [Fact]
    public void CollectionReportsFailuresAndConflictsButStillEmitsUnrelatedModels()
    {
        const string source = """
            namespace Sample;
            [Marker("duplicate")] public partial class First { }
            [Marker("duplicate")] public partial class Second { }
            [Marker("unique")] public partial class Third { }
            [Marker("skip")] public partial class Skipped { }
            [Marker("failure")] public partial class Failed { }
            """;
        var generator = new SampleGenerator(collect: true);
        var run = Run(generator, source);
        var diagnostics = run.Driver.GetRunResult().Diagnostics;
        var conflicts = diagnostics.Where(item => item.Id == "PIPE003").ToArray();
        Assert.Equal(2, conflicts.Length);
        Assert.Contains(diagnostics, item => item.Id == "PIPE001");
        var sources = Sources(run.Driver);
        var output = sources.Single(item => item.HintName == "Summary.g.cs").SourceText.ToString();
        Assert.Contains("Third", output);
        Assert.DoesNotContain("First", output);
        Assert.DoesNotContain("Second", output);
        Assert.DoesNotContain("Skipped", output);
        Assert.DoesNotContain("Failed", output);
    }

    [Fact]
    public void EmptyCollectionsStillGenerateTheAggregate()
    {
        var run = Run(new SampleGenerator(collect: true), "class Unrelated { }");
        AssertCompiles(run.Output);
        var sources = Sources(run.Driver);
        Assert.Contains(sources, item => item.HintName == "Summary.g.cs");
    }

    [Theory]
    [InlineData("rule")]
    [InlineData("analysis")]
    [InlineData("emit")]
    public void UnexpectedExceptionsReportFrameworkDiagnosticsWithoutDroppingOtherTargets(string stage)
    {
        const string source = "namespace Sample; [Marker] public partial class Broken { } [Marker] public partial class First { }";
        var run = Run(new SampleGenerator(failureStage: stage), source);
        var diagnostics = run.Driver.GetRunResult().Diagnostics;
        Assert.Contains(diagnostics, item => item.Id == "SGENFW001");
        Assert.DoesNotContain(diagnostics, item => item.Id is "CS8784" or "CS8785");
        var sources = Sources(run.Driver);
        Assert.Contains(sources, item => item.HintName == "First.g.cs");
        Assert.DoesNotContain(sources, item => item.HintName == "Broken.g.cs");
    }

    [Fact]
    public void CancellationPropagatesThroughSemanticAnalysis()
    {
        using var cancellation = new CancellationTokenSource();
        var generator = new SampleGenerator(cancel: cancellation);
        Assert.Throws<OperationCanceledException>(() => Run(generator, OneTarget, cancellation.Token));
    }

    [Fact]
    public void UnrelatedEditsReuseValueModelsAndRelevantEditsReplaceGeneratedFiles()
    {
        var run = Run(new SampleGenerator(), OneTarget);
        var options = (CSharpParseOptions)run.Input.SyntaxTrees.First().Options;
        var unrelated = CSharpSyntaxTree.ParseText("class Unrelated { }", options);
        var driver = run.Driver.RunGenerators(run.Input.AddSyntaxTrees(unrelated));
        var steps = driver.GetRunResult().Results.Single().TrackedSteps["SampleModels"];
        Assert.NotEmpty(steps.SelectMany(step => step.Outputs));
        foreach (var output in steps.SelectMany(step => step.Outputs))
        {
            Assert.Contains(output.Reason, new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged });
        }
        var original = run.Input.SyntaxTrees.First();
        var changed = CSharpSyntaxTree.ParseText(OneTarget.Replace("First", "Renamed"), options, path: "Input0.cs");
        var input = run.Input.ReplaceSyntaxTree(original, changed);
        driver = driver.RunGenerators(input);
        var sources = Sources(driver);
        Assert.Contains(sources, item => item.HintName == "Renamed.g.cs");
        Assert.DoesNotContain(sources, item => item.HintName == "First.g.cs");
    }

    [Fact]
    public void GeneratorInstancesAndImmutableRuleBranchesDoNotLeakCompilationState()
    {
        var generator = new BranchGenerator();
        var first = Run(generator, "namespace Sample; [Marker] public class First { }");
        var second = Run(generator, "namespace Sample; [Marker] public partial class Second { }");
        var firstSources = Sources(first.Driver);
        Assert.Contains(firstSources, source => source.HintName == "unrestricted-First.g.cs");
        Assert.DoesNotContain(firstSources, source => source.HintName == "restricted-First.g.cs");
        var secondSources = Sources(second.Driver);
        Assert.Contains(secondSources, source => source.HintName == "restricted-Second.g.cs");
        Assert.DoesNotContain(secondSources, source => source.HintName.Contains("First", StringComparison.Ordinal));
    }

    private static (GeneratorDriver Driver, CSharpCompilation Input, Compilation Output) Run(FrameworkGenerator generator, string source, CancellationToken token = default)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, options, path: "Input0.cs", cancellationToken: token);
        var input = CSharpCompilation.Create("PipelineHost", [tree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var adapter = generator.AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([adapter], parseOptions: options,
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _, token);
        return (driver, input, output);
    }

    private static ImmutableArray<GeneratedSourceResult> Sources(GeneratorDriver driver) => driver.GetRunResult().Results.Single().GeneratedSources;
    private static void AssertCompiles(Compilation output)
    {
        var errors = output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    private sealed record SampleModel(string ClassName, string Key, SourceLocation? Location);

    private sealed class SampleGenerator(bool collect = false, bool warningRule = false, AttributeMatch match = AttributeMatch.Exact,
        string? failureStage = null, CancellationTokenSource? cancel = null, bool separateContext = false, bool lateError = false) : FrameworkGenerator
    {
        public int AnalysisCount { get; private set; }

        protected override void Configure(GeneratorBuilder builder)
        {
            builder.AddInitializationSource("Marker.g.cs", AttributeSource);
            var pipeline = builder.ForClassAttribute("Sample.MarkerAttribute", match)
                .Require(Invalid, TypeRules.Partial)
                .Require(warningRule ? Warning : Invalid, new SymbolRule<INamedTypeSymbol>(input =>
                {
                    if (input.Symbol.Name == "Broken" && failureStage == "rule")
                    {
                        throw new InvalidOperationException("broken rule");
                    }
                    return warningRule ? "warning" : null;
                }))
                .Analyze(Analyze)
                .Named("SampleModels");
            if (collect)
            {
                pipeline.Collect().UniqueBy(model => model.Key, Duplicate, model => model.Location)
                    .EmitWithAssemblyName((models, assembly, output) =>
                    {
                        var names = string.Join(",", models.Select(model => model.ClassName));
                        var literal = CSharpLiteral.RenderValue(assembly + ":" + names);
                        output.AddSource("Summary.g.cs", "internal static class Summary { public const string Names = " + literal + "; }");
                    }, "SummaryOutput");
            }
            else
            {
                pipeline.Emit((model, output) =>
                {
                    output.AddSource(model.ClassName + ".g.cs", "internal static class Generated" + model.ClassName + " { }");
                    if (model.ClassName == "Broken" && failureStage == "emit")
                    {
                        throw new InvalidOperationException("broken emitter");
                    }
                }, "SampleOutput");
            }
        }

        private AnalysisResult<SampleModel> Analyze(SymbolContext<INamedTypeSymbol> input, AnalysisContext context)
        {
            AnalysisCount++;
            if (cancel is not null)
            {
                cancel.Cancel();
                context.CancellationToken.ThrowIfCancellationRequested();
            }
            if (input.Symbol.Name == "Broken" && failureStage == "analysis")
            {
                throw new InvalidOperationException("broken analyzer");
            }
            var key = input.Attribute.ConstructorArguments[0].Value as string ?? "";
            if (key == "skip")
            {
                return context.Skip<SampleModel>();
            }
            if (key == "failure")
            {
                var diagnostic = Diagnostic.Create(Invalid, input.Location, input.Symbol.Name, "analysis rejected target");
                context.ReportDiagnostic(diagnostic);
            }
            var model = new SampleModel(input.Symbol.Name, key, SourceLocation.From(input.Location));
            var result = separateContext ? new AnalysisContext(context.CancellationToken).Success(model) : context.Success(model);
            if (lateError)
            {
                var diagnostic = Diagnostic.Create(Invalid, input.Location, input.Symbol.Name, "late error");
                context.ReportDiagnostic(diagnostic);
            }
            return result;
        }
    }

    private sealed class BranchGenerator : FrameworkGenerator
    {
        protected override void Configure(GeneratorBuilder builder)
        {
            builder.AddInitializationSource("Marker.g.cs", AttributeSource);
            var original = builder.ForClassAttribute("Sample.MarkerAttribute");
            original.Require(Invalid, TypeRules.Partial).Analyze(Analyze).Emit((name, output) =>
                output.AddSource("restricted-" + name + ".g.cs", "internal class Restricted" + name + " { }"), "Restricted");
            original.Analyze(Analyze).Emit((name, output) =>
                output.AddSource("unrestricted-" + name + ".g.cs", "internal class Unrestricted" + name + " { }"), "Unrestricted");
        }

        private static AnalysisResult<string> Analyze(SymbolContext<INamedTypeSymbol> input, AnalysisContext context)
        {
            return context.Success(input.Symbol.Name);
        }
    }
}
