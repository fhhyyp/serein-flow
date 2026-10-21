using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ScriptLang.Generator.Tests;

public sealed class PrototypeBoundaryTests
{
    private const string Body = """
        [ScriptLang.PrototypeExtension]
        public partial class Example
        {
            public partial bool IsTarget(ScriptLang.Runtime.Value value) => true;
            [ScriptLang.PrototypeFunction(Name = "echo")]
            private static ScriptLang.Runtime.Value Echo(ScriptLang.Runtime.Value value) => value;
        }
        """;

    [Theory]
    [InlineData("")]
    [InlineData("namespace Sample;")]
    [InlineData("namespace @event;")]
    [InlineData("namespace Outer { namespace Inner {", "} }")]
    public void GlobalFileScopedAndNestedNamespacesCompile(string prefix, string suffix = "")
    {
        var (driver, compilation) = PrototypeGeneratorTests.Run(prefix + Body + suffix);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void IdenticallyNamedTypesInDifferentNamespacesUseDistinctFiles()
    {
        var (driver, compilation) = PrototypeGeneratorTests.Run("namespace First { " + Body + " } namespace Second { " + Body + " }");
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Equal(3, driver.GetRunResult().Results.Single().GeneratedSources.Length);
    }

    [Fact]
    public void MembersFromAllPartialDeclarationsAreIncludedExactlyOnce()
    {
        var source = "namespace Sample { " + Body + """
            [ScriptLang.PrototypeExtension]
            public partial class Example
            {
                [ScriptLang.PrototypeFunction(Name = "second")]
                private static ScriptLang.Runtime.Value Echo() => ScriptLang.Runtime.Value.Null;
            }
            }
            """;
        var (driver, compilation) = PrototypeGeneratorTests.Run(source);
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        var output = Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources.Where(item => item.HintName != "ScriptPrototypeToolkits.g.cs")).SourceText.ToString();
        Assert.Contains("_prototypeMethods[\"echo\"]", output);
        Assert.Contains("_prototypeMethods[\"second\"]", output);
    }

    [Fact]
    public void TaskReturningMethodsDoNotRequireAsyncModifierAndEngineDoesNotCountAsUserArgument()
    {
        var source = """
            namespace Sample;
            [ScriptLang.PrototypeExtension]
            public partial class Example
            {
                public partial bool IsTarget(ScriptLang.Runtime.Value value) => true;
                [ScriptLang.PrototypeFunction(Name = "echo")]
                private static System.Threading.Tasks.Task<ScriptLang.Runtime.Value> Echo(ScriptLang.ScriptEngine engine)
                    => System.Threading.Tasks.Task.FromResult(ScriptLang.Runtime.Value.Null);
            }
            """;
        var (driver, compilation) = PrototypeGeneratorTests.Run(source);
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        var output = driver.GetRunResult().Results.Single().GeneratedSources.Last().SourceText.ToString();
        Assert.Contains("if (args.Count < 0)", output);
        Assert.Contains("await Echo(env)", output);
    }

    [Theory]
    [InlineData("class Example", "PROTOTYPE002")]
    [InlineData("partial class Example<T>", "PROTOTYPE002")]
    public void UnsupportedTypeShapesProduceActionableDiagnostics(string declaration, string id)
    {
        var (driver, _) = PrototypeGeneratorTests.Run(Body.Replace("partial class Example", declaration));
        Assert.Contains(driver.GetRunResult().Diagnostics, item => item.Id == id);
        Assert.DoesNotContain(driver.GetRunResult().Diagnostics, item => item.Id is "CS8785" or "SGENFW001");
    }

    [Fact]
    public void ConflictingScriptNamesAreDiagnosedInsteadOfDroppingClrOverloads()
    {
        var source = Body.Replace("private static ScriptLang.Runtime.Value Echo", "[ScriptLang.PrototypeProperty(Name = \"echo\")] private static ScriptLang.Runtime.Value Echo");
        var (driver, _) = PrototypeGeneratorTests.Run(source);
        Assert.Contains(driver.GetRunResult().Diagnostics, item => item.Id == "PROTOTYPE003");
    }

    [Fact]
    public void MultipleAliasesOnOneMethodUseDistinctGeneratedLocals()
    {
        var source = Body.Replace("[ScriptLang.PrototypeFunction(Name = \"echo\")]",
            "[ScriptLang.PrototypeFunction(Name = \"echo\")] [ScriptLang.PrototypeFunction(Name = \"other\")]");
        var (driver, compilation) = PrototypeGeneratorTests.Run(source);
        var errors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
        var sources = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Contains(sources, output => output.SourceText.ToString().Contains("_prototypeMethods[\"other\"]", StringComparison.Ordinal));
    }

    [Fact]
    public void UnrelatedEditReusesOutputAndAliasEditUpdatesIt()
    {
        var (driver, updated) = PrototypeGeneratorTests.Run(Body);
        var generatedTrees = driver.GetRunResult().Results.Single().GeneratedSources.Select(source => source.SyntaxTree);
        var input = updated.RemoveSyntaxTrees(generatedTrees);
        var options = (CSharpParseOptions)input.SyntaxTrees.First().Options;
        var unrelated = CSharpSyntaxTree.ParseText("class Unrelated { }", options);
        driver = driver.RunGeneratorsAndUpdateCompilation(input.AddSyntaxTrees(unrelated), out _, out _);
        var steps = driver.GetRunResult().Results.Single().TrackedSteps["ScriptPrototypeOutput"];
        Assert.All(steps.SelectMany(step => step.Outputs), output => Assert.Contains(output.Reason,
            new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged }));
        var original = input.SyntaxTrees.First();
        var changed = CSharpSyntaxTree.ParseText(Body.Replace("\"echo\"", "\"renamed\""), options);
        driver = driver.RunGeneratorsAndUpdateCompilation(input.ReplaceSyntaxTree(original, changed), out _, out _);
        Assert.Contains(driver.GetRunResult().Results.Single().GeneratedSources, source => source.SourceText.ToString().Contains("_prototypeMethods[\"renamed\"]", StringComparison.Ordinal));
    }
}
