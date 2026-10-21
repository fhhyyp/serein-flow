using System.Text.Json;
using SereinFlow.Mcp.Tools;

namespace SereinFlow.Mcp.Tests;

public sealed class McpSchemaCompatibilityTests
{
    [Fact]
    public void CatalogPreservesOriginalJsonContracts()
    {
        var assembly = typeof(McpSchemaCompatibilityTests).Assembly;
        using var stream = assembly.GetManifestResourceStream("SereinFlow.Mcp.Tests.tool-schemas.json");
        Assert.NotNull(stream);
        using var expected = JsonDocument.Parse(stream);
        var catalog = SereinFlowMcpToolCatalogFactory.Create();
        var actual = JsonSerializer.SerializeToElement(catalog.Descriptors);
        var expectedCount = expected.RootElement.GetArrayLength();
        var actualCount = actual.GetArrayLength();
        Assert.Equal(expectedCount, actualCount);
        for (var index = 0; index < actual.GetArrayLength(); index++)
        {
            var matches = JsonElement.DeepEquals(expected.RootElement[index], actual[index]);
            var name = catalog.Descriptors[index].Name;
            Assert.True(matches, $"Tool JSON contract changed: {name}");
        }
    }

    [Fact]
    public void ObjectSchemasRejectUndeclaredAndDuplicateRequiredProperties()
    {
        var stringSchema = McpToolSupport.StringSchema();
        var properties = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "name", stringSchema }
        };
        Assert.Throws<ArgumentException>(() =>
        {
            var schema = McpJsonSchema.Object(properties, ["missing"]);
            return schema;
        });
        Assert.Throws<ArgumentException>(() =>
        {
            var schema = McpJsonSchema.Object(properties, ["name", "name"]);
            return schema;
        });
    }

    [Fact]
    public void ObjectSchemaCapturesItsPropertiesAndRequiredNames()
    {
        var stringSchema = McpToolSupport.StringSchema();
        var properties = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "name", stringSchema }
        };
        string[] required = ["name"];
        var schema = McpJsonSchema.Object(properties, required);
        properties.Clear();
        required[0] = "changed";
        var json = JsonSerializer.SerializeToElement(schema);
        var nameType = json.GetProperty("properties").GetProperty("name").GetProperty("type").GetString();
        var requiredName = json.GetProperty("required")[0].GetString();
        Assert.Equal("string", nameType);
        Assert.Equal("name", requiredName);
    }
}
