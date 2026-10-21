using System.Text.Json;
using SereinFlow.ScriptAdapter;

namespace SereinFlow.ScriptAdapter.Tests;

public sealed class BoundedAuditProjectionTests
{
    [Fact]
    public void JsonObjectsArraysAndScalarsKeepTheirShapes()
    {
        using var source = JsonDocument.Parse("{\"rows\":[1, true, null, \"中文\"],\"flag\":false}");
        var projected = ScriptValueConverter.ToBoundedAuditValue(source.RootElement);
        var json = JsonSerializer.Serialize(projected);
        using var actual = JsonDocument.Parse(json);
        var equal = JsonElement.DeepEquals(source.RootElement, actual.RootElement);
        Assert.True(equal, json);
    }

    [Fact]
    public void SharedItemBudgetAndDepthLimitBoundNestedAndCyclicGraphs()
    {
        var cycle = new Dictionary<string, object?>();
        cycle["self"] = cycle;
        var projectedCycle = ScriptValueConverter.ToBoundedAuditValue(cycle, maxDepth: 4);
        var cycleJson = JsonSerializer.Serialize(projectedCycle);
        Assert.Contains("DepthLimit", cycleJson, StringComparison.Ordinal);
        var graph = Enumerable.Range(0, 100).Select(static _ =>
        {
            var values = Enumerable.Range(0, 100);
            return values;
        });
        var projectedGraph = ScriptValueConverter.ToBoundedAuditValue(graph, maxItems: 30);
        var graphJson = JsonSerializer.Serialize(projectedGraph);
        Assert.Contains("ItemLimit", graphJson, StringComparison.Ordinal);
        Assert.True(graphJson.Length < 1000);
    }
}
