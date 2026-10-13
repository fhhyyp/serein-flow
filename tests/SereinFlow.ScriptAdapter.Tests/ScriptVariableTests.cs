using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptModules;
using ScriptLang.Runtime;
using LibraryVariableService = SereinFlow.Library.LibraryVariableService;

namespace SereinFlow.ScriptAdapter.Tests;

public sealed class ScriptVariableTests
{
    [Fact]
    public async Task CompilerRegistersVariablesWithoutRequiringOrExecutingARun()
    {
        var result = await new SereinLangCompiler().CompileAsync(new ScriptCompileRequestDto(
            "import { variables } from \"sereinflow\"\nvariables.set(\"count\", 42)\nreturn variables.get(\"count\")",
            "variables.serein", SereinLangCompiler.SupportedLanguageVersion, []));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task StoresNativeScalarsAndDistinguishesMissingNullAndCaseSensitiveNames()
    {
        var variables = new LibraryVariableService();
        var result = await Execute("""
            import { variables } from "sereinflow"
            variables.set("count", 42)
            variables.set("label", "ready")
            variables.set("enabled", true)
            variables.set("empty", null)
            return { count = variables.get("count"), missing = variables.get("missing"),
                emptyExists = variables.contain("empty"), missingExists = variables.contain("missing"),
                otherCaseExists = variables.contain("Count") }
            """, variables);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(42, variables.Get<int>("count"));
        Assert.Equal("ready", variables.Get<string>("label"));
        Assert.True(variables.Get<bool>("enabled"));
        Assert.True(variables.Contain("empty"));
        Assert.Null(variables.Get("empty"));
        var output = Assert.IsType<ObjectValue>(result.Outputs["result"]);
        Assert.Equal(42, Assert.IsType<NumberValue<int>>(output.Properties["count"]).Value);
        Assert.IsType<NullValue>(output.Properties["missing"]);
        Assert.True(Assert.IsType<BoolValue>(output.Properties["emptyExists"]).Value);
        Assert.False(Assert.IsType<BoolValue>(output.Properties["missingExists"]).Value);
        Assert.False(Assert.IsType<BoolValue>(output.Properties["otherCaseExists"]).Value);
    }

    [Fact]
    public async Task PreservesClrHandlesInsideStoredObjectsAndArrays()
    {
        var variables = new LibraryVariableService();
        var handle = new object();
        variables.Set("source", handle);
        var result = await Execute("""
            import { variables } from "sereinflow"
            variables.set("handle", variables.get("source"))
            variables.set("payload", { handle = variables.get("source"), values = [1, 2, null] })
            return variables.get("payload").handle
            """, variables);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Same(handle, variables.Get<object>("handle"));
        var payload = variables.Get<Dictionary<string, object?>>("payload")!;
        Assert.Same(handle, payload["handle"]);
        Assert.Equal(new object?[] { 1, 2, null }, Assert.IsType<object?[]>(payload["values"]));
        Assert.Same(handle, Assert.IsType<ClrObjectValue>(result.Outputs["result"]).Value);
    }

    [Fact]
    public void ConvertsClrScalarsAndMcpJsonWithoutLosingNumericTypesOrHandles()
    {
        object[] scalars = [42, long.MaxValue, 1.5f, 2.5d, 3.5m, true, "text", DateTime.UtcNow, TimeSpan.FromMinutes(1)];
        foreach (var scalar in scalars)
        {
            var actual = SereinFlowScriptValueConverter.ToVariableValue(SereinFlowScriptValueConverter.ToScriptValue(scalar));
            Assert.Equal(scalar.GetType(), actual!.GetType());
            Assert.Equal(scalar, actual);
        }
        var json = System.Text.Json.JsonSerializer.SerializeToElement(new { count = 42, tags = new[] { "first", "second" } });
        var obj = Assert.IsType<ObjectValue>(SereinFlowScriptValueConverter.ToScriptValue(json));
        Assert.Equal(42, Assert.IsType<NumberValue<int>>(obj.Properties["count"]).Value);
        Assert.IsType<ArrayValue>(obj.Properties["tags"]);
    }

    [Fact]
    public async Task ReadsMcpJsonAndStoresItsFieldsAsNativeClrContainers()
    {
        var variables = new LibraryVariableService();
        variables.Set("mcp.payload", System.Text.Json.JsonSerializer.SerializeToElement(
            new { count = 42, ids = new long[] { long.MaxValue }, note = (string?)null }));
        var result = await Execute("""
            import { variables } from "sereinflow"
            var payload = variables.get("mcp.payload")
            variables.set("copy", payload)
            return payload.count
            """, variables);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(42, Assert.IsType<NumberValue<int>>(result.Outputs["result"]).Value);
        var copy = variables.Get<Dictionary<string, object?>>("copy")!;
        Assert.Equal(42, Assert.IsType<int>(copy["count"]));
        Assert.Equal(long.MaxValue, Assert.IsType<long>(Assert.Single(Assert.IsType<object?[]>(copy["ids"]))));
        Assert.Null(copy["note"]);
    }

    [Fact]
    public async Task CachedScriptsUseTheTableForEachInvocationAndRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "sereinflow-script-tests", Guid.NewGuid().ToString("N"));
        try
        {
            const string source = "import { variables } from \"sereinflow\"\nvariables.set(\"count\", variables.get(\"count\") + 1)\nreturn variables.get(\"count\")";
            var node = CreateNode(source);
            var store = new ScriptArtifactStore(root);
            Assert.True(store.RebuildProject("project", [node.Script!]).IsSuccess);
            var context = new TestContext();
            var executor = new SereinScriptNodeExecutor(store);
            var firstRun = new LibraryVariableService();
            var secondRun = new LibraryVariableService();
            firstRun.Set("count", 40);
            secondRun.Set("count", 7);
            foreach (var (variables, expected) in new[] { (firstRun, 41), (secondRun, 8), (firstRun, 42) })
            {
                var runtime = CreateRuntime(variables);
                var result = await executor.ExecuteAsync(new(node, context, new Dictionary<string, object?>(), runtime), CancellationToken.None);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                Assert.Equal(expected, variables.Get<int>("count"));
                Assert.Equal(expected, Assert.IsType<NumberValue<int>>(result.Outputs["result"]).Value);
            }
            Assert.Equal(42, firstRun.Get<int>("count"));
            Assert.Equal(8, secondRun.Get<int>("count"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    [InlineData("42")]
    public async Task RejectsInvalidNamesWithoutWriting(string nameExpression)
    {
        var variables = new LibraryVariableService();
        var result = await Execute($"import {{ variables }} from \"sereinflow\"\nvariables.set({nameExpression}, 42)", variables);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionBranch.Error, result.NextBranch);
        Assert.Empty(variables.Snapshot());
    }

    [Fact]
    public async Task RejectsScriptFunctionsWithoutReplacingTheStoredValue()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 42);
        var result = await Execute("import { variables, log } from \"sereinflow\"\nvariables.set(\"count\", log.info)", variables);

        Assert.False(result.IsSuccess);
        Assert.Equal(42, variables.Get<int>("count"));
    }

    [Fact]
    public async Task RequiresAHostTableRatherThanCreatingAnIsolatedNodeTable()
    {
        var result = await Execute("import { variables } from \"sereinflow\"\nvariables.set(\"count\", 42)", null);

        Assert.False(result.IsSuccess);
        Assert.Contains("run variable table", result.ErrorMessage);
    }

    [Fact]
    public async Task CancelledRuntimeCannotWriteVariables()
    {
        var variables = new LibraryVariableService();
        var result = await Execute("import { variables } from \"sereinflow\"\nvariables.set(\"count\", 42)", variables, () => true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScriptErrorCodes.Cancelled, result.ErrorCode);
        Assert.Empty(variables.Snapshot());
    }

    [Fact]
    public void ObjectPropertyKeysAreIndependentFromVariableNameValidation()
    {
        var expected = new Dictionary<string, object?> { [""] = 42, [" "] = true };
        var script = SereinFlowScriptValueConverter.ToScriptValue(expected);
        var actual = Assert.IsType<Dictionary<string, object?>>(SereinFlowScriptValueConverter.ToVariableValue(script));

        Assert.Equal(42, actual[""]);
        Assert.Equal(true, actual[" "]);
    }

    [Fact]
    public void RejectsCyclicCollectionsInsteadOfRecursingWithoutBound()
    {
        var dictionary = new Dictionary<string, object?>();
        dictionary["self"] = dictionary;
        Assert.Throws<NotSupportedException>(() => SereinFlowScriptValueConverter.ToScriptValue(dictionary));
        var obj = new ObjectValue(new Dictionary<string, Value>());
        obj.Properties["self"] = obj;
        Assert.Throws<NotSupportedException>(() => SereinFlowScriptValueConverter.ToVariableValue(obj));
    }

    private static NodeDefinition CreateNode(string source)
        => NodeDefinition.Create("script", NodeType.Script, "Script", script: ScriptNodeDefinition.Create(
            "script", source, "1", outputs: [new ScriptValueContract("result", "object")]));

    private static NodeExecutionRuntime CreateRuntime(LibraryVariableService? variables, Func<bool>? cancellationProbe = null)
        => new(new NodeExecutionEnvironment(Guid.NewGuid(), "project", Guid.NewGuid(), "main", "script", 0),
            isCancellationRequested: cancellationProbe, variables: variables);

    private static async Task<NodeExecutionResult> Execute(string source, LibraryVariableService? variables, Func<bool>? cancellationProbe = null)
        => await new SereinScriptNodeExecutor().ExecuteAsync(new(CreateNode(source), new TestContext(),
            new Dictionary<string, object?>(), CreateRuntime(variables, cancellationProbe)), CancellationToken.None);

    private sealed class TestContext : IExecutionContext
    {
        public object? Read(string key) => key == "projectId" ? "project" : null;
        public void Write(string key, object? value) => throw new NotSupportedException();
        public IReadOnlyDictionary<string, object?> Snapshot() => new Dictionary<string, object?>();
    }
}
