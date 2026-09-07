using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;

public sealed class BuiltinNodeTemplateServiceTests
{
    [Fact]
    public void FlowCallTemplateIsACompleteSchema20PatchNode()
    {
        var template = new BuiltinNodeTemplateService(new BuiltinNodeCatalog()).Create(
            new BuiltinNodeTemplateRequestDto(
                Guid.NewGuid(),
                "builtin:flow-call",
                new NodeTemplatePositionDto(320, 180)));

        var node = template.Node;
        Assert.Equal("builtinCatalog", template.TemplateSource);
        Assert.Equal(NodeTypeDto.FlowCall, node.Type);
        Assert.StartsWith("node-", node.Id);
        Assert.Equal(320, node.X);
        Assert.Equal(180, node.Y);
        Assert.Equal("System.Object", node.Ui?.ReturnType);
        Assert.Null(node.Ui?.FlowCallParameterBindings);
        Assert.Contains(node.Ports, port => port.Id == "exec-in");
        Assert.Contains(node.Ports, port => port.Id == "data-out");

        var request = JsonSerializer.SerializeToElement(new
        {
            op = "addNode",
            canvasId = "main",
            node
        }, SereinJsonSerialization.CreateContractOptions());

        var normalized = new FlowPatchContractNormalizer().Normalize(JsonSerializer.SerializeToElement(new
        {
            projectId = Guid.NewGuid(),
            flowId = Guid.NewGuid(),
            expectedDevelopmentVersion = 1,
            schemaVersion = "2.0",
            operations = new[] { request }
        }, SereinJsonSerialization.CreateContractOptions()));

        Assert.Equal("addNode", Assert.Single(normalized.Request.Operations).Op);
    }
}
