using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Mcp;

public sealed partial class SereinFlowMcpBackend
{
    private static Dictionary<string, object?> ApplySchemaProperties()
        => new(StringComparer.Ordinal)
        {
            ["previewId"] = StringSchema(),
            ["previewFingerprint"] = StringSchema(),
            ["confirmation"] = new { type = "string", @enum = new[] { "APPLY" } },
            ["idempotencyKey"] = StringSchema("A fresh key for this exact apply request.")
        };

    private static object ArraySchema(object? items = null, int? minItems = null)
    {
        var schema = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "array",
            ["items"] = items ?? new Dictionary<string, object?>(),
        };
        if (minItems is not null)
            schema["minItems"] = minItems.Value;
        return schema;
    }

    private static object FlowPatchOperationsSchema()
        => ArraySchema(
            new
            {
                oneOf = new object[]
                {
                    CanonicalPatchOperationSchema("addCanvas", "canvas", CanvasValueSchema(), ["canvas"]),
                    CanonicalPatchOperationSchema("updateCanvas", "canvas", CanvasValueSchema(), ["canvasId", "canvas"], ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema("removeCanvas", null, null, ["canvasId"], ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema("addNode", "node", NodeValueSchema(), ["canvasId", "node"], ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema("replaceNode", "node", NodeValueSchema(), ["canvasId", "nodeId", "node"], ("canvasId", StringSchema()), ("nodeId", StringSchema())),
                    CanonicalPatchOperationSchema("removeNode", null, null, ["canvasId", "nodeId"], ("canvasId", StringSchema()), ("nodeId", StringSchema())),
                    CanonicalPatchOperationSchema("setNodeParameter", "parameter", NodeParameterValueSchema(), ["canvasId", "nodeId", "parameterId", "parameter"], ("canvasId", StringSchema()), ("nodeId", StringSchema()), ("parameterId", StringSchema())),
                    CanonicalPatchOperationSchema("addConnection", "connection", ConnectionValueSchema(), ["canvasId", "connection"], ("canvasId", StringSchema())),
                    CanonicalPatchOperationSchema("replaceConnection", "connection", ConnectionValueSchema(), ["canvasId", "connectionId", "connection"], ("canvasId", StringSchema()), ("connectionId", StringSchema())),
                    CanonicalPatchOperationSchema("removeConnection", null, null, ["canvasId", "connectionId"], ("canvasId", StringSchema()), ("connectionId", StringSchema())),
                    CanonicalPatchOperationSchema("setEntryNode", "entryNodeId", StringSchema(), ["entryNodeId"]),
                    CanonicalPatchOperationSchema("setRunPolicy", "runPolicy", RunPolicyValueSchema(), ["runPolicy"]),
                    CanonicalPatchOperationSchema("replaceScriptSource", "source", StringSchema(), ["canvasId", "nodeId", "source"], ("canvasId", StringSchema()), ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema("addCanvas", CanvasValueSchema(), ["value"]),
                    LegacyPatchOperationSchema("updateCanvas", CanvasValueSchema(), ["canvasId", "value"], ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema("removeCanvas", null, ["canvasId"], ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema("addNode", NodeValueSchema(), ["canvasId", "value"], ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema("replaceNode", NodeValueSchema(), ["canvasId", "nodeId", "value"], ("canvasId", StringSchema()), ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema("removeNode", null, ["canvasId", "nodeId"], ("canvasId", StringSchema()), ("nodeId", StringSchema())),
                    LegacyPatchOperationSchema("setNodeParameter", NodeParameterValueSchema(), ["canvasId", "nodeId", "parameterId", "value"], ("canvasId", StringSchema()), ("nodeId", StringSchema()), ("parameterId", StringSchema())),
                    LegacyPatchOperationSchema("addConnection", ConnectionValueSchema(), ["canvasId", "value"], ("canvasId", StringSchema())),
                    LegacyPatchOperationSchema("replaceConnection", ConnectionValueSchema(), ["canvasId", "connectionId", "value"], ("canvasId", StringSchema()), ("connectionId", StringSchema())),
                    LegacyPatchOperationSchema("removeConnection", null, ["canvasId", "connectionId"], ("canvasId", StringSchema()), ("connectionId", StringSchema())),
                    LegacyPatchOperationSchema("setEntryNode", StringSchema(), ["value"]),
                    LegacyPatchOperationSchema("setRunPolicy", RunPolicyValueSchema(), ["value"]),
                    LegacyPatchOperationSchema("replaceScriptSource", StringSchema(), ["canvasId", "nodeId", "value"], ("canvasId", StringSchema()), ("nodeId", StringSchema()))
                }
            },
            minItems: 1);

    private static object CanonicalPatchOperationSchema(
        string operation,
        string? payloadName,
        object? payloadSchema,
        string[] required,
        params (string Name, object Schema)[] fields)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["op"] = new { type = "string", @enum = new[] { operation } },
        };
        foreach (var (name, schema) in fields)
            properties[name] = schema;
        if (payloadName is not null && payloadSchema is not null)
            properties[payloadName] = payloadSchema;
        return ObjectSchema(properties, ["op", .. required]);
    }

    private static object LegacyPatchOperationSchema(
        string operation,
        object? valueSchema,
        string[] required,
        params (string Name, object Schema)[] fields)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operation"] = new { type = "string", @enum = new[] { operation } },
        };
        foreach (var (name, schema) in fields)
            properties[name] = schema;
        if (valueSchema is not null)
            properties["value"] = valueSchema;
        return ObjectSchema(properties, required);
    }

    private static object CanvasValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = StringSchema("Stable canvas ID."),
                ["lifecycle"] = EnumSchema<CanvasLifecycleDto>(),
                ["nodes"] = ArraySchema(NodeValueSchema()),
                ["connections"] = ArraySchema(ConnectionValueSchema()),
                ["name"] = NullableSchema(StringSchema()),
            },
            ["id", "lifecycle", "nodes", "connections"]);

    private static object NodeValueSchema()
        => new
        {
            oneOf = new object[]
            {
                TypedNodeValueSchema("action", NullSchema()),
                TypedNodeValueSchema("flipflop", NullSchema()),
                TypedNodeValueSchema("script", ScriptValueSchema()),
                TypedNodeValueSchema("flowCall", NullSchema())
            }
        };

    private static object TypedNodeValueSchema(string type, object scriptSchema)
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = StringSchema("Stable node ID."),
                ["type"] = new { type = "string", @enum = new[] { type } },
                ["displayName"] = StringSchema(),
                ["x"] = RealSchema(),
                ["y"] = RealSchema(),
                ["ports"] = ArraySchema(PortValueSchema()),
                ["parameters"] = ArraySchema(NodeParameterValueSchema()),
                ["script"] = scriptSchema,
                ["ui"] = NullableSchema(NodeUiValueSchema()),
            },
            ["id", "type", "displayName", "x", "y", "ports", "parameters", "script"]);

    private static object PortValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = StringSchema(),
                ["name"] = StringSchema(),
                ["direction"] = StringSchema(),
                ["required"] = BooleanSchema(),
            },
            ["id", "name", "direction", "required"]);

    private static object NodeParameterValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = StringSchema(),
                ["valueJson"] = NullableSchema(StringSchema()),
                ["source"] = EnumSchema<DataSourceDto>(),
                ["required"] = BooleanSchema(),
                ["ui"] = ParameterUiValueSchema(),
            },
            ["name", "valueJson", "source", "required", "ui"]);

    private static object ConnectionValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = StringSchema("Stable connection ID."),
                ["fromNodeId"] = StringSchema(),
                ["fromPortId"] = StringSchema(),
                ["toNodeId"] = StringSchema(),
                ["toPortId"] = StringSchema(),
                ["kind"] = EnumSchema<ConnectionKindDto>(),
                ["branch"] = NullableSchema(EnumSchema<ExecutionBranchDto>()),
                ["dataSource"] = NullableSchema(EnumSchema<DataSourceDto>()),
                ["priority"] = NumberSchema(),
            },
            ["id", "fromNodeId", "fromPortId", "toNodeId", "toPortId", "kind", "branch", "dataSource", "priority"]);

    private static object ScriptValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["nodeId"] = StringSchema(),
                ["source"] = StringSchema(),
                ["languageVersion"] = StringSchema(),
                ["sourceHash"] = StringSchema(),
                ["inputs"] = ArraySchema(ScriptContractValueSchema()),
                ["outputs"] = ArraySchema(ScriptContractValueSchema()),
            },
            ["nodeId", "source", "languageVersion", "sourceHash", "inputs", "outputs"]);

    private static object RunPolicyValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["concurrencyMode"] = EnumSchema<FlowConcurrencyModeDto>(),
            },
            ["concurrencyMode"]);

    private static object ParameterUiValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = StringSchema(),
                ["nameKey"] = StringSchema(),
                ["valueKind"] = StringSchema(),
                ["literalValue"] = NullableSchema(StringSchema()),
                ["projectInputKey"] = NullableSchema(StringSchema()),
                ["expression"] = NullableSchema(StringSchema()),
                ["sourceNodeId"] = NullableSchema(StringSchema()),
                ["sourcePortId"] = NullableSchema(StringSchema()),
                ["type"] = NullableSchema(StringSchema()),
                ["description"] = NullableSchema(StringSchema()),
                ["inputMode"] = NullableSchema(StringSchema()),
                ["isVariadic"] = NullableSchema(BooleanSchema()),
                ["variadicGroupId"] = NullableSchema(StringSchema()),
                ["elementType"] = NullableSchema(StringSchema()),
                ["variadicMode"] = NullableSchema(StringSchema()),
                ["enumMetadata"] = NullableSchema(EnumMetadataValueSchema())
            },
            ["id", "nameKey", "valueKind"]);

    private static object NodeUiValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = StringSchema(),
                ["titleKey"] = StringSchema(),
                ["subtitleKey"] = StringSchema(),
                ["description"] = NullableSchema(StringSchema()),
                ["status"] = StringSchema(),
                ["hasDataOutput"] = BooleanSchema(),
                ["width"] = NullableSchema(RealSchema()),
                ["category"] = NullableSchema(StringSchema()),
                ["libraryId"] = NullableSchema(StringSchema()),
                ["className"] = NullableSchema(StringSchema()),
                ["methodName"] = NullableSchema(StringSchema()),
                ["dllName"] = NullableSchema(StringSchema()),
                ["dllVersion"] = NullableSchema(StringSchema()),
                ["returnType"] = NullableSchema(StringSchema()),
                ["targetNodeId"] = NullableSchema(StringSchema()),
                ["targetFlowId"] = NullableSchema(StringSchema()),
                ["isAwaitable"] = NullableSchema(BooleanSchema()),
                ["staticReturnType"] = NullableSchema(StringSchema()),
                ["isDynamicReturnType"] = NullableSchema(BooleanSchema()),
                ["targetCanvasId"] = NullableSchema(StringSchema()),
                ["isPublic"] = NullableSchema(BooleanSchema()),
                ["flowCallParameterBindings"] = NullableSchema(ArraySchema(FlowCallParameterBindingValueSchema())),
                ["libraryNodeContractId"] = NullableSchema(StringSchema()),
                ["flowLibraryName"] = NullableSchema(StringSchema())
            },
            ["kind", "titleKey", "subtitleKey", "description", "status", "hasDataOutput", "width"]);

    private static object FlowCallParameterBindingValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["callParameterId"] = StringSchema(),
                ["targetParameterId"] = StringSchema()
            },
            ["callParameterId", "targetParameterId"]);

    private static object EnumMetadataValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["typeName"] = StringSchema(),
                ["isFlags"] = BooleanSchema(),
                ["underlyingType"] = StringSchema(),
                ["options"] = ArraySchema(EnumOptionValueSchema())
            },
            ["typeName", "isFlags", "underlyingType", "options"]);

    private static object EnumOptionValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = StringSchema(),
                ["numericValue"] = StringSchema()
            },
            ["name", "numericValue"]);

    private static object PositionSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["x"] = RealSchema(),
                ["y"] = RealSchema()
            },
            ["x", "y"]);

    private static object NullSchema() => new { type = "null" };

    private static object ScriptContractValueSchema()
        => ObjectSchema(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = StringSchema(),
                ["valueKind"] = StringSchema(),
                ["required"] = BooleanSchema(),
                ["id"] = NullableSchema(StringSchema()),
                ["description"] = NullableSchema(StringSchema()),
            },
            ["name", "valueKind", "required"]);

    private static object ObjectSchema(
        Dictionary<string, object?>? properties = null,
        string[]? required = null,
        bool additionalProperties = false)
        => new
        {
            type = "object",
            properties = properties ?? new Dictionary<string, object?>(),
            required = required ?? [],
            additionalProperties,
        };

    private static object NullableSchema(object schema)
        => new { oneOf = new object[] { schema, new { type = "null" } } };

    private static object RealSchema()
        => new { type = "number" };

    private static object EnumSchema<TEnum>() where TEnum : struct, Enum
        => new
        {
            oneOf = new object[]
            {
                new
                {
                    type = "string",
                    @enum = Enum.GetNames<TEnum>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray(),
                },
                new
                {
                    type = "integer",
                    @enum = Enum.GetValues<TEnum>().Select(value => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
                }
            }
        };

}
