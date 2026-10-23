using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Explicit schema vocabulary for hand-authored MCP contracts. An empty instance
/// represents {}, while null keywords are absent and false/empty values are retained.
/// MCP 手工合同使用的 Schema 模型；保留空 Schema、false 和空集合的含义。
/// </summary>
public sealed record McpJsonSchema
{
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, McpJsonSchema>? Properties { get; init; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Required { get; init; }

    [JsonPropertyName("additionalProperties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AdditionalProperties { get; init; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpJsonSchema? Items { get; init; }

    [JsonPropertyName("minItems")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinItems { get; init; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Enum { get; init; }

    [JsonPropertyName("oneOf")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<McpJsonSchema>? OneOf { get; init; }

    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Compatibility factory; new callers use CreateObject.")]
    public static McpJsonSchema Object(
        IReadOnlyDictionary<string, McpJsonSchema>? properties = null,
        string[]? required = null,
        bool additionalProperties = false)
        => CreateObject(properties, required, additionalProperties);

    public static McpJsonSchema CreateObject(
        IReadOnlyDictionary<string, McpJsonSchema>? properties = null,
        string[]? required = null,
        bool additionalProperties = false)
    {
        var propertySnapshot = properties is null
            ? new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
            : new Dictionary<string, McpJsonSchema>(properties, StringComparer.Ordinal);
        var requiredSnapshot = required?.ToArray() ?? [];
        var requiredNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in requiredSnapshot)
        {
            if (!propertySnapshot.ContainsKey(name) || !requiredNames.Add(name))
            {
                throw new ArgumentException("Required properties must be declared exactly once. 必填属性必须且只能声明一次。", nameof(required));
            }
        }
        return new McpJsonSchema
        {
            Type = "object",
            Properties = propertySnapshot,
            Required = requiredSnapshot,
            AdditionalProperties = additionalProperties
        };
    }
}
