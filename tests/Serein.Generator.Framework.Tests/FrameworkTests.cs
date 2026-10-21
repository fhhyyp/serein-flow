using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serein.Generator.Framework;

namespace Serein.Generator.Framework.Tests;

public sealed class FrameworkTests
{
    [Fact]
    public void ImmutableSequencesCompareElementsIncludingDefaultAndEmpty()
    {
        Assert.Equal(default, new EquatableArray<string>([]));
        var first = new EquatableArray<string>(["one", "two"]);
        var second = new EquatableArray<string>(["one", "two"]);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new EquatableArray<string>(["two", "one"]));
        var mutable = new[] { "before" };
        var snapshot = new EquatableArray<string>(mutable);
        mutable[0] = "after";
        Assert.Equal("before", snapshot[0]);
    }

    [Fact]
    public void CodeBlocksRestoreIndentationWhenAnEmitterThrows()
    {
        var code = new CodeBuilder<string>(new GenerationContext(default), "model");
        Assert.Throws<InvalidOperationException>(() => code.AppendTab(() => throw new InvalidOperationException()));
        code.AppendCode("class Sample").AppendCodeBlock(() => code.AppendCode("int value;"));
        Assert.Equal("class Sample\n{\n    int value;\n}\n", code.ToCode());
    }

    [Fact]
    public void GenerationFailureDiscardsPartialSourcesAndReportsTargetAndCause()
    {
        var output = GenerationResult.Create("Sample", context =>
        {
            context.AddSource("partial.g.cs", "invalid");
            throw new InvalidOperationException("intentional failure");
        });
        Assert.Empty(output.Sources);
        var diagnostic = Assert.Single(output.Diagnostics).ToDiagnostic();
        Assert.Equal("SGENFW001", diagnostic.Id);
        Assert.Contains("Sample", diagnostic.GetMessage());
        Assert.Contains("intentional failure", diagnostic.GetMessage());
    }

    [Fact]
    public void CancellationPropagatesWithoutReportingAGeneratorFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => GenerationResult.Create("Sample", _ => { }, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => GenerationResult.Create("Sample", _ => throw new OperationCanceledException()));
    }

    [Fact]
    public void DiagnosticsRetainIdentityMessageAndNavigableLocationWithoutSyntaxTrees()
    {
        var tree = CSharpSyntaxTree.ParseText("class Sample { }", path: "sample.cs");
        var descriptor = new DiagnosticDescriptor("TEST001", "Test", "Invalid '{0}'", "Test", DiagnosticSeverity.Error, true);
        var snapshot = DiagnosticInfo.From(Diagnostic.Create(descriptor, tree.GetRoot().GetLocation(), "Sample"));
        var restored = snapshot.ToDiagnostic();
        Assert.Equal("TEST001", restored.Id);
        Assert.Equal("Invalid 'Sample'", restored.GetMessage());
        Assert.Equal("sample.cs", restored.Location.GetLineSpan().Path);
        Assert.Equal(tree.GetRoot().Span, restored.Location.SourceSpan);
        Assert.Null(restored.Location.SourceTree);
        Assert.Equal(snapshot, DiagnosticInfo.From(Diagnostic.Create(descriptor, tree.GetRoot().GetLocation(), "Sample")));
    }

    [Fact]
    public void LiteralRenderingIsCultureIndependentAndCompilesWithPreciseNumericTypes()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var values = new object[] { "quote\"\\\n", '\'', true, 1.25f, 1.25d, 1.25m, ulong.MaxValue, long.MaxValue, uint.MaxValue, double.NaN, float.PositiveInfinity };
            var expressions = values.Select(CSharpLiteral.RenderValue).ToArray();
            var source = "class Sample { object[] Values = new object[] { " + string.Join(",", expressions) + " }; }";
            var compilation = Compilation(source);
            Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
            Assert.Equal("1.25M", CSharpLiteral.RenderValue(1.25m));
            Assert.Equal("1.25F", CSharpLiteral.RenderValue(1.25f));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void AttributeDataPreservesArraysEnumsTypesNullAndNamedArguments()
    {
        var compilation = Compilation("""
            using System;
            public enum Mode { First, Second }
            public class MarkerAttribute(string name, params Mode[] modes) : Attribute
            {
                public Type? Type { get; set; }
                public string? Text { get; set; }
                public double Amount { get; set; }
            }
            [Marker("quoted\"", Mode.Second, Type = typeof(string), Text = null, Amount = 1.5)]
            public class Sample { }
            """);
        var attribute = AttributeReader.Find(compilation.GetTypeByMetadataName("Sample")!, "MarkerAttribute")!;
        Assert.Equal(1.5, AttributeReader.NamedValue(attribute, "Amount", 0d));
        var expression = CSharpLiteral.RenderAttribute(attribute);
        var updated = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Consumer { object Value = " + expression + "; }"));
        Assert.Empty(updated.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Contains("typeof(string)", expression);
        Assert.Contains("@Text = null", expression);
    }

    [Fact]
    public void StableNamesDistinguishTypesAndEscapeKeywords()
    {
        Assert.NotEqual(SymbolNames.StableIdentifier("Type", "A.B"), SymbolNames.StableIdentifier("Type", "A_B"));
        Assert.Equal(SymbolNames.StableIdentifier("Type", "A.B"), SymbolNames.StableIdentifier("Type", "A.B"));
        Assert.Equal("@class", SymbolNames.Identifier("class"));
        Assert.Equal("Name", SymbolNames.Identifier("Name"));
    }

    [Fact]
    public void NestedGenericAttributeNamesUseMetadataIdentityWithoutSourceEscaping()
    {
        var compilation = Compilation("""
            namespace @event
            {
                public class Outer
                {
                    public class MarkerAttribute<T> : System.Attribute { }
                }
                [Outer.Marker<int>] public class Sample { }
            }
            """);
        var type = compilation.GetTypeByMetadataName("event.Sample")!;
        var attribute = AttributeReader.Find(type, "event.Outer+MarkerAttribute`1");
        Assert.NotNull(attribute);
        var metadataName = SymbolNames.MetadataName(attribute.AttributeClass!);
        Assert.Equal("event.Outer+MarkerAttribute`1", metadataName);
    }

    internal static CSharpCompilation Compilation(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create("TestHost", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
