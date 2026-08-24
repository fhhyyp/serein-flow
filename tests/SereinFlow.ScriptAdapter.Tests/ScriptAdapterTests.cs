using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;

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
        Assert.Equal(42, result.Outputs["result"]);
    }

    [Fact]
    public async Task MapsObjectToMultipleOutputs()
    {
        var node = CreateScriptNode("return { sum = left + right, label = \"done\" }", [new("left", "number"), new("right", "number")], [new("sum", "number"), new("label", "string")]);
        var result = await Execute(node, new Dictionary<string, object?> { ["left"] = 2, ["right"] = 3 });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(5, result.Outputs["sum"]);
        Assert.Equal("done", result.Outputs["label"]);
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
}
