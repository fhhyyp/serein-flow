using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;
public sealed class FlowPatchServiceTests
{
    [Fact]
    public void AddCanvasRequiresAnExplicitId()
    {
        var definition = CreateDefinition();
        var canvasDto = new CanvasDto("", CanvasLifecycleDto.Custom, [], [], "Sub");
        var serializeToElementResult = JsonSerializer.SerializeToElement(canvasDto);
        var operation = Operation(FlowPatchOperationKindDto.AddCanvas, serializeToElementResult);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService = new FlowPatchService();
            var applyResult = flowPatchService.Apply(definition, [operation]);
            return applyResult;
        });
        Assert.Contains("canvasId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddNodeRequiresAnExplicitId()
    {
        var createNodeResult = CreateNode("");
        var serializeToElementResult3 = JsonSerializer.SerializeToElement(createNodeResult);
        var operation = Operation(FlowPatchOperationKindDto.AddNode, serializeToElementResult3, canvasId: "main");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService1 = new FlowPatchService();
            var createDefinitionResult = CreateDefinition();
            var applyResult2 = flowPatchService1.Apply(createDefinitionResult, [operation]);
            return applyResult2;
        });
        Assert.Contains("nodeId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConnectionRequiresAnExplicitId()
    {
        var connection = new ConnectionDto("", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0);
        var serializeToElementResult7 = JsonSerializer.SerializeToElement(connection);
        var operation = Operation(FlowPatchOperationKindDto.AddConnection, serializeToElementResult7, canvasId: "main");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService4 = new FlowPatchService();
            var createDefinitionResult5 = CreateDefinition();
            var applyResult6 = flowPatchService4.Apply(createDefinitionResult5, [operation]);
            return applyResult6;
        });
        Assert.Contains("connectionId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceNodeRequiresAnExplicitMatchingTargetId()
    {
        var createNodeResult11 = CreateNode("node-a");
        var serializeToElementResult12 = JsonSerializer.SerializeToElement(createNodeResult11);
        var operation = Operation(FlowPatchOperationKindDto.ReplaceNode, serializeToElementResult12, canvasId: "main", nodeId: "node-b");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService8 = new FlowPatchService();
            var createDefinitionResult9 = CreateDefinition();
            var applyResult10 = flowPatchService8.Apply(createDefinitionResult9, [operation]);
            return applyResult10;
        });
        Assert.Contains("nodeId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateCanvasRequiresAnExplicitMatchingTargetId()
    {
        var canvasDto16 = new CanvasDto("sub", CanvasLifecycleDto.Custom, [], [], "Sub");
        var serializeToElementResult17 = JsonSerializer.SerializeToElement(canvasDto16);
        var operation = Operation(FlowPatchOperationKindDto.UpdateCanvas, serializeToElementResult17, canvasId: "main");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService13 = new FlowPatchService();
            var createDefinitionResult14 = CreateDefinition();
            var applyResult15 = flowPatchService13.Apply(createDefinitionResult14, [operation]);
            return applyResult15;
        });
        Assert.Contains("canvasId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceConnectionRequiresAnExplicitMatchingTargetId()
    {
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto()]
        };
        var connectionDto = new ConnectionDto("edge-a", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0);
        var serializeToElementResult22 = JsonSerializer.SerializeToElement(connectionDto);
        var operation = new FlowPatchOperationDto(FlowPatchOperationKindDto.ReplaceConnection, CanvasId: "main", ConnectionId: "edge-b", Value: serializeToElementResult22);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService18 = new FlowPatchService();
            var applyResult19 = flowPatchService18.Apply(definition, [operation]);
            return applyResult19;
        });
        Assert.Contains("connectionId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue = [CreateNode("node-a"), CreateNode("node-b")];
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ConnectionDto> evaluatedValue20 = [new ConnectionDto("edge-a", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0)];
            var canvasDto21 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue, evaluatedValue20);
            return canvasDto21;
        }
    }

    [Fact]
    public void SetNodeParameterRequiresMatchingParameterId()
    {
        var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("different", "amount", "number", "2", null, null, null, null);
        var parameter = new NodeParameterDto("amount", "2", DataSourceDto.Literal, true, nodeParameterUiMetadataDto);
        var serializeToElementResult26 = JsonSerializer.SerializeToElement(parameter);
        var operation = Operation(FlowPatchOperationKindDto.SetNodeParameter, serializeToElementResult26, canvasId: "main", nodeId: "node-a", parameterId: "amount");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService23 = new FlowPatchService();
            var createDefinitionResult24 = CreateDefinition();
            var applyResult25 = flowPatchService23.Apply(createDefinitionResult24, [operation]);
            return applyResult25;
        });
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAndRemoveNodeParameterMutateOneParameterWithoutReplacingTheNode()
    {
        var addedParameter = Parameter("value-2");
        var flowPatchService27 = new FlowPatchService();
        var createDefinitionResult28 = CreateDefinition();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue30 = [resolveOperationResult()];
        var added = flowPatchService27.Apply(createDefinitionResult28, evaluatedValue30);
        var evaluatedValue31 = Assert.Single(added.Canvases).Nodes;
        var addedNode = evaluatedValue31.Single();
        var selectResult = addedNode.Parameters.Select(parameter => parameter.Ui!.Id);
        Assert.Equal(["value", "value-2"], selectResult);
        var flowPatchService32 = new FlowPatchService();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue33 = [new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNodeParameter, CanvasId: "main", NodeId: "node-a", ParameterId: "value-2")];
        var removed = flowPatchService32.Apply(added, evaluatedValue33);
        var evaluatedValue34 = Assert.Single(removed.Canvases).Nodes;
        var evaluatedValue35 = evaluatedValue34.Single().Parameters;
        var selectResult36 = evaluatedValue35.Select(parameter => parameter.Ui!.Id);
        Assert.Equal(["value"], selectResult36);
        global::SereinFlow.Contracts.FlowPatchOperationDto resolveOperationResult()
        {
            var serializeToElementResult29 = JsonSerializer.SerializeToElement(addedParameter);
            var operationResult = Operation(FlowPatchOperationKindDto.AddNodeParameter, serializeToElementResult29, canvasId: "main", nodeId: "node-a");
            return operationResult;
        }
    }

    [Fact]
    public void RemoveNodeParameterRequiresIncomingDataConnectionsRemovedFirst()
    {
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto39()]
        };
        var operation = new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNodeParameter, CanvasId: "main", NodeId: "node-a", ParameterId: "value");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService37 = new FlowPatchService();
            var applyResult38 = flowPatchService37.Apply(definition, [operation]);
            return applyResult38;
        });
        Assert.Contains("Data connections", exception.Message, StringComparison.Ordinal);
        Assert.Contains("must be removed first", exception.Message, StringComparison.Ordinal);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto39()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue40 = [CreateNode("node-a"), CreateNode("source")];
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ConnectionDto> evaluatedValue41 = [new ConnectionDto("data", "source", "data-out", "node-a", "value", ConnectionKindDto.Data, null, DataSourceDto.PreviousNode, 0)];
            var canvasDto42 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue40, evaluatedValue41);
            return canvasDto42;
        }
    }

    [Fact]
    public void NewCanvasRejectsDuplicateNodeConnectionAndParameterIds()
    {
        var node = CreateNode("node-a");
        var duplicateNode = node with
        {
            Parameters = [Parameter("value"), Parameter("value")]
        };
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ConnectionDto> evaluatedValue46 = [new ConnectionDto("edge", "node-a", "out", "node-a", "in", ConnectionKindDto.Execution, null, null, 0), new ConnectionDto("edge", "node-a", "out", "node-a", "in", ConnectionKindDto.Execution, null, null, 0)];
        var canvas = new CanvasDto("sub", CanvasLifecycleDto.Custom, [node, duplicateNode], evaluatedValue46, "Sub");
        var serializeToElementResult47 = JsonSerializer.SerializeToElement(canvas);
        var operation = Operation(FlowPatchOperationKindDto.AddCanvas, serializeToElementResult47);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService43 = new FlowPatchService();
            var createDefinitionResult44 = CreateDefinition();
            var applyResult45 = flowPatchService43.Apply(createDefinitionResult44, [operation]);
            return applyResult45;
        });
        Assert.Contains("duplicated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeCannotBeRemovedBeforeItsConnections()
    {
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto50()]
        };
        var operation = new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNode, CanvasId: "main", NodeId: "node-a");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var flowPatchService48 = new FlowPatchService();
            var applyResult49 = flowPatchService48.Apply(definition, [operation]);
            return applyResult49;
        });
        Assert.Contains("Connections", exception.Message, StringComparison.Ordinal);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto50()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue51 = [CreateNode("node-a"), CreateNode("node-b")];
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ConnectionDto> evaluatedValue52 = [new ConnectionDto("edge", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0)];
            var canvasDto53 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue51, evaluatedValue52, "Main");
            return canvasDto53;
        }
    }

    [Fact]
    public void RemovingTheEntryNodeClearsTheEntryForAnEmptyDraft()
    {
        var flowPatchService54 = new FlowPatchService();
        var createDefinitionResult55 = CreateDefinition();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue56 = [new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNode, CanvasId: "main", NodeId: "node-a")];
        var result = flowPatchService54.Apply(createDefinitionResult55, evaluatedValue56);
        Assert.Empty(result.EntryNodeId);
        var evaluatedValue57 = Assert.Single(result.Canvases).Nodes;
        Assert.Empty(evaluatedValue57);
    }

    [Fact]
    public void RemovingTheEntryNodeClearsTheEntryWhenOtherNodesRemain()
    {
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto58()]
        };
        var flowPatchService61 = new FlowPatchService();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue62 = [new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNode, CanvasId: "main", NodeId: "node-a")];
        var result = flowPatchService61.Apply(definition, evaluatedValue62);
        Assert.Empty(result.EntryNodeId);
        var evaluatedValue63 = Assert.Single(result.Canvases).Nodes;
        var evaluatedValue64 = Assert.Single(evaluatedValue63).Id;
        Assert.Equal("node-b", evaluatedValue64);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto58()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue59 = [CreateNode("node-a"), CreateNode("node-b")];
            var canvasDto60 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue59, [], "Main");
            return canvasDto60;
        }
    }

    [Fact]
    public void RemovingAllNodesAfterTheirConnectionsClearsTheEntry()
    {
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto65()]
        };
        var flowPatchService69 = new FlowPatchService();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue70 = [new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveConnection, CanvasId: "main", ConnectionId: "edge"), new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNode, CanvasId: "main", NodeId: "node-a"), new FlowPatchOperationDto(FlowPatchOperationKindDto.RemoveNode, CanvasId: "main", NodeId: "node-b")];
        var result = flowPatchService69.Apply(definition, evaluatedValue70);
        Assert.Empty(result.EntryNodeId);
        var canvas = Assert.Single(result.Canvases);
        Assert.Empty(canvas.Nodes);
        Assert.Empty(canvas.Connections);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto65()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue66 = [CreateNode("node-a"), CreateNode("node-b")];
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ConnectionDto> evaluatedValue67 = [new ConnectionDto("edge", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0)];
            var canvasDto68 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue66, evaluatedValue67, "Main");
            return canvasDto68;
        }
    }

    [Fact]
    public void NestedDtoEnumsAcceptCanonicalCamelCaseStrings()
    {
        using var document = JsonDocument.Parse("{\"concurrencyMode\":\"exclusiveReject\"}");
        var cloneResult = document.RootElement.Clone();
        var operation = new FlowPatchOperationDto(FlowPatchOperationKindDto.SetRunPolicy, Value: cloneResult);
        var flowPatchService71 = new FlowPatchService();
        var createDefinitionResult72 = CreateDefinition();
        var result = flowPatchService71.Apply(createDefinitionResult72, [operation]);
        Assert.Equal(FlowConcurrencyModeDto.ExclusiveReject, result.RunPolicy!.ConcurrencyMode);
    }

    [Fact]
    public void DataConnectionSynchronizesTargetParameterSourceAndMetadata()
    {
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto73()]
        };
        var connection = new ConnectionDto("data", "source", "data-out", "target", "value", ConnectionKindDto.Data, null, null, 0);
        var flowPatchService76 = new FlowPatchService();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue80 = [resolveOperationResult77()];
        var result = flowPatchService76.Apply(definition, evaluatedValue80);
        var canvas = Assert.Single(result.Canvases);
        var target = canvas.Nodes.Single(node => node.Id == "target");
        var parameter = Assert.Single(target.Parameters);
        var savedConnection = Assert.Single(canvas.Connections);
        Assert.Equal(DataSourceDto.PreviousNode, parameter.Source);
        Assert.Equal("source", parameter.Ui!.SourceNodeId);
        Assert.Equal("data-out", parameter.Ui.SourcePortId);
        Assert.Equal(DataSourceDto.PreviousNode, savedConnection.DataSource);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto73()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue74 = [CreateNode("source")with
            {
                Ports = [new NodePortDto("data-out", "Data", "output", false)],
            }, CreateNode("target"), ];
            var canvasDto75 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue74, [], "Main");
            return canvasDto75;
        }

        global::SereinFlow.Contracts.FlowPatchOperationDto resolveOperationResult77()
        {
            var serializeToElementResult78 = JsonSerializer.SerializeToElement(connection);
            var operationResult79 = Operation(FlowPatchOperationKindDto.AddConnection, serializeToElementResult78, canvasId: "main");
            return operationResult79;
        }
    }

    [Fact]
    public void RemovingDataConnectionRestoresItsLiteralFallback()
    {
        var targetUi = Parameter("value").Ui!with
        {
            SourceNodeId = "source",
            SourcePortId = "data-out",
        };
        var targetParameter = Parameter("value")with
        {
            Source = DataSourceDto.PreviousNode,
            Ui = targetUi,
        };
        var definition = CreateDefinition()with
        {
            Canvases = [resolveCanvasDto81()]
        };
        var flowPatchService85 = new FlowPatchService();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.FlowPatchOperationDto> evaluatedValue89 = [resolveOperationResult86()];
        var result = flowPatchService85.Apply(definition, evaluatedValue89);
        var canvas = Assert.Single(result.Canvases);
        var evaluatedValue90 = canvas.Nodes.Single(node => node.Id == "target").Parameters;
        var parameter = Assert.Single(evaluatedValue90);
        Assert.Equal(DataSourceDto.Literal, parameter.Source);
        Assert.Null(parameter.Ui!.SourceNodeId);
        Assert.Null(parameter.Ui.SourcePortId);
        Assert.Empty(canvas.Connections);
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto81()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue82 = [CreateNode("source")with
            {
                Ports = [new NodePortDto("data-out", "Data", "output", false)],
            }, CreateNode("target")with
            {
                Parameters = [targetParameter],
            }, ];
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.ConnectionDto> evaluatedValue83 = [new ConnectionDto("data", "source", "data-out", "target", "value", ConnectionKindDto.Data, null, DataSourceDto.PreviousNode, 0)];
            var canvasDto84 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue82, evaluatedValue83, "Main");
            return canvasDto84;
        }

        global::SereinFlow.Contracts.FlowPatchOperationDto resolveOperationResult86()
        {
            var serializeToElementResult87 = JsonSerializer.SerializeToElement("data");
            var operationResult88 = Operation(FlowPatchOperationKindDto.RemoveConnection, serializeToElementResult87, canvasId: "main", connectionId: "data");
            return operationResult88;
        }
    }

    private static FlowPatchOperationDto Operation(FlowPatchOperationKindDto kind, JsonElement value, string? canvasId = null, string? nodeId = null, string? parameterId = null, string? connectionId = null)
    {
        global::SereinFlow.Contracts.FlowPatchOperationDto evaluatedValue91 = new(kind, canvasId, nodeId, ConnectionId: connectionId, ParameterId: parameterId, Value: value);
        return evaluatedValue91;
    }

    private static FlowDefinitionDto CreateDefinition()
    {
        var newGuidResult = Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.CanvasDto> evaluatedValue95 = [resolveCanvasDto92()];
        global::SereinFlow.Contracts.FlowDefinitionDto evaluatedValue96 = new(newGuidResult, 1, 1, evaluatedValue95, "node-a", "checksum");
        return evaluatedValue96;
        global::SereinFlow.Contracts.CanvasDto resolveCanvasDto92()
        {
            global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeDto> evaluatedValue93 = [CreateNode("node-a")];
            var canvasDto94 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue93, [], "Main");
            return canvasDto94;
        }
    }

    private static NodeDto CreateNode(string id)
    {
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.NodeParameterDto> evaluatedValue97 = [Parameter("value")];
        global::SereinFlow.Contracts.NodeDto evaluatedValue98 = new(id, NodeTypeDto.Action, "Action", 0, 0, [], evaluatedValue97, null);
        return evaluatedValue98;
    }

    private static NodeParameterDto Parameter(string id)
    {
        var nodeParameterUiMetadataDto99 = new NodeParameterUiMetadataDto(id, id, "number", "1", null, null, null, null);
        global::SereinFlow.Contracts.NodeParameterDto evaluatedValue100 = new(id, "1", DataSourceDto.Literal, true, nodeParameterUiMetadataDto99);
        return evaluatedValue100;
    }
}
