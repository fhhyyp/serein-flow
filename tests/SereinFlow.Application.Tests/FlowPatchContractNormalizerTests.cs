using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;

public sealed class FlowPatchContractNormalizerTests
{
    [Theory]
    [MemberData(nameof(V2Operations))]
    public void NormalizesEveryV2Operation(string expectedOp, object operation)
    {
        var normalized = new FlowPatchContractNormalizer().Normalize(Request(operation));

        Assert.Equal(FlowPatchContract.CurrentSchemaVersion, normalized.Request.SchemaVersion);
        Assert.Equal(expectedOp, Assert.Single(normalized.Request.Operations).Op);
        Assert.Empty(normalized.Warnings);
    }

    [Fact]
    public void RejectsMissingNestedFieldsWrongEnumsAndTypeMismatches()
    {
        var normalizer = new FlowPatchContractNormalizer();

        var missingField = Assert.Throws<FlowPatchContractException>(() => normalizer.Normalize(Request(new
        {
            op = "addNode",
            canvasId = "main",
            node = new
            {
                id = "node-missing-name",
                type = "action",
                x = 1,
                y = 2,
                ports = Array.Empty<object>(),
                parameters = Array.Empty<object>(),
                script = (object?)null
            }
        })));
        Assert.Equal("mcp.flow_patch.field_required", missingField.Code);
        Assert.Equal("$.operations[0].node.displayName", missingField.FieldPath);

        var wrongEnum = Assert.Throws<FlowPatchContractException>(() => normalizer.Normalize(Request(new
        {
            op = "setRunPolicy",
            runPolicy = new { concurrencyMode = "Parallel" }
        })));
        Assert.Equal("mcp.flow_patch.enum_encoding_invalid", wrongEnum.Code);
        Assert.Equal("$.operations[0].runPolicy.concurrencyMode", wrongEnum.FieldPath);

        var wrongTypePayload = Assert.Throws<FlowPatchContractException>(() => normalizer.Normalize(Request(new
        {
            op = "addNode",
            canvasId = "main",
            node = ActionNode("node-action", script: Script("node-action"))
        })));
        Assert.Equal("mcp.flow_patch.unexpected_field", wrongTypePayload.Code);
        Assert.Equal("$.operations[0].node.script", wrongTypePayload.FieldPath);
    }

    [Fact]
    public void RejectsDuplicateIdsAndUnresolvedReferencesBeforeMutation()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var duplicatePort = Assert.Throws<FlowPatchContractException>(() => normalizer.Normalize(Request(new
        {
            op = "addNode",
            canvasId = "main",
            node = ActionNode(
                "node-duplicate-port",
                ports:
                [
                    new { id = "exec-in", name = "Input", direction = "input", required = false },
                    new { id = "exec-in", name = "Input again", direction = "input", required = false }
                ])
        })));
        Assert.Equal("mcp.flow_patch.duplicate_id", duplicatePort.Code);
        Assert.Equal("$.operations[0].node.ports[1].id", duplicatePort.FieldPath);

        var normalized = normalizer.Normalize(Request(new
        {
            op = "addConnection",
            canvasId = "main",
            connection = new
            {
                id = "connection-invalid",
                fromNodeId = "node-existing",
                fromPortId = "missing-port",
                toNodeId = "node-existing",
                toPortId = "exec-in",
                kind = "execution",
                branch = (string?)null,
                dataSource = (string?)null,
                priority = 0
            }
        }));

        var exception = Assert.Throws<FlowPatchContractException>(() => normalizer.ValidateReferences(CurrentDefinition(), normalized.Request.Operations));
        Assert.Equal("mcp.flow_patch.reference_invalid", exception.Code);
        Assert.Equal("$.operations[0].connection.fromPortId", exception.FieldPath);
    }

    [Fact]
    public void ValidatesDataConnectionTargetAgainstParameterId()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var normalized = normalizer.Normalize(Request(new
        {
            op = "addConnection",
            canvasId = "main",
            connection = DataConnection("data-connection", "amount")
        }));

        normalizer.ValidateReferences(CurrentDefinition(), normalized.Request.Operations);
    }

    [Fact]
    public void NormalizesDataConnectionSourceToPreviousNode()
    {
        var normalized = new FlowPatchContractNormalizer().Normalize(Request(new
        {
            op = "addConnection",
            canvasId = "main",
            connection = DataConnection("data-connection", "amount")
        }));

        Assert.Equal(DataSourceDto.PreviousNode, Assert.Single(normalized.Request.Operations).Connection!.DataSource);
    }

    [Fact]
    public void NormalizesExecutionConnectionSourceToNull()
    {
        var normalized = new FlowPatchContractNormalizer().Normalize(Request(new
        {
            op = "addConnection",
            canvasId = "main",
            connection = new
            {
                id = "execution-connection",
                fromNodeId = "node-existing",
                fromPortId = "exec-success",
                toNodeId = "node-existing",
                toPortId = "exec-in",
                kind = "execution",
                branch = "success",
                dataSource = "literal",
                priority = 0
            }
        }));

        Assert.Null(Assert.Single(normalized.Request.Operations).Connection!.DataSource);
    }

    [Fact]
    public void RejectsDataConnectionTargetingParameterPortId()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var normalized = normalizer.Normalize(Request(new
        {
            op = "addConnection",
            canvasId = "main",
            connection = DataConnection("data-connection", "param-amount")
        }));

        var exception = Assert.Throws<FlowPatchContractException>(() =>
            normalizer.ValidateReferences(CurrentDefinition(), normalized.Request.Operations));

        Assert.Equal("mcp.flow_patch.reference_invalid", exception.Code);
        Assert.Equal("$.operations[0].connection.toPortId", exception.FieldPath);
        Assert.Equal("a parameter ID on toNodeId", exception.Expected);
    }

    [Fact]
    public void ParameterCanBeAddedBeforeAConnectionTargetsIt()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var normalized = normalizer.Normalize(Request(
            new
            {
                op = "addNodeParameter",
                canvasId = "main",
                nodeId = "node-existing",
                parameter = Parameter("amount-2"),
            },
            new
            {
                op = "addConnection",
                canvasId = "main",
                connection = DataConnection("data-connection", "amount-2"),
            }));

        normalizer.ValidateReferences(CurrentDefinition(), normalized.Request.Operations);
    }

    [Fact]
    public void ParameterCannotBeRemovedBeforeItsDataConnections()
    {
        var normalizer = new FlowPatchContractNormalizer();
        var normalized = normalizer.Normalize(Request(new
        {
            op = "removeNodeParameter",
            canvasId = "main",
            nodeId = "node-existing",
            parameterId = "amount",
        }));
        var current = CurrentDefinition();
        var definition = current with
        {
            Canvases =
            [current.Canvases[0] with
            {
                Connections = [DataConnectionDto("data-connection", "amount")],
            }]
        };

        var exception = Assert.Throws<FlowPatchContractException>(() =>
            normalizer.ValidateReferences(definition, normalized.Request.Operations));

        Assert.Equal("mcp.flow_patch.reference_invalid", exception.Code);
        Assert.Equal("$.operations[0].parameterId", exception.FieldPath);
    }

    public static IEnumerable<object[]> V2Operations()
    {
        yield return ["addCanvas", new { op = "addCanvas", canvas = Canvas("extra") }];
        yield return ["updateCanvas", new { op = "updateCanvas", canvasId = "main", canvas = Canvas("main") }];
        yield return ["removeCanvas", new { op = "removeCanvas", canvasId = "extra" }];
        yield return ["addNode", new { op = "addNode", canvasId = "main", node = ActionNode("node-added") }];
        yield return ["replaceNode", new { op = "replaceNode", canvasId = "main", nodeId = "node-existing", node = ActionNode("node-existing") }];
        yield return ["removeNode", new { op = "removeNode", canvasId = "main", nodeId = "node-existing" }];
        yield return ["setNodeParameter", new { op = "setNodeParameter", canvasId = "main", nodeId = "node-existing", parameterId = "amount", parameter = Parameter("amount") }];
        yield return ["addNodeParameter", new { op = "addNodeParameter", canvasId = "main", nodeId = "node-existing", parameter = Parameter("amount-2") }];
        yield return ["removeNodeParameter", new { op = "removeNodeParameter", canvasId = "main", nodeId = "node-existing", parameterId = "amount" }];
        yield return ["addConnection", new { op = "addConnection", canvasId = "main", connection = Connection("connection-added") }];
        yield return ["replaceConnection", new { op = "replaceConnection", canvasId = "main", connectionId = "connection-existing", connection = Connection("connection-existing") }];
        yield return ["removeConnection", new { op = "removeConnection", canvasId = "main", connectionId = "connection-existing" }];
        yield return ["setEntryNode", new { op = "setEntryNode", entryNodeId = "node-existing" }];
        yield return ["setRunPolicy", new { op = "setRunPolicy", runPolicy = new { concurrencyMode = "parallel" } }];
        yield return ["replaceScriptSource", new { op = "replaceScriptSource", canvasId = "main", nodeId = "node-script", source = "result = 1" }];
    }

    private static JsonElement Request(params object[] operations)
        => JsonSerializer.SerializeToElement(new
        {
            projectId = Guid.NewGuid(),
            flowId = Guid.NewGuid(),
            expectedDevelopmentVersion = 1,
            schemaVersion = "2.0",
            operations
        });

    private static object Canvas(string id)
        => new
        {
            id,
            lifecycle = "custom",
            nodes = new object[] { ActionNode("node-canvas") },
            connections = Array.Empty<object>(),
            name = "Additional"
        };

    private static object ActionNode(
        string id,
        object? script = null,
        object[]? ports = null)
        => new
        {
            id,
            type = "action",
            displayName = "Action",
            x = 20,
            y = 40,
            ports = ports ??
            [
                new { id = "exec-in", name = "Input", direction = "input", required = false },
                new { id = "exec-success", name = "Success", direction = "output", required = false }
            ],
            parameters = Array.Empty<object>(),
            script
        };

    private static object Parameter(string id)
        => new
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
                projectInputKey = (string?)null,
                expression = (string?)null,
                sourceNodeId = (string?)null,
                sourcePortId = (string?)null
            }
        };

    private static object Connection(string id)
        => new
        {
            id,
            fromNodeId = "node-existing",
            fromPortId = "exec-success",
            toNodeId = "node-existing",
            toPortId = "exec-in",
            kind = "execution",
            branch = (string?)null,
            dataSource = (string?)null,
            priority = 0
        };

    private static object DataConnection(string id, string targetParameterId)
        => new
        {
            id,
            fromNodeId = "node-existing",
            fromPortId = "data-out",
            toNodeId = "node-existing",
            toPortId = targetParameterId,
            kind = "data",
            branch = (string?)null,
            dataSource = (string?)null,
            priority = 0
        };

    private static ConnectionDto DataConnectionDto(string id, string targetParameterId)
        => new(
            id,
            "node-existing",
            "data-out",
            "node-existing",
            targetParameterId,
            ConnectionKindDto.Data,
            null,
            DataSourceDto.PreviousNode,
            0);

    private static object Script(string nodeId)
        => new
        {
            nodeId,
            source = "result = 1",
            languageVersion = "0.1",
            sourceHash = "hash",
            inputs = Array.Empty<object>(),
            outputs = Array.Empty<object>()
        };

    private static FlowDefinitionDto CurrentDefinition()
        => new(
            Guid.NewGuid(),
            5,
            1,
            [
                new CanvasDto(
                    "main",
                    CanvasLifecycleDto.Main,
                    [
                        new NodeDto(
                            "node-existing",
                            NodeTypeDto.Action,
                            "Existing",
                            0,
                            0,
                            [
                                new NodePortDto("exec-in", "Input", "input", false),
                                new NodePortDto("exec-success", "Success", "output", false),
                                new NodePortDto("data-out", "Data output", "output", false),
                                new NodePortDto("param-amount", "amount", "input", false)
                            ],
                            [new NodeParameterDto("amount", "1", DataSourceDto.Literal, true, new NodeParameterUiMetadataDto("amount", "amount", "System.Int32", "1", null, null, null, null))],
                            null)
                    ],
                    [])
            ],
            "node-existing",
            "checksum");
}
