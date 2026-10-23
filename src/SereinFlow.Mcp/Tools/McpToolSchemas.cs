using SereinFlow.Contracts;
using System.Text.Json;
using static SereinFlow.Mcp.Support.McpSchemaBuilder;

namespace SereinFlow.Mcp.Tools;

internal static class McpToolSchemas
{
    internal static Dictionary<string, McpJsonSchema> ApplySchemaProperties()
    {
        return new Dictionary<string, McpJsonSchema>
        {
            { "previewId", StringSchema() },
            { "previewFingerprint", StringSchema() },
            { "confirmation", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { "APPLY" }) } },
            { "idempotencyKey", StringSchema("A fresh key for this exact apply request.") }
        };
    }

    internal static McpJsonSchema ArraySchema(McpJsonSchema? items = null, int? minItems = null)
    {
        return new McpJsonSchema
        {
            Type = "array",
            Items = items ?? new McpJsonSchema(),
            MinItems = minItems
        };
    }

    internal static McpJsonSchema FlowPatchOperationsSchema()
    {
        return ArraySchema(
            new McpJsonSchema
            {
                OneOf = new McpJsonSchema[]
                {
                    CanonicalPatchOperationSchema("addCanvas", "canvas", CanvasValueSchema(), ["canvas"]),
                    CanonicalPatchOperationSchema(
                        "updateCanvas",
                        "canvas",
                        CanvasValueSchema(),
                        ["canvasId", "canvas"],
                        ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema("removeCanvas", null, null, ["canvasId"], (("canvasId", StringSchema()))),
                    CanonicalPatchOperationSchema(
                        "addNode",
                        "node",
                        NodeValueSchema(),
                        ["canvasId", "node"],
                        ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "replaceNode",
                        "node",
                        NodeValueSchema(),
                        ["canvasId", "nodeId", "node"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "removeNode",
                        null,
                        null,
                        ["canvasId", "nodeId"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "setNodeParameter",
                        "parameter",
                        NodeParameterValueSchema(),
                        ["canvasId", "nodeId", "parameterId", "parameter"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema()),
                        ("parameterId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "addNodeParameter",
                        "parameter",
                        NodeParameterValueSchema(),
                        ["canvasId", "nodeId", "parameter"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "removeNodeParameter",
                        null,
                        null,
                        ["canvasId", "nodeId", "parameterId"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema()),
                        ("parameterId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "addConnection",
                        "connection",
                        ConnectionValueSchema(),
                        ["canvasId", "connection"],
                        ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "replaceConnection",
                        "connection",
                        ConnectionValueSchema(),
                        ["canvasId", "connectionId", "connection"],
                        ("canvasId", StringSchema()),
                        ("connectionId", StringSchema())),
                    CanonicalPatchOperationSchema(
                        "removeConnection",
                        null,
                        null,
                        ["canvasId", "connectionId"],
                        ("canvasId", StringSchema()),
                        ("connectionId", StringSchema())),
                    CanonicalPatchOperationSchema("setEntryNode", "entryNodeId", StringSchema(), ["entryNodeId"]),
                    CanonicalPatchOperationSchema("setRunPolicy", "runPolicy", RunPolicyValueSchema(), ["runPolicy"]),
                    CanonicalPatchOperationSchema(
                        "replaceScriptSource",
                        "source",
                        StringSchema(),
                        ["canvasId", "nodeId", "source"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema("addCanvas", CanvasValueSchema(), ["value"]),
                    LegacyPatchOperationSchema(
                        "updateCanvas",
                        CanvasValueSchema(),
                        ["canvasId", "value"],
                        ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema("removeCanvas", null, ["canvasId"], (("canvasId", StringSchema()))),
                    LegacyPatchOperationSchema(
                        "addNode",
                        NodeValueSchema(),
                        ["canvasId", "value"],
                        ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "replaceNode",
                        NodeValueSchema(),
                        ["canvasId", "nodeId", "value"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "removeNode",
                        null,
                        ["canvasId", "nodeId"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "setNodeParameter",
                        NodeParameterValueSchema(),
                        ["canvasId", "nodeId", "parameterId", "value"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema()),
                        ("parameterId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "addNodeParameter",
                        NodeParameterValueSchema(),
                        ["canvasId", "nodeId", "value"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "removeNodeParameter",
                        null,
                        ["canvasId", "nodeId", "parameterId"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema()),
                        ("parameterId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "addConnection",
                        ConnectionValueSchema(),
                        ["canvasId", "value"],
                        ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "replaceConnection",
                        ConnectionValueSchema(),
                        ["canvasId", "connectionId", "value"],
                        ("canvasId", StringSchema()),
                        ("connectionId", StringSchema())),
                    LegacyPatchOperationSchema(
                        "removeConnection",
                        null,
                        ["canvasId", "connectionId"],
                        ("canvasId", StringSchema()),
                        ("connectionId", StringSchema())),
                    LegacyPatchOperationSchema("setEntryNode", StringSchema(), ["value"]),
                    LegacyPatchOperationSchema("setRunPolicy", RunPolicyValueSchema(), ["value"]),
                    LegacyPatchOperationSchema(
                        "replaceScriptSource",
                        StringSchema(),
                        ["canvasId", "nodeId", "value"],
                        ("canvasId", StringSchema()),
                        ("nodeId", StringSchema()))
                }
            },
            minItems: 1);
    }

    private static McpJsonSchema CanonicalPatchOperationSchema(
        string operation,
        string? payloadName,
        McpJsonSchema? payloadSchema,
        string[] required,
        params (string Name, McpJsonSchema Schema)[] fields)
    {
        var properties = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            {
                "op",
                new McpJsonSchema
                {
                    Type = "string",
                    Enum = JsonSerializer.SerializeToElement(new[] { operation })
                }
            }
        };
        foreach (var field in fields)
        {
            properties.Add(field.Name, field.Schema);
        }

        if (payloadName is not null && payloadSchema is not null)
        {
            properties.Add(payloadName, payloadSchema);
        }

        return ObjectSchema(properties, ["op", .. required]);
    }

    private static McpJsonSchema LegacyPatchOperationSchema(
        string operation,
        McpJsonSchema? valueSchema,
        string[] required,
        params (string Name, McpJsonSchema Schema)[] fields)
    {
        var properties = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            {
                "operation",
                new McpJsonSchema
                {
                    Type = "string",
                    Enum = JsonSerializer.SerializeToElement(new[] { operation })
                }
            }
        };
        foreach (var field in fields)
        {
            properties.Add(field.Name, field.Schema);
        }

        if (valueSchema is not null)
        {
            properties.Add("value", valueSchema);
        }

        return ObjectSchema(properties, required);
    }

    private static McpJsonSchema CanvasValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "id", StringSchema("Stable canvas ID.") },
                { "lifecycle", EnumSchema<CanvasLifecycleDto>() },
                { "nodes", ArraySchema(NodeValueSchema()) },
                { "connections", ArraySchema(ConnectionValueSchema()) },
                { "name", NullableSchema(StringSchema()) }
            },
            ["id", "lifecycle", "nodes", "connections"]);
    }

    private static McpJsonSchema NodeValueSchema()
    {
        return new McpJsonSchema
        {
            OneOf = new McpJsonSchema[]
            {
                TypedNodeValueSchema("action", NullSchema()),
                TypedNodeValueSchema("flipflop", NullSchema()),
                TypedNodeValueSchema("script", ScriptValueSchema()),
                TypedNodeValueSchema("flowCall", NullSchema())
            }
        };
    }

    private static McpJsonSchema TypedNodeValueSchema(string type, McpJsonSchema scriptSchema)
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "id", StringSchema("Stable node ID.") },
                { "type", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { type }) } },
                { "displayName", StringSchema() },
                { "x", RealSchema() },
                { "y", RealSchema() },
                { "ports", ArraySchema(PortValueSchema()) },
                { "parameters", ArraySchema(NodeParameterValueSchema()) },
                { "script", scriptSchema },
                { "ui", NullableSchema(NodeUiValueSchema()) }
            },
            ["id", "type", "displayName", "x", "y", "ports", "parameters", "script"]);
    }

    private static McpJsonSchema PortValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "id", StringSchema() },
                { "name", StringSchema() },
                { "direction", StringSchema() },
                { "required", BooleanSchema() }
            },
            ["id", "name", "direction", "required"]);
    }

    private static McpJsonSchema NodeParameterValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "name", StringSchema() },
                { "valueJson", NullableSchema(StringSchema()) },
                { "source", EnumSchema<DataSourceDto>() },
                { "required", BooleanSchema() },
                { "ui", ParameterUiValueSchema() }
            },
            ["name", "valueJson", "source", "required", "ui"]);
    }

    private static McpJsonSchema ConnectionValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "id", StringSchema("Stable connection ID.") },
                { "fromNodeId", StringSchema() },
                { "fromPortId", StringSchema() },
                { "toNodeId", StringSchema() },
                { "toPortId", StringSchema() },
                { "kind", EnumSchema<ConnectionKindDto>() },
                { "branch", NullableSchema(EnumSchema<ExecutionBranchDto>()) },
                { "dataSource", NullableSchema(EnumSchema<DataSourceDto>()) },
                { "priority", NumberSchema() }
            },
            ["id", "fromNodeId", "fromPortId", "toNodeId", "toPortId", "kind", "branch", "dataSource", "priority"]);
    }

    private static McpJsonSchema ScriptValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "nodeId", StringSchema() },
                { "source", StringSchema() },
                { "languageVersion", StringSchema() },
                { "sourceHash", NullableSchema(StringSchema("Optional metadata; the server derives the hash from source.")) },
                { "inputs", ArraySchema(ScriptContractValueSchema()) },
                { "outputs", ArraySchema(ScriptContractValueSchema()) }
            },
            ["nodeId", "source", "languageVersion", "inputs", "outputs"]);
    }

    private static McpJsonSchema RunPolicyValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema> { { "concurrencyMode", EnumSchema<FlowConcurrencyModeDto>() } },
            ["concurrencyMode"]);
    }

    private static McpJsonSchema ParameterUiValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "id", StringSchema() },
                { "nameKey", StringSchema() },
                { "valueKind", StringSchema() },
                { "literalValue", NullableSchema(StringSchema()) },
                { "projectInputKey", NullableSchema(StringSchema()) },
                { "expression", NullableSchema(StringSchema()) },
                { "sourceNodeId", NullableSchema(StringSchema()) },
                { "sourcePortId", NullableSchema(StringSchema()) },
                { "type", NullableSchema(StringSchema()) },
                { "description", NullableSchema(StringSchema()) },
                { "inputMode", NullableSchema(StringSchema()) },
                { "isVariadic", NullableSchema(BooleanSchema()) },
                { "variadicGroupId", NullableSchema(StringSchema()) },
                { "elementType", NullableSchema(StringSchema()) },
                { "variadicMode", NullableSchema(StringSchema()) },
                { "enumMetadata", NullableSchema(EnumMetadataValueSchema()) }
            },
            ["id", "nameKey", "valueKind"]);
    }

    private static McpJsonSchema NodeUiValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "kind", StringSchema() },
                { "titleKey", StringSchema() },
                { "subtitleKey", StringSchema() },
                { "description", NullableSchema(StringSchema()) },
                { "status", StringSchema() },
                { "hasDataOutput", BooleanSchema() },
                { "width", NullableSchema(RealSchema()) },
                { "category", NullableSchema(StringSchema()) },
                { "libraryId", NullableSchema(StringSchema()) },
                { "className", NullableSchema(StringSchema()) },
                { "methodName", NullableSchema(StringSchema()) },
                { "dllName", NullableSchema(StringSchema()) },
                { "dllVersion", NullableSchema(StringSchema()) },
                { "returnType", NullableSchema(StringSchema()) },
                { "targetNodeId", NullableSchema(StringSchema()) },
                { "targetFlowId", NullableSchema(StringSchema()) },
                { "isAwaitable", NullableSchema(BooleanSchema()) },
                { "staticReturnType", NullableSchema(StringSchema()) },
                { "isDynamicReturnType", NullableSchema(BooleanSchema()) },
                { "targetCanvasId", NullableSchema(StringSchema()) },
                { "isPublic", NullableSchema(BooleanSchema()) },
                { "flowCallParameterBindings", NullableSchema(ArraySchema(FlowCallParameterBindingValueSchema())) },
                { "libraryNodeContractId", NullableSchema(StringSchema()) },
                { "flowLibraryName", NullableSchema(StringSchema()) }
            },
            ["kind", "titleKey", "subtitleKey", "description", "status", "hasDataOutput", "width"]);
    }

    private static McpJsonSchema FlowCallParameterBindingValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "callParameterId", StringSchema() },
                { "targetParameterId", StringSchema() }
            },
            ["callParameterId", "targetParameterId"]);
    }

    private static McpJsonSchema EnumMetadataValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "typeName", StringSchema() },
                { "isFlags", BooleanSchema() },
                { "underlyingType", StringSchema() },
                { "options", ArraySchema(EnumOptionValueSchema()) }
            },
            ["typeName", "isFlags", "underlyingType", "options"]);
    }

    private static McpJsonSchema EnumOptionValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema> { { "name", StringSchema() }, { "numericValue", StringSchema() } },
            ["name", "numericValue"]);
    }

    internal static McpJsonSchema PositionSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal) { { "x", RealSchema() }, { "y", RealSchema() } },
            ["x", "y"]);
    }

    private static McpJsonSchema NullSchema()
    {
        return new McpJsonSchema
        {
            Type = "null"
        };
    }

    private static McpJsonSchema ScriptContractValueSchema()
    {
        return ObjectSchema(
            new Dictionary<string, McpJsonSchema>
            {
                { "name", StringSchema() },
                { "valueKind", StringSchema() },
                { "required", BooleanSchema() },
                { "id", NullableSchema(StringSchema()) },
                { "description", NullableSchema(StringSchema()) }
            },
            ["name", "valueKind", "required"]);
    }

    private static McpJsonSchema ObjectSchema(
        Dictionary<string, McpJsonSchema>? properties = null,
        string[]? required = null,
        bool additionalProperties = false)
    {
        return McpJsonSchema.CreateObject(properties, required, additionalProperties);
    }

    private static McpJsonSchema NullableSchema(McpJsonSchema schema)
    {
        return new McpJsonSchema
        {
            OneOf = [schema, new McpJsonSchema
    {
        Type = "null"
    }

    ]
        };
    }

    private static McpJsonSchema RealSchema()
    {
        return new McpJsonSchema
        {
            Type = "number"
        };
    }

    private static McpJsonSchema EnumSchema<TEnum>()
        where TEnum : struct, Enum
    {
        var names = Enum
            .GetNames<TEnum>()
            .Select(JsonNamingPolicy.CamelCase.ConvertName)
            .ToArray();
        var numbers = Enum
            .GetValues<TEnum>()
            .Select(value => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        var stringValues = JsonSerializer.SerializeToElement(names);
        var numericValues = JsonSerializer.SerializeToElement(numbers);
        return new McpJsonSchema
        {
            OneOf = [new McpJsonSchema { Type = "string", Enum = stringValues }, new McpJsonSchema { Type = "integer", Enum = numericValues }]
        };
    }
}
