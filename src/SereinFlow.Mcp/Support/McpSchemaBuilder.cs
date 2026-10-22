using System.Text.Json;

namespace SereinFlow.Mcp.Support;

internal static class McpSchemaBuilder
{
    internal static McpToolDescriptor Tool(string name, string description, JsonElement inputSchema)
    {
        return new McpToolDescriptor(name, description, inputSchema);
    }

    internal static JsonElement Schema(Dictionary<string, McpJsonSchema>? properties = null, string[]? required = null)
    {
        var schema = McpJsonSchema.Object(properties, required);
        return JsonSerializer.SerializeToElement(schema);
    }

    internal static McpJsonSchema StringSchema(string? description = null)
    {
        return new McpJsonSchema
        {
            Type = "string",
            Description = description
        };
    }

    internal static McpJsonSchema StringEnumSchema(string? description, params string[] values)
    {
        var enumValues = JsonSerializer.SerializeToElement(values);
        return new McpJsonSchema
        {
            Type = "string",
            Enum = enumValues,
            Description = description
        };
    }

    internal static McpJsonSchema NumberSchema()
    {
        return new McpJsonSchema
        {
            Type = "integer"
        };
    }

    internal static McpJsonSchema BooleanSchema()
    {
        return new McpJsonSchema
        {
            Type = "boolean"
        };
    }
}
