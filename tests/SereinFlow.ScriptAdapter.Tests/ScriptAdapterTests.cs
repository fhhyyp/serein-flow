using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter.Tests;

public sealed class ScriptAdapterTests
{
    [Fact]
    public void ConverterRoundTripsJsonCompatibleValues()
    {
        var value = ScriptValueConverter.ToScriptValue(new Dictionary<string, object?>
        {
            ["name"] = "Ada",
            ["enabled"] = true,
            ["scores"] = new List<int> { 1, 2, 3 },
            ["missing"] = null
        });

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(ScriptValueConverter.ToClrValue(value));
        Assert.Equal("Ada", result["name"]);
        Assert.Equal(true, result["enabled"]);
        Assert.Null(result["missing"]);
    }

    [Fact]
    public async Task ExecutesSingleOutputScript()
    {
        var node = CreateScriptNode("return amount + 1", [new("amount", "number", true)], [new("result", "number")]);
        var result = await Execute(node, new Dictionary<string, object?> { ["amount"] = 41 });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var output = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
        Assert.Equal(42, ScriptValueTypeConverter.Convert(output, typeof(int)));
    }

    [Fact]
    public async Task RegistersTheNamedScriptGlobalWhenInputUsesAStableConnectorIdAndCachedArtifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "sereinflow-script-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var definition = CreateScriptDefinition(
                "return input1",
                [new("input1", "System.Object", true, Id: "input-1")],
                [new("result", "ScriptLang.Runtime.Value", false, Id: "result")]);
            var node = NodeDefinition.Create("node", NodeType.Script, "Script", script: definition);
            var store = new ScriptArtifactStore(root);
            Assert.True(store.RebuildProject("project", [definition]).IsSuccess);

            var context = new TestContext();
            context.Write("projectId", "project");
            var executor = new SereinScriptNodeExecutor(store);
            var expected = new Dictionary<string, object?> { ["batchNo"] = "B01", ["passed"] = false };
            var result = await executor.ExecuteAsync(
                new NodeExecutionRequest(node, context, new Dictionary<string, object?> { ["input-1"] = expected }),
                CancellationToken.None);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var output = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
            var actual = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(ScriptValueConverter.ToClrValue(output));
            Assert.Equal("B01", actual["batchNo"]);
            Assert.Equal(false, actual["passed"]);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RegistersDeclaredButUnusedInputsWhenLoadingACachedArtifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "sereinflow-script-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var definition = CreateScriptDefinition(
                "return 0",
                [new("input1", "System.Object", false, Id: "input-1")],
                [new("result", "ScriptLang.Runtime.Value", false, Id: "result")]);
            var node = NodeDefinition.Create("node", NodeType.Script, "Script", script: definition);
            var store = new ScriptArtifactStore(root);
            Assert.True(store.RebuildProject("project", [definition]).IsSuccess);

            var context = new TestContext();
            context.Write("projectId", "project");
            var executor = new SereinScriptNodeExecutor(store);
            var result = await executor.ExecuteAsync(
                new NodeExecutionRequest(node, context, new Dictionary<string, object?>
                {
                    ["input-1"] = new Dictionary<string, object?> { ["batchNo"] = "B01" }
                }),
                CancellationToken.None);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var output = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
            Assert.Equal(0, ScriptValueTypeConverter.Convert(output, typeof(int)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MapsObjectToMultipleOutputs()
    {
        var node = CreateScriptNode("return { sum = left + right, label = \"done\" }", [new("left", "number"), new("right", "number")], [new("sum", "number"), new("label", "string")]);
        var result = await Execute(node, new Dictionary<string, object?> { ["left"] = 2, ["right"] = 3 });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(5, ScriptValueTypeConverter.Convert(Assert.IsAssignableFrom<Value>(result.Outputs["sum"]), typeof(int)));
        Assert.Equal("done", ScriptValueTypeConverter.Convert(Assert.IsAssignableFrom<Value>(result.Outputs["label"]), typeof(string)));
    }

    [Fact]
    public async Task RejectsMissingAndUnknownInputs()
    {
        var node = CreateScriptNode("return amount", [new("amount", "number", true)], [new("result", "number")]);

        var missing = await Execute(node, new Dictionary<string, object?>());
        var unknown = await Execute(node, new Dictionary<string, object?> { ["amount"] = 1, ["other"] = 2 });

        Assert.Equal("script.input_missing", missing.ErrorCode);
        Assert.Contains("Required script input", missing.ErrorMessage);
        Assert.Equal("script.input_unknown", unknown.ErrorCode);
        Assert.Contains("Unknown script input", unknown.ErrorMessage);
    }

    [Fact]
    public async Task CancellationIsReturnedAsStructuredFailure()
    {
        var node = CreateScriptNode("for i in range(0, 1000000000) { var keep = i }", [], [new("result", "number")]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await Execute(node, new Dictionary<string, object?>(), cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("script.cancelled", result.ErrorCode);
    }

    [Fact]
    public async Task ScriptExceptionsUseErrorBranch()
    {
        var node = CreateScriptNode("return missing_value", [], [new("result", "number")]);

        var result = await Execute(node, new Dictionary<string, object?>());

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionBranch.Error, result.NextBranch);
        Assert.NotNull(result.ErrorCode);
    }

    [Fact]
    public void RebuildsArtifactsAndDoesNotKeepOldArtifactAfterFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "sereinflow-script-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScriptArtifactStore(root);
            var valid = CreateScriptDefinition("return 1", [], [new("result", "number")]);
            var first = store.RebuildProject("project", [valid]);
            Assert.True(first.IsSuccess);
            Assert.True(File.Exists(Path.Combine(store.GetProjectPath("project"), "node.ssc")));

            var invalid = CreateScriptDefinition("return (", [], [new("result", "number")]);
            var second = store.RebuildProject("project", [invalid]);

            Assert.False(second.IsSuccess);
            Assert.False(File.Exists(Path.Combine(store.GetProjectPath("project"), "node.ssc")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ArtifactFingerprintIncludesTheScriptInputContract()
    {
        var root = Path.Combine(Path.GetTempPath(), "sereinflow-script-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScriptArtifactStore(root);
            var first = CreateScriptDefinition("return 1", [new("amount", "number", Id: "amount-id")], [new("result", "number")]);
            var changedContract = CreateScriptDefinition("return 1", [new("quantity", "number", Id: "quantity-id")], [new("result", "number")]);

            Assert.True(store.RebuildProject("project", [first]).IsSuccess);
            Assert.False(store.TryLoad("project", changedContract, out _, out _));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ConvertsRuntimeValuesOnlyAtTheTargetClrBoundary()
    {
        var dateTime = new DateTime(2026, 8, 27, 9, 30, 0, DateTimeKind.Utc);
        var timeSpan = TimeSpan.FromMinutes(5);
        var value = NumberValueFactory.Create(42);
        var array = new ArrayValue([NumberValueFactory.Create(1), NumberValueFactory.Create(2)]);
        var obj = new ObjectValue(new Dictionary<string, Value>
        {
            ["Name"] = StringValue.Create("Ada"),
            ["Count"] = NumberValueFactory.Create(3)
        });

        Assert.Equal("text", ScriptValueTypeConverter.Convert(StringValue.Create("text"), typeof(string)));
        Assert.Equal(42L, ScriptValueTypeConverter.Convert(value, typeof(long)));
        Assert.True((bool)ScriptValueTypeConverter.Convert(BoolValue.True, typeof(bool))!);
        Assert.Equal(dateTime, ScriptValueTypeConverter.Convert(new DateTimeValue(dateTime), typeof(DateTime)));
        Assert.Equal(timeSpan, ScriptValueTypeConverter.Convert(new TimeSpanValue(timeSpan), typeof(TimeSpan)));
        Assert.Equal([1, 2], Assert.IsType<List<int>>(ScriptValueTypeConverter.Convert(array, typeof(List<int>))));
        Assert.Equal([1, 2], Assert.IsType<int[]>(ScriptValueTypeConverter.Convert(array, typeof(int[]))));
        Assert.Equal(3, Assert.IsType<SamplePayload>(ScriptValueTypeConverter.Convert(obj, typeof(SamplePayload))).Count);
        Assert.Equal("Ada", Assert.IsType<SamplePayload>(ScriptValueTypeConverter.Convert(obj, typeof(SamplePayload))).Name);
        Assert.Equal("Ada", Assert.IsType<Dictionary<string, string>>(ScriptValueTypeConverter.Convert(
            new ObjectValue(new Dictionary<string, Value> { ["name"] = StringValue.Create("Ada") }),
            typeof(Dictionary<string, string>)))["name"]);
    }

    [Fact]
    public void PreservesSpecialRuntimeValuesAndUsesSafeAuditSummaries()
    {
        var raw = StringValue.Create("unchanged");
        Assert.Same(raw, ScriptValueTypeConverter.Convert(raw, typeof(Value)));
        Assert.Same(raw, ScriptValueTypeConverter.Convert(raw, typeof(StringValue)));

        var carrier = new SamplePayload { Name = "Worker only", Count = 7 };
        var clrObject = new ClrObjectValue(carrier);
        Assert.Same(carrier, ScriptValueTypeConverter.Convert(clrObject, typeof(SamplePayload)));
        var objectSummary = Assert.IsType<Dictionary<string, object?>>(ScriptValueConverter.ToAuditValue(clrObject));
        Assert.Equal("ClrObject", objectSummary["$kind"]);
        Assert.Equal(typeof(SamplePayload).FullName, objectSummary["clrType"]);

        var method = typeof(ScriptAdapterTests).GetMethod(nameof(StaticMethod), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var clrMethod = new ClrMethodValue(method, null!);
        Assert.Same(method, ScriptValueTypeConverter.Convert(clrMethod, typeof(System.Reflection.MethodInfo)));
        var methodSummary = Assert.IsType<Dictionary<string, object?>>(ScriptValueConverter.ToAuditValue(clrMethod));
        Assert.Equal("ClrMethod", methodSummary["$kind"]);

        var error = Assert.Throws<ScriptValueConversionException>(() => ScriptValueTypeConverter.Convert(StringValue.Create("x"), typeof(int)));
        Assert.Contains("cannot be converted", error.Message);
        Assert.Contains("无法转换", error.Message);
    }

    private static async Task<NodeExecutionResult> Execute(NodeDefinition node, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        var executor = new SereinScriptNodeExecutor();
        return await executor.ExecuteAsync(new NodeExecutionRequest(node, new TestContext(), inputs), cancellationToken);
    }

    private static NodeDefinition CreateScriptNode(
        string source,
        IEnumerable<ScriptValueContract> inputs,
        IEnumerable<ScriptValueContract> outputs)
    {
        var script = CreateScriptDefinition(source, inputs, outputs);
        return NodeDefinition.Create("node", NodeType.Script, "Script", script: script);
    }

    private static ScriptNodeDefinition CreateScriptDefinition(
        string source,
        IEnumerable<ScriptValueContract> inputs,
        IEnumerable<ScriptValueContract> outputs)
        => ScriptNodeDefinition.Create("node", source, "1", inputs: inputs, outputs: outputs);

    private sealed class TestContext : IExecutionContext
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public object? Read(string key) => _values.GetValueOrDefault(key);

        public void Write(string key, object? value) => _values[key] = value;

        public IReadOnlyDictionary<string, object?> Snapshot() => _values;
    }

    private sealed class SamplePayload
    {
        public string? Name { get; set; }

        public int Count { get; set; }
    }

    private static void StaticMethod()
    {
    }
}
