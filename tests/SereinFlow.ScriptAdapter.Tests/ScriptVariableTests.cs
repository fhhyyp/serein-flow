using System.Collections.Generic;
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
        var sereinLangCompiler = new SereinLangCompiler();
        var scriptCompileRequestDto = new ScriptCompileRequestDto("import { variables } from \"sereinflow\"\nvariables.set(\"count\", 42)\nreturn variables.get(\"count\")", "variables.serein", SereinLangCompiler.SupportedLanguageVersion, []);
        var result = await sereinLangCompiler.CompileAsync(scriptCompileRequestDto);
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
        var getResult = variables.Get<int>("count");
        Assert.Equal(42, getResult);
        var getResult1 = variables.Get<string>("label");
        Assert.Equal("ready", getResult1);
        var getResult2 = variables.Get<bool>("enabled");
        Assert.True(getResult2);
        var containResult = variables.Contain("empty");
        Assert.True(containResult);
        var getResult3 = variables.Get("empty");
        Assert.Null(getResult3);
        var output = Assert.IsType<ObjectValue>(result.Outputs["result"]);
        var evaluatedValue = Assert.IsType<NumberValue<int>>(output.Properties["count"]).Value;
        Assert.Equal(42, evaluatedValue);
        Assert.IsType<NullValue>(output.Properties["missing"]);
        var evaluatedValue4 = Assert.IsType<BoolValue>(output.Properties["emptyExists"]).Value;
        Assert.True(evaluatedValue4);
        var evaluatedValue5 = Assert.IsType<BoolValue>(output.Properties["missingExists"]).Value;
        Assert.False(evaluatedValue5);
        var evaluatedValue6 = Assert.IsType<BoolValue>(output.Properties["otherCaseExists"]).Value;
        Assert.False(evaluatedValue6);
    }

    [Fact]
    public async Task PreservesClrHandlesInsideStoredObjectsAndArrays()
    {
        var variables = new LibraryVariableService();
        var handle = new object ();
        variables.Set("source", handle);
        var result = await Execute("""
            import { variables } from "sereinflow"
            variables.set("handle", variables.get("source"))
            variables.set("payload", { handle = variables.get("source"), values = [1, 2, null] })
            return variables.get("payload").handle
            """, variables);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var getResult7 = variables.Get<object>("handle");
        Assert.Same(handle, getResult7);
        var payload = variables.Get<Dictionary<string, object?>>("payload")!;
        Assert.Same(handle, payload["handle"]);
        var isTypeResult = Assert.IsType<object? []>(payload["values"]);
        Assert.Equal(new object? [] { 1, 2, null }, isTypeResult);
        var evaluatedValue8 = Assert.IsType<ClrObjectValue>(result.Outputs["result"]).Value;
        Assert.Same(handle, evaluatedValue8);
    }

    [Fact]
    public void ConvertsClrScalarsAndMcpJsonWithoutLosingNumericTypesOrHandles()
    {
        object[] scalars = [42, long.MaxValue, 1.5f, 2.5d, 3.5m, true, "text", DateTime.UtcNow, TimeSpan.FromMinutes(1)];
        foreach (var scalar in scalars)
        {
            var toScriptValueResult = SereinFlowScriptValueConverter.ToScriptValue(scalar);
            var actual = SereinFlowScriptValueConverter.ToVariableValue(toScriptValueResult);
            var getTypeResult = scalar.GetType();
            var getTypeResult9 = actual!.GetType();
            Assert.Equal(getTypeResult, getTypeResult9);
            Assert.Equal(scalar, actual);
        }

        var json = System.Text.Json.JsonSerializer.SerializeToElement(new { count = 42, tags = new[] { "first", "second" } });
        var toScriptValueResult10 = SereinFlowScriptValueConverter.ToScriptValue(json);
        var obj = Assert.IsType<ObjectValue>(toScriptValueResult10);
        var evaluatedValue11 = Assert.IsType<NumberValue<int>>(obj.Properties["count"]).Value;
        Assert.Equal(42, evaluatedValue11);
        Assert.IsType<ArrayValue>(obj.Properties["tags"]);
    }

    [Fact]
    public async Task ReadsMcpJsonAndStoresItsFieldsAsNativeClrContainers()
    {
        var variables = new LibraryVariableService();
        var serializeToElementResult = System.Text.Json.JsonSerializer.SerializeToElement(new { count = 42, ids = new long[] { long.MaxValue }, note = (string? )null });
        variables.Set("mcp.payload", serializeToElementResult);
        var result = await Execute("""
            import { variables } from "sereinflow"
            var payload = variables.get("mcp.payload")
            variables.set("copy", payload)
            return payload.count
            """, variables);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var evaluatedValue12 = Assert.IsType<NumberValue<int>>(result.Outputs["result"]).Value;
        Assert.Equal(42, evaluatedValue12);
        var copy = variables.Get<Dictionary<string, object?>>("copy")!;
        var isTypeResult13 = Assert.IsType<int>(copy["count"]);
        Assert.Equal(42, isTypeResult13);
        var isTypeResult14 = Assert.IsType<object? []>(copy["ids"]);
        var singleResult = Assert.Single(isTypeResult14);
        var isTypeResult15 = Assert.IsType<long>(singleResult);
        Assert.Equal(long.MaxValue, isTypeResult15);
        Assert.Null(copy["note"]);
    }

    [Fact]
    public async Task CachedScriptsUseTheTableForEachInvocationAndRun()
    {
        var getTempPathResult = Path.GetTempPath();
        var newGuidResult = Guid.NewGuid();
        var toStringResult = newGuidResult.ToString("N");
        var root = Path.Combine(getTempPathResult, "sereinflow-script-tests", toStringResult);
        try
        {
            const string source = "import { variables } from \"sereinflow\"\nvariables.set(\"count\", variables.get(\"count\") + 1)\nreturn variables.get(\"count\")";
            var node = CreateNode(source);
            var store = new ScriptArtifactStore(root);
            var evaluatedValue20 = store.RebuildProject("project", [node.Script!]).IsSuccess;
            Assert.True(evaluatedValue20);
            var context = new TestContext();
            var executor = new SereinScriptNodeExecutor(store);
            var firstRun = new LibraryVariableService();
            var secondRun = new LibraryVariableService();
            firstRun.Set("count", 40);
            secondRun.Set("count", 7);
            foreach (var (variables, expected) in new[]
            {
                (firstRun, 41),
                (secondRun, 8),
                (firstRun, 42)
            }

            )
            {
                var runtime = CreateRuntime(variables);
                var dictionary16 = new Dictionary<string, object?>();
                NodeExecutionRequest evaluatedValue17 = new(node, context, dictionary16, runtime);
                var result = await executor.ExecuteAsync(evaluatedValue17, CancellationToken.None);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                var getResult18 = variables.Get<int>("count");
                Assert.Equal(expected, getResult18);
                var evaluatedValue19 = Assert.IsType<NumberValue<int>>(result.Outputs["result"]).Value;
                Assert.Equal(expected, evaluatedValue19);
            }

            var getResult21 = firstRun.Get<int>("count");
            Assert.Equal(42, getResult21);
            var getResult22 = secondRun.Get<int>("count");
            Assert.Equal(8, getResult22);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
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
        var snapshotResult = variables.Snapshot();
        Assert.Empty(snapshotResult);
    }

    [Fact]
    public async Task RejectsScriptFunctionsWithoutReplacingTheStoredValue()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 42);
        var result = await Execute("import { variables, log } from \"sereinflow\"\nvariables.set(\"count\", log.info)", variables);
        Assert.False(result.IsSuccess);
        var getResult23 = variables.Get<int>("count");
        Assert.Equal(42, getResult23);
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
        var snapshotResult24 = variables.Snapshot();
        Assert.Empty(snapshotResult24);
    }

    [Fact]
    public void ObjectPropertyKeysAreIndependentFromVariableNameValidation()
    {
        var expected = new Dictionary<string, object?>
        {
            [""] = 42,
            [" "] = true
        };
        var script = SereinFlowScriptValueConverter.ToScriptValue(expected);
        var toVariableValueResult = SereinFlowScriptValueConverter.ToVariableValue(script);
        var actual = Assert.IsType<Dictionary<string, object?>>(toVariableValueResult);
        Assert.Equal(42, actual[""]);
        Assert.Equal(true, actual[" "]);
    }

    [Fact]
    public void RejectsCyclicCollectionsInsteadOfRecursingWithoutBound()
    {
        var dictionary = new Dictionary<string, object?>();
        dictionary["self"] = dictionary;
        Assert.Throws<NotSupportedException>(() =>
        {
            var toScriptValueResult25 = SereinFlowScriptValueConverter.ToScriptValue(dictionary);
            return toScriptValueResult25;
        });
        var dictionary27 = new Dictionary<string, Value>();
        var obj = new ObjectValue(dictionary27);
        obj.Properties["self"] = obj;
        Assert.Throws<NotSupportedException>(() =>
        {
            var toVariableValueResult26 = SereinFlowScriptValueConverter.ToVariableValue(obj);
            return toVariableValueResult26;
        });
    }

    private static NodeDefinition CreateNode(string source)
    {
        IEnumerable<ScriptValueContract> evaluatedValue28 = [new ScriptValueContract("result", "object")];
        var createResult = ScriptNodeDefinition.Create("script", source, "1", outputs: evaluatedValue28);
        var createResult29 = NodeDefinition.Create("script", NodeType.Script, "Script", script: createResult);
        return createResult29;
    }

    private static NodeExecutionRuntime CreateRuntime(LibraryVariableService? variables, Func<bool>? cancellationProbe = null)
    {
        var newGuidResult30 = Guid.NewGuid();
        var newGuidResult31 = Guid.NewGuid();
        var nodeExecutionEnvironment = new NodeExecutionEnvironment(newGuidResult30, "project", newGuidResult31, "main", "script", 0);
        NodeExecutionRuntime evaluatedValue32 = new(nodeExecutionEnvironment, isCancellationRequested: cancellationProbe, variables: variables);
        return evaluatedValue32;
    }

    private static async Task<NodeExecutionResult> Execute(string source, LibraryVariableService? variables, Func<bool>? cancellationProbe = null)
    {
        var sereinScriptNodeExecutor = new SereinScriptNodeExecutor();
        var createNodeResult = CreateNode(source);
        var testContext = new TestContext();
        var dictionary33 = new Dictionary<string, object?>();
        var createRuntimeResult = CreateRuntime(variables, cancellationProbe);
        NodeExecutionRequest evaluatedValue34 = new(createNodeResult, testContext, dictionary33, createRuntimeResult);
        var executeAsyncResult = await sereinScriptNodeExecutor.ExecuteAsync(evaluatedValue34, CancellationToken.None);
        return executeAsyncResult;
    }

    private sealed class TestContext : IExecutionContext
    {
        public object? Read(string key) => key == "projectId" ? "project" : null;
        public void Write(string key, object? value)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyDictionary<string, object?> Snapshot()
        {
            var dictionary35 = new Dictionary<string, object?>();
            return dictionary35;
        }
    }
}
