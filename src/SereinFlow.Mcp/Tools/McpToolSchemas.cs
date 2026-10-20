using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

internal static class McpToolSchemas
{
    internal static Dictionary<string, McpJsonSchema> ApplySchemaProperties()
    {
        Dictionary<string, McpJsonSchema> evaluatedValue = new(StringComparer.Ordinal)
        {
            { "previewId", StringSchema() },
            { "previewFingerprint", StringSchema() },
            { "confirmation", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { "APPLY" }) } },
            { "idempotencyKey", StringSchema("A fresh key for this exact apply request.") }
        };
        return evaluatedValue;
    }

    internal static McpJsonSchema ArraySchema(McpJsonSchema? items = null, int? minItems = null)
    {
        var schema = new McpJsonSchema
        {
            Type = "array",
            Items = items ?? new McpJsonSchema(),
            MinItems = minItems
        };
        return schema;
    }

    internal static McpJsonSchema FlowPatchOperationsSchema()
    {
        var evaluatedValue117 = new McpJsonSchema[]
        {
            resolveCanonicalPatchOperationSchemaResult(),
            resolveCanonicalPatchOperationSchemaResult1(),
            resolveCanonicalPatchOperationSchemaResult5(),
            resolveCanonicalPatchOperationSchemaResult8(),
            resolveCanonicalPatchOperationSchemaResult11(),
            resolveCanonicalPatchOperationSchemaResult16(),
            resolveCanonicalPatchOperationSchemaResult20(),
            resolveCanonicalPatchOperationSchemaResult25(),
            resolveCanonicalPatchOperationSchemaResult30(),
            resolveCanonicalPatchOperationSchemaResult35(),
            resolveCanonicalPatchOperationSchemaResult38(),
            resolveCanonicalPatchOperationSchemaResult43(),
            resolveCanonicalPatchOperationSchemaResult47(),
            resolveCanonicalPatchOperationSchemaResult49(),
            resolveCanonicalPatchOperationSchemaResult51(),
            resolveLegacyPatchOperationSchemaResult(),
            resolveLegacyPatchOperationSchemaResult57(),
            resolveLegacyPatchOperationSchemaResult61(),
            resolveLegacyPatchOperationSchemaResult64(),
            resolveLegacyPatchOperationSchemaResult68(),
            resolveLegacyPatchOperationSchemaResult73(),
            resolveLegacyPatchOperationSchemaResult77(),
            resolveLegacyPatchOperationSchemaResult83(),
            resolveLegacyPatchOperationSchemaResult88(),
            resolveLegacyPatchOperationSchemaResult93(),
            resolveLegacyPatchOperationSchemaResult97(),
            resolveLegacyPatchOperationSchemaResult102(),
            resolveLegacyPatchOperationSchemaResult106(),
            resolveLegacyPatchOperationSchemaResult109(),
            resolveLegacyPatchOperationSchemaResult112()
        };
        var evaluatedValue118 = new McpJsonSchema { OneOf = evaluatedValue117 };
        var arraySchemaResult = ArraySchema(evaluatedValue118, minItems: 1);
        return arraySchemaResult;
        McpJsonSchema resolveCanonicalPatchOperationSchemaResult()
        {
            var canvasValueSchemaResult = CanvasValueSchema();
            var canonicalPatchOperationSchemaResult = CanonicalPatchOperationSchema("addCanvas", "canvas", canvasValueSchemaResult, ["canvas"]);
            return canonicalPatchOperationSchemaResult;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult1()
        {
            var canvasValueSchemaResult2 = CanvasValueSchema();
            var evaluatedValue3 = ("canvasId", StringSchema());
            var canonicalPatchOperationSchemaResult4 = CanonicalPatchOperationSchema("updateCanvas", "canvas", canvasValueSchemaResult2, ["canvasId", "canvas"], evaluatedValue3);
            return canonicalPatchOperationSchemaResult4;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult5()
        {
            var evaluatedValue6 = ("canvasId", StringSchema());
            var canonicalPatchOperationSchemaResult7 = CanonicalPatchOperationSchema("removeCanvas", null, null, ["canvasId"], evaluatedValue6);
            return canonicalPatchOperationSchemaResult7;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult8()
        {
            var nodeValueSchemaResult = NodeValueSchema();
            var evaluatedValue9 = ("canvasId", StringSchema());
            var canonicalPatchOperationSchemaResult10 = CanonicalPatchOperationSchema("addNode", "node", nodeValueSchemaResult, ["canvasId", "node"], evaluatedValue9);
            return canonicalPatchOperationSchemaResult10;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult11()
        {
            var nodeValueSchemaResult12 = NodeValueSchema();
            var evaluatedValue13 = ("canvasId", StringSchema());
            var evaluatedValue14 = ("nodeId", StringSchema());
            var canonicalPatchOperationSchemaResult15 = CanonicalPatchOperationSchema("replaceNode", "node", nodeValueSchemaResult12, ["canvasId", "nodeId", "node"], evaluatedValue13, evaluatedValue14);
            return canonicalPatchOperationSchemaResult15;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult16()
        {
            var evaluatedValue17 = ("canvasId", StringSchema());
            var evaluatedValue18 = ("nodeId", StringSchema());
            var canonicalPatchOperationSchemaResult19 = CanonicalPatchOperationSchema("removeNode", null, null, ["canvasId", "nodeId"], evaluatedValue17, evaluatedValue18);
            return canonicalPatchOperationSchemaResult19;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult20()
        {
            var nodeParameterValueSchemaResult = NodeParameterValueSchema();
            var evaluatedValue21 = ("canvasId", StringSchema());
            var evaluatedValue22 = ("nodeId", StringSchema());
            var evaluatedValue23 = ("parameterId", StringSchema());
            var canonicalPatchOperationSchemaResult24 = CanonicalPatchOperationSchema("setNodeParameter", "parameter", nodeParameterValueSchemaResult, ["canvasId", "nodeId", "parameterId", "parameter"], evaluatedValue21, evaluatedValue22, evaluatedValue23);
            return canonicalPatchOperationSchemaResult24;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult25()
        {
            var nodeParameterValueSchemaResult26 = NodeParameterValueSchema();
            var evaluatedValue27 = ("canvasId", StringSchema());
            var evaluatedValue28 = ("nodeId", StringSchema());
            var canonicalPatchOperationSchemaResult29 = CanonicalPatchOperationSchema("addNodeParameter", "parameter", nodeParameterValueSchemaResult26, ["canvasId", "nodeId", "parameter"], evaluatedValue27, evaluatedValue28);
            return canonicalPatchOperationSchemaResult29;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult30()
        {
            var evaluatedValue31 = ("canvasId", StringSchema());
            var evaluatedValue32 = ("nodeId", StringSchema());
            var evaluatedValue33 = ("parameterId", StringSchema());
            var canonicalPatchOperationSchemaResult34 = CanonicalPatchOperationSchema("removeNodeParameter", null, null, ["canvasId", "nodeId", "parameterId"], evaluatedValue31, evaluatedValue32, evaluatedValue33);
            return canonicalPatchOperationSchemaResult34;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult35()
        {
            var connectionValueSchemaResult = ConnectionValueSchema();
            var evaluatedValue36 = ("canvasId", StringSchema());
            var canonicalPatchOperationSchemaResult37 = CanonicalPatchOperationSchema("addConnection", "connection", connectionValueSchemaResult, ["canvasId", "connection"], evaluatedValue36);
            return canonicalPatchOperationSchemaResult37;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult38()
        {
            var connectionValueSchemaResult39 = ConnectionValueSchema();
            var evaluatedValue40 = ("canvasId", StringSchema());
            var evaluatedValue41 = ("connectionId", StringSchema());
            var canonicalPatchOperationSchemaResult42 = CanonicalPatchOperationSchema("replaceConnection", "connection", connectionValueSchemaResult39, ["canvasId", "connectionId", "connection"], evaluatedValue40, evaluatedValue41);
            return canonicalPatchOperationSchemaResult42;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult43()
        {
            var evaluatedValue44 = ("canvasId", StringSchema());
            var evaluatedValue45 = ("connectionId", StringSchema());
            var canonicalPatchOperationSchemaResult46 = CanonicalPatchOperationSchema("removeConnection", null, null, ["canvasId", "connectionId"], evaluatedValue44, evaluatedValue45);
            return canonicalPatchOperationSchemaResult46;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult47()
        {
            var stringSchemaResult = StringSchema();
            var canonicalPatchOperationSchemaResult48 = CanonicalPatchOperationSchema("setEntryNode", "entryNodeId", stringSchemaResult, ["entryNodeId"]);
            return canonicalPatchOperationSchemaResult48;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult49()
        {
            var runPolicyValueSchemaResult = RunPolicyValueSchema();
            var canonicalPatchOperationSchemaResult50 = CanonicalPatchOperationSchema("setRunPolicy", "runPolicy", runPolicyValueSchemaResult, ["runPolicy"]);
            return canonicalPatchOperationSchemaResult50;
        }

        McpJsonSchema resolveCanonicalPatchOperationSchemaResult51()
        {
            var stringSchemaResult52 = StringSchema();
            var evaluatedValue53 = ("canvasId", StringSchema());
            var evaluatedValue54 = ("nodeId", StringSchema());
            var canonicalPatchOperationSchemaResult55 = CanonicalPatchOperationSchema("replaceScriptSource", "source", stringSchemaResult52, ["canvasId", "nodeId", "source"], evaluatedValue53, evaluatedValue54);
            return canonicalPatchOperationSchemaResult55;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult()
        {
            var canvasValueSchemaResult56 = CanvasValueSchema();
            var legacyPatchOperationSchemaResult = LegacyPatchOperationSchema("addCanvas", canvasValueSchemaResult56, ["value"]);
            return legacyPatchOperationSchemaResult;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult57()
        {
            var canvasValueSchemaResult58 = CanvasValueSchema();
            var evaluatedValue59 = ("canvasId", StringSchema());
            var legacyPatchOperationSchemaResult60 = LegacyPatchOperationSchema("updateCanvas", canvasValueSchemaResult58, ["canvasId", "value"], evaluatedValue59);
            return legacyPatchOperationSchemaResult60;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult61()
        {
            var evaluatedValue62 = ("canvasId", StringSchema());
            var legacyPatchOperationSchemaResult63 = LegacyPatchOperationSchema("removeCanvas", null, ["canvasId"], evaluatedValue62);
            return legacyPatchOperationSchemaResult63;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult64()
        {
            var nodeValueSchemaResult65 = NodeValueSchema();
            var evaluatedValue66 = ("canvasId", StringSchema());
            var legacyPatchOperationSchemaResult67 = LegacyPatchOperationSchema("addNode", nodeValueSchemaResult65, ["canvasId", "value"], evaluatedValue66);
            return legacyPatchOperationSchemaResult67;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult68()
        {
            var nodeValueSchemaResult69 = NodeValueSchema();
            var evaluatedValue70 = ("canvasId", StringSchema());
            var evaluatedValue71 = ("nodeId", StringSchema());
            var legacyPatchOperationSchemaResult72 = LegacyPatchOperationSchema("replaceNode", nodeValueSchemaResult69, ["canvasId", "nodeId", "value"], evaluatedValue70, evaluatedValue71);
            return legacyPatchOperationSchemaResult72;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult73()
        {
            var evaluatedValue74 = ("canvasId", StringSchema());
            var evaluatedValue75 = ("nodeId", StringSchema());
            var legacyPatchOperationSchemaResult76 = LegacyPatchOperationSchema("removeNode", null, ["canvasId", "nodeId"], evaluatedValue74, evaluatedValue75);
            return legacyPatchOperationSchemaResult76;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult77()
        {
            var nodeParameterValueSchemaResult78 = NodeParameterValueSchema();
            var evaluatedValue79 = ("canvasId", StringSchema());
            var evaluatedValue80 = ("nodeId", StringSchema());
            var evaluatedValue81 = ("parameterId", StringSchema());
            var legacyPatchOperationSchemaResult82 = LegacyPatchOperationSchema("setNodeParameter", nodeParameterValueSchemaResult78, ["canvasId", "nodeId", "parameterId", "value"], evaluatedValue79, evaluatedValue80, evaluatedValue81);
            return legacyPatchOperationSchemaResult82;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult83()
        {
            var nodeParameterValueSchemaResult84 = NodeParameterValueSchema();
            var evaluatedValue85 = ("canvasId", StringSchema());
            var evaluatedValue86 = ("nodeId", StringSchema());
            var legacyPatchOperationSchemaResult87 = LegacyPatchOperationSchema("addNodeParameter", nodeParameterValueSchemaResult84, ["canvasId", "nodeId", "value"], evaluatedValue85, evaluatedValue86);
            return legacyPatchOperationSchemaResult87;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult88()
        {
            var evaluatedValue89 = ("canvasId", StringSchema());
            var evaluatedValue90 = ("nodeId", StringSchema());
            var evaluatedValue91 = ("parameterId", StringSchema());
            var legacyPatchOperationSchemaResult92 = LegacyPatchOperationSchema("removeNodeParameter", null, ["canvasId", "nodeId", "parameterId"], evaluatedValue89, evaluatedValue90, evaluatedValue91);
            return legacyPatchOperationSchemaResult92;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult93()
        {
            var connectionValueSchemaResult94 = ConnectionValueSchema();
            var evaluatedValue95 = ("canvasId", StringSchema());
            var legacyPatchOperationSchemaResult96 = LegacyPatchOperationSchema("addConnection", connectionValueSchemaResult94, ["canvasId", "value"], evaluatedValue95);
            return legacyPatchOperationSchemaResult96;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult97()
        {
            var connectionValueSchemaResult98 = ConnectionValueSchema();
            var evaluatedValue99 = ("canvasId", StringSchema());
            var evaluatedValue100 = ("connectionId", StringSchema());
            var legacyPatchOperationSchemaResult101 = LegacyPatchOperationSchema("replaceConnection", connectionValueSchemaResult98, ["canvasId", "connectionId", "value"], evaluatedValue99, evaluatedValue100);
            return legacyPatchOperationSchemaResult101;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult102()
        {
            var evaluatedValue103 = ("canvasId", StringSchema());
            var evaluatedValue104 = ("connectionId", StringSchema());
            var legacyPatchOperationSchemaResult105 = LegacyPatchOperationSchema("removeConnection", null, ["canvasId", "connectionId"], evaluatedValue103, evaluatedValue104);
            return legacyPatchOperationSchemaResult105;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult106()
        {
            var stringSchemaResult107 = StringSchema();
            var legacyPatchOperationSchemaResult108 = LegacyPatchOperationSchema("setEntryNode", stringSchemaResult107, ["value"]);
            return legacyPatchOperationSchemaResult108;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult109()
        {
            var runPolicyValueSchemaResult110 = RunPolicyValueSchema();
            var legacyPatchOperationSchemaResult111 = LegacyPatchOperationSchema("setRunPolicy", runPolicyValueSchemaResult110, ["value"]);
            return legacyPatchOperationSchemaResult111;
        }

        McpJsonSchema resolveLegacyPatchOperationSchemaResult112()
        {
            var stringSchemaResult113 = StringSchema();
            var evaluatedValue114 = ("canvasId", StringSchema());
            var evaluatedValue115 = ("nodeId", StringSchema());
            var legacyPatchOperationSchemaResult116 = LegacyPatchOperationSchema("replaceScriptSource", stringSchemaResult113, ["canvasId", "nodeId", "value"], evaluatedValue114, evaluatedValue115);
            return legacyPatchOperationSchemaResult116;
        }
    }

    private static McpJsonSchema CanonicalPatchOperationSchema(string operation, string? payloadName, McpJsonSchema? payloadSchema, string[] required, params (string Name, McpJsonSchema Schema)[] fields)
    {
        var properties = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "op", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { operation }) } }
        };
        foreach (var field in fields)
        {
            properties.Add(field.Name, field.Schema);
        }

        if (payloadName is not null && payloadSchema is not null)
        {
            properties.Add(payloadName, payloadSchema);
        }

        var objectSchemaResult = ObjectSchema(properties, ["op", .. required]);
        return objectSchemaResult;
    }

    private static McpJsonSchema LegacyPatchOperationSchema(string operation, McpJsonSchema? valueSchema, string[] required, params (string Name, McpJsonSchema Schema)[] fields)
    {
        var properties = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "operation", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { operation }) } }
        };
        foreach (var field in fields)
        {
            properties.Add(field.Name, field.Schema);
        }

        if (valueSchema is not null)
        {
            properties.Add("value", valueSchema);
        }

        var objectSchemaResult119 = ObjectSchema(properties, required);
        return objectSchemaResult119;
    }

    private static McpJsonSchema CanvasValueSchema()
    {
        var dictionary = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "id", StringSchema("Stable canvas ID.") },
            { "lifecycle", EnumSchema<CanvasLifecycleDto>() },
            { "nodes", resolveArraySchemaResult() },
            { "connections", resolveArraySchemaResult122() },
            { "name", resolveNullableSchemaResult() }
        };
        var objectSchemaResult126 = ObjectSchema(dictionary, ["id", "lifecycle", "nodes", "connections"]);
        return objectSchemaResult126;
        McpJsonSchema resolveArraySchemaResult()
        {
            var nodeValueSchemaResult120 = NodeValueSchema();
            var arraySchemaResult121 = ArraySchema(nodeValueSchemaResult120);
            return arraySchemaResult121;
        }

        McpJsonSchema resolveArraySchemaResult122()
        {
            var connectionValueSchemaResult123 = ConnectionValueSchema();
            var arraySchemaResult124 = ArraySchema(connectionValueSchemaResult123);
            return arraySchemaResult124;
        }

        McpJsonSchema resolveNullableSchemaResult()
        {
            var stringSchemaResult125 = StringSchema();
            var nullableSchemaResult = NullableSchema(stringSchemaResult125);
            return nullableSchemaResult;
        }
    }

    private static McpJsonSchema NodeValueSchema()
    {
        var evaluatedValue135 = new McpJsonSchema[]
        {
            resolveTypedNodeValueSchemaResult(),
            resolveTypedNodeValueSchemaResult127(),
            resolveTypedNodeValueSchemaResult130(),
            resolveTypedNodeValueSchemaResult132()
        };
        var schema = new McpJsonSchema { OneOf = evaluatedValue135 };
        return schema;
        McpJsonSchema resolveTypedNodeValueSchemaResult()
        {
            var nullSchemaResult = NullSchema();
            var typedNodeValueSchemaResult = TypedNodeValueSchema("action", nullSchemaResult);
            return typedNodeValueSchemaResult;
        }

        McpJsonSchema resolveTypedNodeValueSchemaResult127()
        {
            var nullSchemaResult128 = NullSchema();
            var typedNodeValueSchemaResult129 = TypedNodeValueSchema("flipflop", nullSchemaResult128);
            return typedNodeValueSchemaResult129;
        }

        McpJsonSchema resolveTypedNodeValueSchemaResult130()
        {
            var scriptValueSchemaResult = ScriptValueSchema();
            var typedNodeValueSchemaResult131 = TypedNodeValueSchema("script", scriptValueSchemaResult);
            return typedNodeValueSchemaResult131;
        }

        McpJsonSchema resolveTypedNodeValueSchemaResult132()
        {
            var nullSchemaResult133 = NullSchema();
            var typedNodeValueSchemaResult134 = TypedNodeValueSchema("flowCall", nullSchemaResult133);
            return typedNodeValueSchemaResult134;
        }
    }

    private static McpJsonSchema TypedNodeValueSchema(string type, McpJsonSchema scriptSchema)
    {
        var dictionary143 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "id", StringSchema("Stable node ID.") },
            { "type", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { type }) } },
            { "displayName", StringSchema() },
            { "x", RealSchema() },
            { "y", RealSchema() },
            { "ports", resolveArraySchemaResult136() },
            { "parameters", resolveArraySchemaResult138() },
            { "script", scriptSchema },
            { "ui", resolveNullableSchemaResult141() }
        };
        var objectSchemaResult144 = ObjectSchema(dictionary143, ["id", "type", "displayName", "x", "y", "ports", "parameters", "script"]);
        return objectSchemaResult144;
        McpJsonSchema resolveArraySchemaResult136()
        {
            var portValueSchemaResult = PortValueSchema();
            var arraySchemaResult137 = ArraySchema(portValueSchemaResult);
            return arraySchemaResult137;
        }

        McpJsonSchema resolveArraySchemaResult138()
        {
            var nodeParameterValueSchemaResult139 = NodeParameterValueSchema();
            var arraySchemaResult140 = ArraySchema(nodeParameterValueSchemaResult139);
            return arraySchemaResult140;
        }

        McpJsonSchema resolveNullableSchemaResult141()
        {
            var nodeUiValueSchemaResult = NodeUiValueSchema();
            var nullableSchemaResult142 = NullableSchema(nodeUiValueSchemaResult);
            return nullableSchemaResult142;
        }
    }

    private static McpJsonSchema PortValueSchema()
    {
        var dictionary145 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "id", StringSchema() },
            { "name", StringSchema() },
            { "direction", StringSchema() },
            { "required", BooleanSchema() }
        };
        var objectSchemaResult146 = ObjectSchema(dictionary145, ["id", "name", "direction", "required"]);
        return objectSchemaResult146;
    }

    private static McpJsonSchema NodeParameterValueSchema()
    {
        var dictionary150 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "name", StringSchema() },
            { "valueJson", resolveNullableSchemaResult147() },
            { "source", EnumSchema<DataSourceDto>() },
            { "required", BooleanSchema() },
            { "ui", ParameterUiValueSchema() }
        };
        var objectSchemaResult151 = ObjectSchema(dictionary150, ["name", "valueJson", "source", "required", "ui"]);
        return objectSchemaResult151;
        McpJsonSchema resolveNullableSchemaResult147()
        {
            var stringSchemaResult148 = StringSchema();
            var nullableSchemaResult149 = NullableSchema(stringSchemaResult148);
            return nullableSchemaResult149;
        }
    }

    private static McpJsonSchema ConnectionValueSchema()
    {
        var dictionary157 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "id", StringSchema("Stable connection ID.") },
            { "fromNodeId", StringSchema() },
            { "fromPortId", StringSchema() },
            { "toNodeId", StringSchema() },
            { "toPortId", StringSchema() },
            { "kind", EnumSchema<ConnectionKindDto>() },
            { "branch", resolveNullableSchemaResult152() },
            { "dataSource", resolveNullableSchemaResult154() },
            { "priority", NumberSchema() }
        };
        var objectSchemaResult158 = ObjectSchema(dictionary157, ["id", "fromNodeId", "fromPortId", "toNodeId", "toPortId", "kind", "branch", "dataSource", "priority"]);
        return objectSchemaResult158;
        McpJsonSchema resolveNullableSchemaResult152()
        {
            var enumSchemaResult = EnumSchema<ExecutionBranchDto>();
            var nullableSchemaResult153 = NullableSchema(enumSchemaResult);
            return nullableSchemaResult153;
        }

        McpJsonSchema resolveNullableSchemaResult154()
        {
            var enumSchemaResult155 = EnumSchema<DataSourceDto>();
            var nullableSchemaResult156 = NullableSchema(enumSchemaResult155);
            return nullableSchemaResult156;
        }
    }

    private static McpJsonSchema ScriptValueSchema()
    {
        var dictionary167 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "nodeId", StringSchema() },
            { "source", StringSchema() },
            { "languageVersion", StringSchema() },
            { "sourceHash", resolveNullableSchemaResult159() },
            { "inputs", resolveArraySchemaResult162() },
            { "outputs", resolveArraySchemaResult164() }
        };
        var objectSchemaResult168 = ObjectSchema(dictionary167, ["nodeId", "source", "languageVersion", "inputs", "outputs"]);
        return objectSchemaResult168;
        McpJsonSchema resolveNullableSchemaResult159()
        {
            var stringSchemaResult160 = StringSchema("Optional metadata; the server derives the hash from source.");
            var nullableSchemaResult161 = NullableSchema(stringSchemaResult160);
            return nullableSchemaResult161;
        }

        McpJsonSchema resolveArraySchemaResult162()
        {
            var scriptContractValueSchemaResult = ScriptContractValueSchema();
            var arraySchemaResult163 = ArraySchema(scriptContractValueSchemaResult);
            return arraySchemaResult163;
        }

        McpJsonSchema resolveArraySchemaResult164()
        {
            var scriptContractValueSchemaResult165 = ScriptContractValueSchema();
            var arraySchemaResult166 = ArraySchema(scriptContractValueSchemaResult165);
            return arraySchemaResult166;
        }
    }

    private static McpJsonSchema RunPolicyValueSchema()
    {
        var dictionary169 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "concurrencyMode", EnumSchema<FlowConcurrencyModeDto>() }
        };
        var objectSchemaResult170 = ObjectSchema(dictionary169, ["concurrencyMode"]);
        return objectSchemaResult170;
    }

    private static McpJsonSchema ParameterUiValueSchema()
    {
        var dictionary208 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "id", StringSchema() },
            { "nameKey", StringSchema() },
            { "valueKind", StringSchema() },
            { "literalValue", resolveNullableSchemaResult171() },
            { "projectInputKey", resolveNullableSchemaResult174() },
            { "expression", resolveNullableSchemaResult177() },
            { "sourceNodeId", resolveNullableSchemaResult180() },
            { "sourcePortId", resolveNullableSchemaResult183() },
            { "type", resolveNullableSchemaResult186() },
            { "description", resolveNullableSchemaResult189() },
            { "inputMode", resolveNullableSchemaResult192() },
            { "isVariadic", resolveNullableSchemaResult195() },
            { "variadicGroupId", resolveNullableSchemaResult197() },
            { "elementType", resolveNullableSchemaResult200() },
            { "variadicMode", resolveNullableSchemaResult203() },
            { "enumMetadata", resolveNullableSchemaResult206() }
        };
        var objectSchemaResult209 = ObjectSchema(dictionary208, ["id", "nameKey", "valueKind"]);
        return objectSchemaResult209;
        McpJsonSchema resolveNullableSchemaResult171()
        {
            var stringSchemaResult172 = StringSchema();
            var nullableSchemaResult173 = NullableSchema(stringSchemaResult172);
            return nullableSchemaResult173;
        }

        McpJsonSchema resolveNullableSchemaResult174()
        {
            var stringSchemaResult175 = StringSchema();
            var nullableSchemaResult176 = NullableSchema(stringSchemaResult175);
            return nullableSchemaResult176;
        }

        McpJsonSchema resolveNullableSchemaResult177()
        {
            var stringSchemaResult178 = StringSchema();
            var nullableSchemaResult179 = NullableSchema(stringSchemaResult178);
            return nullableSchemaResult179;
        }

        McpJsonSchema resolveNullableSchemaResult180()
        {
            var stringSchemaResult181 = StringSchema();
            var nullableSchemaResult182 = NullableSchema(stringSchemaResult181);
            return nullableSchemaResult182;
        }

        McpJsonSchema resolveNullableSchemaResult183()
        {
            var stringSchemaResult184 = StringSchema();
            var nullableSchemaResult185 = NullableSchema(stringSchemaResult184);
            return nullableSchemaResult185;
        }

        McpJsonSchema resolveNullableSchemaResult186()
        {
            var stringSchemaResult187 = StringSchema();
            var nullableSchemaResult188 = NullableSchema(stringSchemaResult187);
            return nullableSchemaResult188;
        }

        McpJsonSchema resolveNullableSchemaResult189()
        {
            var stringSchemaResult190 = StringSchema();
            var nullableSchemaResult191 = NullableSchema(stringSchemaResult190);
            return nullableSchemaResult191;
        }

        McpJsonSchema resolveNullableSchemaResult192()
        {
            var stringSchemaResult193 = StringSchema();
            var nullableSchemaResult194 = NullableSchema(stringSchemaResult193);
            return nullableSchemaResult194;
        }

        McpJsonSchema resolveNullableSchemaResult195()
        {
            var booleanSchemaResult = BooleanSchema();
            var nullableSchemaResult196 = NullableSchema(booleanSchemaResult);
            return nullableSchemaResult196;
        }

        McpJsonSchema resolveNullableSchemaResult197()
        {
            var stringSchemaResult198 = StringSchema();
            var nullableSchemaResult199 = NullableSchema(stringSchemaResult198);
            return nullableSchemaResult199;
        }

        McpJsonSchema resolveNullableSchemaResult200()
        {
            var stringSchemaResult201 = StringSchema();
            var nullableSchemaResult202 = NullableSchema(stringSchemaResult201);
            return nullableSchemaResult202;
        }

        McpJsonSchema resolveNullableSchemaResult203()
        {
            var stringSchemaResult204 = StringSchema();
            var nullableSchemaResult205 = NullableSchema(stringSchemaResult204);
            return nullableSchemaResult205;
        }

        McpJsonSchema resolveNullableSchemaResult206()
        {
            var enumMetadataValueSchemaResult = EnumMetadataValueSchema();
            var nullableSchemaResult207 = NullableSchema(enumMetadataValueSchemaResult);
            return nullableSchemaResult207;
        }
    }

    private static McpJsonSchema NodeUiValueSchema()
    {
        var dictionary266 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "kind", StringSchema() },
            { "titleKey", StringSchema() },
            { "subtitleKey", StringSchema() },
            { "description", resolveNullableSchemaResult210() },
            { "status", StringSchema() },
            { "hasDataOutput", BooleanSchema() },
            { "width", resolveNullableSchemaResult213() },
            { "category", resolveNullableSchemaResult215() },
            { "libraryId", resolveNullableSchemaResult218() },
            { "className", resolveNullableSchemaResult221() },
            { "methodName", resolveNullableSchemaResult224() },
            { "dllName", resolveNullableSchemaResult227() },
            { "dllVersion", resolveNullableSchemaResult230() },
            { "returnType", resolveNullableSchemaResult233() },
            { "targetNodeId", resolveNullableSchemaResult236() },
            { "targetFlowId", resolveNullableSchemaResult239() },
            { "isAwaitable", resolveNullableSchemaResult242() },
            { "staticReturnType", resolveNullableSchemaResult245() },
            { "isDynamicReturnType", resolveNullableSchemaResult248() },
            { "targetCanvasId", resolveNullableSchemaResult251() },
            { "isPublic", resolveNullableSchemaResult254() },
            { "flowCallParameterBindings", resolveNullableSchemaResult257() },
            { "libraryNodeContractId", resolveNullableSchemaResult260() },
            { "flowLibraryName", resolveNullableSchemaResult263() }
        };
        var objectSchemaResult267 = ObjectSchema(dictionary266, ["kind", "titleKey", "subtitleKey", "description", "status", "hasDataOutput", "width"]);
        return objectSchemaResult267;
        McpJsonSchema resolveNullableSchemaResult210()
        {
            var stringSchemaResult211 = StringSchema();
            var nullableSchemaResult212 = NullableSchema(stringSchemaResult211);
            return nullableSchemaResult212;
        }

        McpJsonSchema resolveNullableSchemaResult213()
        {
            var realSchemaResult = RealSchema();
            var nullableSchemaResult214 = NullableSchema(realSchemaResult);
            return nullableSchemaResult214;
        }

        McpJsonSchema resolveNullableSchemaResult215()
        {
            var stringSchemaResult216 = StringSchema();
            var nullableSchemaResult217 = NullableSchema(stringSchemaResult216);
            return nullableSchemaResult217;
        }

        McpJsonSchema resolveNullableSchemaResult218()
        {
            var stringSchemaResult219 = StringSchema();
            var nullableSchemaResult220 = NullableSchema(stringSchemaResult219);
            return nullableSchemaResult220;
        }

        McpJsonSchema resolveNullableSchemaResult221()
        {
            var stringSchemaResult222 = StringSchema();
            var nullableSchemaResult223 = NullableSchema(stringSchemaResult222);
            return nullableSchemaResult223;
        }

        McpJsonSchema resolveNullableSchemaResult224()
        {
            var stringSchemaResult225 = StringSchema();
            var nullableSchemaResult226 = NullableSchema(stringSchemaResult225);
            return nullableSchemaResult226;
        }

        McpJsonSchema resolveNullableSchemaResult227()
        {
            var stringSchemaResult228 = StringSchema();
            var nullableSchemaResult229 = NullableSchema(stringSchemaResult228);
            return nullableSchemaResult229;
        }

        McpJsonSchema resolveNullableSchemaResult230()
        {
            var stringSchemaResult231 = StringSchema();
            var nullableSchemaResult232 = NullableSchema(stringSchemaResult231);
            return nullableSchemaResult232;
        }

        McpJsonSchema resolveNullableSchemaResult233()
        {
            var stringSchemaResult234 = StringSchema();
            var nullableSchemaResult235 = NullableSchema(stringSchemaResult234);
            return nullableSchemaResult235;
        }

        McpJsonSchema resolveNullableSchemaResult236()
        {
            var stringSchemaResult237 = StringSchema();
            var nullableSchemaResult238 = NullableSchema(stringSchemaResult237);
            return nullableSchemaResult238;
        }

        McpJsonSchema resolveNullableSchemaResult239()
        {
            var stringSchemaResult240 = StringSchema();
            var nullableSchemaResult241 = NullableSchema(stringSchemaResult240);
            return nullableSchemaResult241;
        }

        McpJsonSchema resolveNullableSchemaResult242()
        {
            var booleanSchemaResult243 = BooleanSchema();
            var nullableSchemaResult244 = NullableSchema(booleanSchemaResult243);
            return nullableSchemaResult244;
        }

        McpJsonSchema resolveNullableSchemaResult245()
        {
            var stringSchemaResult246 = StringSchema();
            var nullableSchemaResult247 = NullableSchema(stringSchemaResult246);
            return nullableSchemaResult247;
        }

        McpJsonSchema resolveNullableSchemaResult248()
        {
            var booleanSchemaResult249 = BooleanSchema();
            var nullableSchemaResult250 = NullableSchema(booleanSchemaResult249);
            return nullableSchemaResult250;
        }

        McpJsonSchema resolveNullableSchemaResult251()
        {
            var stringSchemaResult252 = StringSchema();
            var nullableSchemaResult253 = NullableSchema(stringSchemaResult252);
            return nullableSchemaResult253;
        }

        McpJsonSchema resolveNullableSchemaResult254()
        {
            var booleanSchemaResult255 = BooleanSchema();
            var nullableSchemaResult256 = NullableSchema(booleanSchemaResult255);
            return nullableSchemaResult256;
        }

        McpJsonSchema resolveNullableSchemaResult257()
        {
            var flowCallParameterBindingValueSchemaResult = FlowCallParameterBindingValueSchema();
            var arraySchemaResult258 = ArraySchema(flowCallParameterBindingValueSchemaResult);
            var nullableSchemaResult259 = NullableSchema(arraySchemaResult258);
            return nullableSchemaResult259;
        }

        McpJsonSchema resolveNullableSchemaResult260()
        {
            var stringSchemaResult261 = StringSchema();
            var nullableSchemaResult262 = NullableSchema(stringSchemaResult261);
            return nullableSchemaResult262;
        }

        McpJsonSchema resolveNullableSchemaResult263()
        {
            var stringSchemaResult264 = StringSchema();
            var nullableSchemaResult265 = NullableSchema(stringSchemaResult264);
            return nullableSchemaResult265;
        }
    }

    private static McpJsonSchema FlowCallParameterBindingValueSchema()
    {
        var dictionary268 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "callParameterId", StringSchema() },
            { "targetParameterId", StringSchema() }
        };
        var objectSchemaResult269 = ObjectSchema(dictionary268, ["callParameterId", "targetParameterId"]);
        return objectSchemaResult269;
    }

    private static McpJsonSchema EnumMetadataValueSchema()
    {
        var dictionary272 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "typeName", StringSchema() },
            { "isFlags", BooleanSchema() },
            { "underlyingType", StringSchema() },
            { "options", resolveArraySchemaResult270() }
        };
        var objectSchemaResult273 = ObjectSchema(dictionary272, ["typeName", "isFlags", "underlyingType", "options"]);
        return objectSchemaResult273;
        McpJsonSchema resolveArraySchemaResult270()
        {
            var enumOptionValueSchemaResult = EnumOptionValueSchema();
            var arraySchemaResult271 = ArraySchema(enumOptionValueSchemaResult);
            return arraySchemaResult271;
        }
    }

    private static McpJsonSchema EnumOptionValueSchema()
    {
        var dictionary274 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "name", StringSchema() },
            { "numericValue", StringSchema() }
        };
        var objectSchemaResult275 = ObjectSchema(dictionary274, ["name", "numericValue"]);
        return objectSchemaResult275;
    }

    internal static McpJsonSchema PositionSchema()
    {
        var dictionary276 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "x", RealSchema() },
            { "y", RealSchema() }
        };
        var objectSchemaResult277 = ObjectSchema(dictionary276, ["x", "y"]);
        return objectSchemaResult277;
    }

    private static McpJsonSchema NullSchema()
    {
        var schema = new McpJsonSchema { Type = "null" };
        return schema;
    }
    private static McpJsonSchema ScriptContractValueSchema()
    {
        var dictionary284 = new Dictionary<string, McpJsonSchema>(StringComparer.Ordinal)
        {
            { "name", StringSchema() },
            { "valueKind", StringSchema() },
            { "required", BooleanSchema() },
            { "id", resolveNullableSchemaResult278() },
            { "description", resolveNullableSchemaResult281() }
        };
        var objectSchemaResult285 = ObjectSchema(dictionary284, ["name", "valueKind", "required"]);
        return objectSchemaResult285;
        McpJsonSchema resolveNullableSchemaResult278()
        {
            var stringSchemaResult279 = StringSchema();
            var nullableSchemaResult280 = NullableSchema(stringSchemaResult279);
            return nullableSchemaResult280;
        }

        McpJsonSchema resolveNullableSchemaResult281()
        {
            var stringSchemaResult282 = StringSchema();
            var nullableSchemaResult283 = NullableSchema(stringSchemaResult282);
            return nullableSchemaResult283;
        }
    }

    private static McpJsonSchema ObjectSchema(Dictionary<string, McpJsonSchema>? properties = null, string[]? required = null, bool additionalProperties = false)
    {
        var schema = McpJsonSchema.Object(properties, required, additionalProperties);
        return schema;
    }

    private static McpJsonSchema NullableSchema(McpJsonSchema schema)
    {
        var nullableSchema = new McpJsonSchema { OneOf = [schema, new McpJsonSchema { Type = "null" }] };
        return nullableSchema;
    }

    private static McpJsonSchema RealSchema()
    {
        var schema = new McpJsonSchema { Type = "number" };
        return schema;
    }

    private static McpJsonSchema EnumSchema<TEnum>()
        where TEnum : struct, Enum
    {
        var names = Enum.GetNames<TEnum>()
            .Select(JsonNamingPolicy.CamelCase.ConvertName)
            .ToArray();
        var numbers = Enum.GetValues<TEnum>()
            .Select(value =>
            {
                var number = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                return number;
            })
            .ToArray();
        var stringValues = JsonSerializer.SerializeToElement(names);
        var numericValues = JsonSerializer.SerializeToElement(numbers);
        var schema = new McpJsonSchema
        {
            OneOf =
            [
                new McpJsonSchema { Type = "string", Enum = stringValues },
                new McpJsonSchema { Type = "integer", Enum = numericValues }
            ]
        };
        return schema;
    }
}
