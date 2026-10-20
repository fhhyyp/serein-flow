using System.Collections.Generic;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;
public sealed class FlowPatchContractNormalizerTests
{
    [Theory]
    [MemberData(nameof(V2Operations))]
    public void NormalizesEveryV2Operation(string expectedOp, object operation)
    {
        var flowPatchContractNormalizer = new FlowPatchContractNormalizer();
        var requestResult = Request(operation);
        var normalized = flowPatchContractNormalizer.Normalize(requestResult);
        Assert.Equal(FlowPatchContract.CurrentSchemaVersion, normalized.Request.SchemaVersion);
        var evaluatedValue = Assert.Single(normalized.Request.Operations).Op;
        Assert.Equal(expectedOp, evaluatedValue);
        Assert.Empty(normalized.Warnings);
    }

    [Fact]
    public void RejectsMissingNestedFieldsWrongEnumsAndTypeMismatches()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var missingField = Assert.Throws<FlowPatchContractException>(() =>
        {
            var emptyResult = Array.Empty<object>();
            var emptyResult1 = Array.Empty<object>();
            var evaluatedValue2 = new
            {
                id = "node-missing-name",
                type = "action",
                x = 1,
                y = 2,
                ports = emptyResult,
                parameters = emptyResult1,
                script = (object? )null
            };
            var evaluatedValue3 = new
            {
                op = "addNode",
                canvasId = "main",
                node = evaluatedValue2
            };
            var requestResult4 = Request(evaluatedValue3);
            var normalizeResult = normalizer.Normalize(requestResult4);
            return normalizeResult;
        });
        Assert.Equal(McpErrorCodes.FlowPatchFieldRequired, missingField.Code);
        Assert.Equal("$.operations[0].node.displayName", missingField.FieldPath);
        var wrongEnum = Assert.Throws<FlowPatchContractException>(() =>
        {
            var requestResult5 = Request(new { op = "setRunPolicy", runPolicy = new { concurrencyMode = "Parallel" } });
            var normalizeResult6 = normalizer.Normalize(requestResult5);
            return normalizeResult6;
        });
        Assert.Equal(McpErrorCodes.FlowPatchEnumEncodingInvalid, wrongEnum.Code);
        Assert.Equal("$.operations[0].runPolicy.concurrencyMode", wrongEnum.FieldPath);
        var wrongTypePayload = Assert.Throws<FlowPatchContractException>(() =>
        {
            var scriptResult = Script("node-action");
            var actionNodeResult = ActionNode("node-action", script: scriptResult);
            var evaluatedValue7 = new
            {
                op = "addNode",
                canvasId = "main",
                node = actionNodeResult
            };
            var requestResult8 = Request(evaluatedValue7);
            var normalizeResult9 = normalizer.Normalize(requestResult8);
            return normalizeResult9;
        });
        Assert.Equal(McpErrorCodes.FlowPatchUnexpectedField, wrongTypePayload.Code);
        Assert.Equal("$.operations[0].node.script", wrongTypePayload.FieldPath);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "stale-source-hash")]
    public void ScriptSourceHashIsOptionalMetadata(bool includeHash, string? sourceHash)
    {
        var script = new Dictionary<string, object?>
        {
            ["nodeId"] = "node-script",
            ["source"] = "return 1",
            ["languageVersion"] = "0.1",
            ["inputs"] = Array.Empty<object>(),
            ["outputs"] = Array.Empty<object>(),
        };
        if (includeHash)
        {
            script["sourceHash"] = sourceHash;
        }

        var flowPatchContractNormalizer10 = new FlowPatchContractNormalizer();
        var emptyResult11 = Array.Empty<object>();
        var emptyResult12 = Array.Empty<object>();
        var evaluatedValue13 = new
        {
            id = "node-script",
            type = "script",
            displayName = "Script",
            x = 0,
            y = 0,
            ports = emptyResult11,
            parameters = emptyResult12,
            script
        };
        var evaluatedValue14 = new
        {
            op = "addNode",
            canvasId = "main",
            node = evaluatedValue13
        };
        var requestResult15 = Request(evaluatedValue14);
        var normalized = flowPatchContractNormalizer10.Normalize(requestResult15);
        var evaluatedValue16 = Assert.Single(normalized.Request.Operations).Op;
        Assert.Equal("addNode", evaluatedValue16);
        Assert.Empty(normalized.Warnings);
    }

    [Fact]
    public void AllowsReturnTypeMetadataOnBuiltInScriptAndFlowCallNodes()
    {
        var flowPatchContractNormalizer17 = new FlowPatchContractNormalizer();
        var emptyResult18 = Array.Empty<object>();
        var emptyResult19 = Array.Empty<object>();
        var scriptResult20 = Script("node-script");
        var evaluatedValue21 = new
        {
            id = "node-script",
            type = "script",
            displayName = "Script",
            x = 20,
            y = 40,
            ports = emptyResult18,
            parameters = emptyResult19,
            script = scriptResult20,
            ui = new
            {
                kind = "script",
                titleKey = "node.kind.script",
                subtitleKey = "node.scriptSubtitle",
                description = (string? )null,
                status = "ready",
                hasDataOutput = true,
                width = 260,
                category = "basic",
                returnType = "ScriptLang.Runtime.Value"
            }
        };
        var evaluatedValue22 = new
        {
            op = "addNode",
            canvasId = "main",
            node = evaluatedValue21
        };
        var emptyResult23 = Array.Empty<object>();
        var emptyResult24 = Array.Empty<object>();
        var evaluatedValue25 = new
        {
            id = "node-flow-call",
            type = "flowCall",
            displayName = "FlowCall",
            x = 20,
            y = 40,
            ports = emptyResult23,
            parameters = emptyResult24,
            script = (object? )null,
            ui = new
            {
                kind = "flowCall",
                titleKey = "node.kind.flowCall",
                subtitleKey = "node.flowCallSubtitle",
                description = (string? )null,
                status = "ready",
                hasDataOutput = true,
                width = 260,
                category = "basic",
                returnType = "System.Object"
            }
        };
        var evaluatedValue26 = new
        {
            op = "addNode",
            canvasId = "main",
            node = evaluatedValue25
        };
        var requestResult27 = Request(evaluatedValue22, evaluatedValue26);
        var normalized = flowPatchContractNormalizer17.Normalize(requestResult27);
        var selectResult = normalized.Request.Operations.Select(operation => operation.Op);
        Assert.Equal(["addNode", "addNode"], selectResult);
    }

    [Fact]
    public void RejectsObsoleteFlowLibraryNodeContractIdField()
    {
        var exception = Assert.Throws<FlowPatchContractException>(() =>
        {
            var flowPatchContractNormalizer28 = new FlowPatchContractNormalizer();
            var emptyResult29 = Array.Empty<object>();
            var emptyResult30 = Array.Empty<object>();
            var evaluatedValue31 = new
            {
                id = "node-action",
                type = "action",
                displayName = "Action",
                x = 20,
                y = 40,
                ports = emptyResult29,
                parameters = emptyResult30,
                script = (object? )null,
                ui = new
                {
                    kind = "action",
                    titleKey = "node.catalogMethod",
                    subtitleKey = "node.catalogSubtitle",
                    description = (string? )null,
                    status = "ready",
                    hasDataOutput = true,
                    width = 260,
                    category = "method",
                    flowLibraryNodeContractId = "legacy-contract"
                }
            };
            var evaluatedValue32 = new
            {
                op = "addNode",
                canvasId = "main",
                node = evaluatedValue31
            };
            var requestResult33 = Request(evaluatedValue32);
            var normalizeResult34 = flowPatchContractNormalizer28.Normalize(requestResult33);
            return normalizeResult34;
        });
        Assert.Equal(McpErrorCodes.FlowPatchUnexpectedField, exception.Code);
        Assert.Equal("$.operations[0].node.ui.flowLibraryNodeContractId", exception.FieldPath);
    }

    [Fact]
    public void RejectsDuplicateIdsAndUnresolvedReferencesBeforeMutation()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var duplicatePort = Assert.Throws<FlowPatchContractException>(() =>
        {
            var actionNodeResult35 = ActionNode("node-duplicate-port", ports: [new { id = "exec-in", name = "Input", direction = "input", required = false }, new { id = "exec-in", name = "Input again", direction = "input", required = false }]);
            var evaluatedValue36 = new
            {
                op = "addNode",
                canvasId = "main",
                node = actionNodeResult35
            };
            var requestResult37 = Request(evaluatedValue36);
            var normalizeResult38 = normalizer.Normalize(requestResult37);
            return normalizeResult38;
        });
        Assert.Equal(McpErrorCodes.FlowPatchDuplicateId, duplicatePort.Code);
        Assert.Equal("$.operations[0].node.ports[1].id", duplicatePort.FieldPath);
        var requestResult39 = Request(new { op = "addConnection", canvasId = "main", connection = new { id = "connection-invalid", fromNodeId = "node-existing", fromPortId = "missing-port", toNodeId = "node-existing", toPortId = "exec-in", kind = "execution", branch = (string? )null, dataSource = (string? )null, priority = 0 } });
        var normalized = normalizer.Normalize(requestResult39);
        var exception = Assert.Throws<FlowPatchContractException>(() =>
        {
            var currentDefinitionResult = CurrentDefinition();
            normalizer.ValidateReferences(currentDefinitionResult, normalized.Request.Operations);
        });
        Assert.Equal(McpErrorCodes.FlowPatchReferenceInvalid, exception.Code);
        Assert.Equal("$.operations[0].connection.fromPortId", exception.FieldPath);
    }

    [Fact]
    public void ValidatesDataConnectionTargetAgainstParameterId()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var dataConnectionResult = DataConnection("data-connection", "amount");
        var evaluatedValue40 = new
        {
            op = "addConnection",
            canvasId = "main",
            connection = dataConnectionResult
        };
        var requestResult41 = Request(evaluatedValue40);
        var normalized = normalizer.Normalize(requestResult41);
        var currentDefinitionResult42 = CurrentDefinition();
        normalizer.ValidateReferences(currentDefinitionResult42, normalized.Request.Operations);
    }

    [Fact]
    public void NormalizesDataConnectionSourceToPreviousNode()
    {
        var flowPatchContractNormalizer43 = new FlowPatchContractNormalizer();
        var dataConnectionResult44 = DataConnection("data-connection", "amount");
        var evaluatedValue45 = new
        {
            op = "addConnection",
            canvasId = "main",
            connection = dataConnectionResult44
        };
        var requestResult46 = Request(evaluatedValue45);
        var normalized = flowPatchContractNormalizer43.Normalize(requestResult46);
        var evaluatedValue47 = Assert.Single(normalized.Request.Operations).Connection!.DataSource;
        Assert.Equal(DataSourceDto.PreviousNode, evaluatedValue47);
    }

    [Fact]
    public void NormalizesExecutionConnectionSourceToNull()
    {
        var flowPatchContractNormalizer48 = new FlowPatchContractNormalizer();
        var requestResult49 = Request(new { op = "addConnection", canvasId = "main", connection = new { id = "execution-connection", fromNodeId = "node-existing", fromPortId = "exec-success", toNodeId = "node-existing", toPortId = "exec-in", kind = "execution", branch = "success", dataSource = "literal", priority = 0 } });
        var normalized = flowPatchContractNormalizer48.Normalize(requestResult49);
        var evaluatedValue50 = Assert.Single(normalized.Request.Operations).Connection!.DataSource;
        Assert.Null(evaluatedValue50);
    }

    [Fact]
    public void RejectsDataConnectionTargetingParameterPortId()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var dataConnectionResult52 = DataConnection("data-connection", "param-amount");
        var evaluatedValue53 = new
        {
            op = "addConnection",
            canvasId = "main",
            connection = dataConnectionResult52
        };
        var requestResult54 = Request(evaluatedValue53);
        var normalized = normalizer.Normalize(requestResult54);
        var exception = Assert.Throws<FlowPatchContractException>(() =>
        {
            var currentDefinitionResult51 = CurrentDefinition();
            normalizer.ValidateReferences(currentDefinitionResult51, normalized.Request.Operations);
        });
        Assert.Equal(McpErrorCodes.FlowPatchReferenceInvalid, exception.Code);
        Assert.Equal("$.operations[0].connection.toPortId", exception.FieldPath);
        Assert.Equal("a parameter ID on toNodeId", exception.Expected);
    }

    [Fact]
    public void ParameterCanBeAddedBeforeAConnectionTargetsIt()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var parameterResult = Parameter("amount-2");
        var evaluatedValue55 = new
        {
            op = "addNodeParameter",
            canvasId = "main",
            nodeId = "node-existing",
            parameter = parameterResult
        };
        var dataConnectionResult56 = DataConnection("data-connection", "amount-2");
        var evaluatedValue57 = new
        {
            op = "addConnection",
            canvasId = "main",
            connection = dataConnectionResult56
        };
        var requestResult58 = Request(evaluatedValue55, evaluatedValue57);
        var normalized = normalizer.Normalize(requestResult58);
        var currentDefinitionResult59 = CurrentDefinition();
        normalizer.ValidateReferences(currentDefinitionResult59, normalized.Request.Operations);
    }

    [Fact]
    public void ParameterCannotBeRemovedBeforeItsDataConnections()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var requestResult60 = Request(new { op = "removeNodeParameter", canvasId = "main", nodeId = "node-existing", parameterId = "amount", });
        var normalized = normalizer.Normalize(requestResult60);
        var current = CurrentDefinition();
        var definition = current with
        {
            Canvases = [current.Canvases[0] with
            {
                Connections = [DataConnectionDto("data-connection", "amount")],
            }

            ]
        };
        var exception = Assert.Throws<FlowPatchContractException>(() =>
        {
            normalizer.ValidateReferences(definition, normalized.Request.Operations);
        });
        Assert.Equal(McpErrorCodes.FlowPatchReferenceInvalid, exception.Code);
        Assert.Equal("$.operations[0].parameterId", exception.FieldPath);
    }

    public static IEnumerable<object[]> V2Operations()
    {
        yield return["addCanvas", new
        {
            op = "addCanvas",
            canvas = Canvas("extra")
        }

        ];
        yield return["updateCanvas", new
        {
            op = "updateCanvas",
            canvasId = "main",
            canvas = Canvas("main")
        }

        ];
        yield return["removeCanvas", new
        {
            op = "removeCanvas",
            canvasId = "extra"
        }

        ];
        yield return["addNode", new
        {
            op = "addNode",
            canvasId = "main",
            node = ActionNode("node-added")
        }

        ];
        yield return["replaceNode", new
        {
            op = "replaceNode",
            canvasId = "main",
            nodeId = "node-existing",
            node = ActionNode("node-existing")
        }

        ];
        yield return["removeNode", new
        {
            op = "removeNode",
            canvasId = "main",
            nodeId = "node-existing"
        }

        ];
        yield return["setNodeParameter", new
        {
            op = "setNodeParameter",
            canvasId = "main",
            nodeId = "node-existing",
            parameterId = "amount",
            parameter = Parameter("amount")
        }

        ];
        yield return["addNodeParameter", new
        {
            op = "addNodeParameter",
            canvasId = "main",
            nodeId = "node-existing",
            parameter = Parameter("amount-2")
        }

        ];
        yield return["removeNodeParameter", new
        {
            op = "removeNodeParameter",
            canvasId = "main",
            nodeId = "node-existing",
            parameterId = "amount"
        }

        ];
        yield return["addConnection", new
        {
            op = "addConnection",
            canvasId = "main",
            connection = Connection("connection-added")
        }

        ];
        yield return["replaceConnection", new
        {
            op = "replaceConnection",
            canvasId = "main",
            connectionId = "connection-existing",
            connection = Connection("connection-existing")
        }

        ];
        yield return["removeConnection", new
        {
            op = "removeConnection",
            canvasId = "main",
            connectionId = "connection-existing"
        }

        ];
        yield return["setEntryNode", new
        {
            op = "setEntryNode",
            entryNodeId = "node-existing"
        }

        ];
        yield return["setRunPolicy", new
        {
            op = "setRunPolicy",
            runPolicy = new
            {
                concurrencyMode = "parallel"
            }
        }

        ];
        yield return["replaceScriptSource", new
        {
            op = "replaceScriptSource",
            canvasId = "main",
            nodeId = "node-script",
            source = "result = 1"
        }

        ];
    }

    private static JsonElement Request(params object[] operations)
    {
        var newGuidResult = Guid.NewGuid();
        var newGuidResult61 = Guid.NewGuid();
        var evaluatedValue62 = new
        {
            projectId = newGuidResult,
            flowId = newGuidResult61,
            expectedDevelopmentVersion = 1,
            schemaVersion = "2.0",
            operations
        };
        var serializeToElementResult = JsonSerializer.SerializeToElement(evaluatedValue62);
        return serializeToElementResult;
    }

    private static object Canvas(string id)
    {
        var evaluatedValue63 = new object[]
        {
            ActionNode("node-canvas")
        };
        var emptyResult64 = Array.Empty<object>();
        return new
        {
            id,
            lifecycle = "custom",
            nodes = evaluatedValue63,
            connections = emptyResult64,
            name = "Additional"
        };
    }

    private static object ActionNode(string id, object? script = null, object[]? ports = null)
    {
        var emptyResult65 = Array.Empty<object>();
        return new
        {
            id,
            type = "action",
            displayName = "Action",
            x = 20,
            y = 40,
            ports = ports ?? [new
            {
                id = "exec-in",
                name = "Input",
                direction = "input",
                required = false
            }, new
            {
                id = "exec-success",
                name = "Success",
                direction = "output",
                required = false
            }

            ],
            parameters = emptyResult65,
            script
        };
    }

    private static object Parameter(string id) => new
    {
        name = "amount",
        valueJson = "1",
        source = "literal",
        required = true,
        ui = new
        {
            id,
            nameKey = "amount",
            valueKind = "System.Int32",
            literalValue = "1",
            projectInputKey = (string? )null,
            expression = (string? )null,
            sourceNodeId = (string? )null,
            sourcePortId = (string? )null
        }
    };
    private static object Connection(string id) => new
    {
        id,
        fromNodeId = "node-existing",
        fromPortId = "exec-success",
        toNodeId = "node-existing",
        toPortId = "exec-in",
        kind = "execution",
        branch = (string? )null,
        dataSource = (string? )null,
        priority = 0
    };
    private static object DataConnection(string id, string targetParameterId) => new
    {
        id,
        fromNodeId = "node-existing",
        fromPortId = "data-out",
        toNodeId = "node-existing",
        toPortId = targetParameterId,
        kind = "data",
        branch = (string? )null,
        dataSource = (string? )null,
        priority = 0
    };
    private static ConnectionDto DataConnectionDto(string id, string targetParameterId)
    {
        ConnectionDto evaluatedValue66 = new(id, "node-existing", "data-out", "node-existing", targetParameterId, ConnectionKindDto.Data, null, DataSourceDto.PreviousNode, 0);
        return evaluatedValue66;
    }

    private static object Script(string nodeId)
    {
        var emptyResult67 = Array.Empty<object>();
        var emptyResult68 = Array.Empty<object>();
        return new
        {
            nodeId,
            source = "result = 1",
            languageVersion = "0.1",
            sourceHash = "hash",
            inputs = emptyResult67,
            outputs = emptyResult68
        };
    }

    private static FlowDefinitionDto CurrentDefinition()
    {
        var newGuidResult69 = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue73 = [resolveCanvasDto()];
        FlowDefinitionDto evaluatedValue74 = new(newGuidResult69, 5, 1, evaluatedValue73, "node-existing", "checksum");
        return evaluatedValue74;
        CanvasDto resolveCanvasDto()
        {
            IReadOnlyList<NodeDto> evaluatedValue72 = [resolveNodeDto()];
            var canvasDto = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue72, []);
            return canvasDto;
            NodeDto resolveNodeDto()
            {
                IReadOnlyList<NodePortDto> evaluatedValue70 = [new NodePortDto("exec-in", "Input", "input", false), new NodePortDto("exec-success", "Success", "output", false), new NodePortDto("data-out", "Data output", "output", false), new NodePortDto("param-amount", "amount", "input", false)];
                IReadOnlyList<NodeParameterDto> evaluatedValue71 = [resolveNodeParameterDto()];
                var nodeDto = new NodeDto("node-existing", NodeTypeDto.Action, "Existing", 0, 0, evaluatedValue70, evaluatedValue71, null);
                return nodeDto;
                NodeParameterDto resolveNodeParameterDto()
                {
                    var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("amount", "amount", "System.Int32", "1", null, null, null, null);
                    var nodeParameterDto = new NodeParameterDto("amount", "1", DataSourceDto.Literal, true, nodeParameterUiMetadataDto);
                    return nodeParameterDto;
                }
            }
        }
    }
}
