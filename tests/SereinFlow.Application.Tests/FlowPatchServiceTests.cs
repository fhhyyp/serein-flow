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
        var operation = Operation(
            FlowPatchOperationKindDto.AddCanvas,
            JsonSerializer.SerializeToElement(new CanvasDto("", CanvasLifecycleDto.Custom, [], [], "Sub")));

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(definition, [operation]));

        Assert.Contains("canvasId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddNodeRequiresAnExplicitId()
    {
        var operation = Operation(
            FlowPatchOperationKindDto.AddNode,
            JsonSerializer.SerializeToElement(CreateNode("")),
            canvasId: "main");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(CreateDefinition(), [operation]));

        Assert.Contains("nodeId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConnectionRequiresAnExplicitId()
    {
        var connection = new ConnectionDto("", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0);
        var operation = Operation(
            FlowPatchOperationKindDto.AddConnection,
            JsonSerializer.SerializeToElement(connection),
            canvasId: "main");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(CreateDefinition(), [operation]));

        Assert.Contains("connectionId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceNodeRequiresAnExplicitMatchingTargetId()
    {
        var operation = Operation(
            FlowPatchOperationKindDto.ReplaceNode,
            JsonSerializer.SerializeToElement(CreateNode("node-a")),
            canvasId: "main",
            nodeId: "node-b");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(CreateDefinition(), [operation]));

        Assert.Contains("nodeId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateCanvasRequiresAnExplicitMatchingTargetId()
    {
        var operation = Operation(
            FlowPatchOperationKindDto.UpdateCanvas,
            JsonSerializer.SerializeToElement(new CanvasDto("sub", CanvasLifecycleDto.Custom, [], [], "Sub")),
            canvasId: "main");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(CreateDefinition(), [operation]));

        Assert.Contains("canvasId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceConnectionRequiresAnExplicitMatchingTargetId()
    {
        var definition = CreateDefinition() with
        {
            Canvases =
            [
                new CanvasDto(
                    "main",
                    CanvasLifecycleDto.Main,
                    [CreateNode("node-a"), CreateNode("node-b")],
                    [new ConnectionDto("edge-a", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0)])
            ]
        };
        var operation = new FlowPatchOperationDto(
            FlowPatchOperationKindDto.ReplaceConnection,
            CanvasId: "main",
            ConnectionId: "edge-b",
            Value: JsonSerializer.SerializeToElement(
                new ConnectionDto("edge-a", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0)));

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(definition, [operation]));

        Assert.Contains("connectionId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetNodeParameterRequiresMatchingParameterId()
    {
        var parameter = new NodeParameterDto(
            "amount",
            "2",
            DataSourceDto.Literal,
            true,
            new NodeParameterUiMetadataDto("different", "amount", "number", "2", null, null, null, null));
        var operation = Operation(
            FlowPatchOperationKindDto.SetNodeParameter,
            JsonSerializer.SerializeToElement(parameter),
            canvasId: "main",
            nodeId: "node-a",
            parameterId: "amount");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(CreateDefinition(), [operation]));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAndRemoveNodeParameterMutateOneParameterWithoutReplacingTheNode()
    {
        var addedParameter = Parameter("value-2");
        var added = new FlowPatchService().Apply(
            CreateDefinition(),
            [Operation(
                FlowPatchOperationKindDto.AddNodeParameter,
                JsonSerializer.SerializeToElement(addedParameter),
                canvasId: "main",
                nodeId: "node-a")]);

        var addedNode = Assert.Single(added.Canvases).Nodes.Single();
        Assert.Equal(["value", "value-2"], addedNode.Parameters.Select(parameter => parameter.Ui!.Id));

        var removed = new FlowPatchService().Apply(
            added,
            [new FlowPatchOperationDto(
                FlowPatchOperationKindDto.RemoveNodeParameter,
                CanvasId: "main",
                NodeId: "node-a",
                ParameterId: "value-2")]);

        Assert.Equal(["value"], Assert.Single(removed.Canvases).Nodes.Single().Parameters.Select(parameter => parameter.Ui!.Id));
    }

    [Fact]
    public void RemoveNodeParameterRequiresIncomingDataConnectionsRemovedFirst()
    {
        var definition = CreateDefinition() with
        {
            Canvases =
            [
                new CanvasDto(
                    "main",
                    CanvasLifecycleDto.Main,
                    [CreateNode("node-a"), CreateNode("source")],
                    [new ConnectionDto("data", "source", "data-out", "node-a", "value", ConnectionKindDto.Data, null, DataSourceDto.PreviousNode, 0)])
            ]
        };
        var operation = new FlowPatchOperationDto(
            FlowPatchOperationKindDto.RemoveNodeParameter,
            CanvasId: "main",
            NodeId: "node-a",
            ParameterId: "value");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(definition, [operation]));

        Assert.Contains("Data connections", exception.Message, StringComparison.Ordinal);
        Assert.Contains("must be removed first", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NewCanvasRejectsDuplicateNodeConnectionAndParameterIds()
    {
        var node = CreateNode("node-a");
        var duplicateNode = node with { Parameters = [Parameter("value"), Parameter("value")] };
        var canvas = new CanvasDto(
            "sub",
            CanvasLifecycleDto.Custom,
            [node, duplicateNode],
            [new ConnectionDto("edge", "node-a", "out", "node-a", "in", ConnectionKindDto.Execution, null, null, 0),
             new ConnectionDto("edge", "node-a", "out", "node-a", "in", ConnectionKindDto.Execution, null, null, 0)],
            "Sub");
        var operation = Operation(FlowPatchOperationKindDto.AddCanvas, JsonSerializer.SerializeToElement(canvas));

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(CreateDefinition(), [operation]));

        Assert.Contains("duplicated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeCannotBeRemovedBeforeItsConnections()
    {
        var definition = CreateDefinition() with
        {
            Canvases =
            [
                new CanvasDto(
                    "main",
                    CanvasLifecycleDto.Main,
                    [CreateNode("node-a"), CreateNode("node-b")],
                    [new ConnectionDto("edge", "node-a", "out", "node-b", "in", ConnectionKindDto.Execution, null, null, 0)],
                    "Main")
            ]
        };
        var operation = new FlowPatchOperationDto(
            FlowPatchOperationKindDto.RemoveNode,
            CanvasId: "main",
            NodeId: "node-a");

        var exception = Assert.Throws<InvalidOperationException>(() => new FlowPatchService().Apply(definition, [operation]));

        Assert.Contains("Connections", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedDtoEnumsAcceptCanonicalCamelCaseStrings()
    {
        using var document = JsonDocument.Parse("{\"concurrencyMode\":\"exclusiveReject\"}");
        var operation = new FlowPatchOperationDto(
            FlowPatchOperationKindDto.SetRunPolicy,
            Value: document.RootElement.Clone());

        var result = new FlowPatchService().Apply(CreateDefinition(), [operation]);

        Assert.Equal(FlowConcurrencyModeDto.ExclusiveReject, result.RunPolicy!.ConcurrencyMode);
    }

    private static FlowPatchOperationDto Operation(
        FlowPatchOperationKindDto kind,
        JsonElement value,
        string? canvasId = null,
        string? nodeId = null,
        string? parameterId = null,
        string? connectionId = null)
        => new(kind, canvasId, nodeId, ConnectionId: connectionId, ParameterId: parameterId, Value: value);

    private static FlowDefinitionDto CreateDefinition()
        => new(
            Guid.NewGuid(),
            1,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [CreateNode("node-a")], [], "Main")],
            "node-a",
            "checksum");

    private static NodeDto CreateNode(string id)
        => new(id, NodeTypeDto.Action, "Action", 0, 0, [], [Parameter("value")], null);

    private static NodeParameterDto Parameter(string id)
        => new(
            id,
            "1",
            DataSourceDto.Literal,
            true,
            new NodeParameterUiMetadataDto(id, id, "number", "1", null, null, null, null));
}
