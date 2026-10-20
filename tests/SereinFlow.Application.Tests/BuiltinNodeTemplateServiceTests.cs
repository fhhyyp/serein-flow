using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;
public sealed class BuiltinNodeTemplateServiceTests
{
    [Fact]
    public void FlowCallTemplateIsACompleteSchema20PatchNode()
    {
        var builtinNodeCatalog = new BuiltinNodeCatalog();
        var builtinNodeTemplateService = new BuiltinNodeTemplateService(builtinNodeCatalog);
        var newGuidResult = Guid.NewGuid();
        var nodeTemplatePositionDto = new NodeTemplatePositionDto(320, 180);
        var builtinNodeTemplateRequestDto = new BuiltinNodeTemplateRequestDto(newGuidResult, "builtin:flow-call", nodeTemplatePositionDto);
        var template = builtinNodeTemplateService.Create(builtinNodeTemplateRequestDto);
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
        var createContractOptionsResult = SereinJsonSerialization.CreateContractOptions();
        var request = JsonSerializer.SerializeToElement(new { op = "addNode", canvasId = "main", node }, createContractOptionsResult);
        var flowPatchContractNormalizer = new FlowPatchContractNormalizer();
        var newGuidResult1 = Guid.NewGuid();
        var newGuidResult2 = Guid.NewGuid();
        var evaluatedValue = new
        {
            projectId = newGuidResult1,
            flowId = newGuidResult2,
            expectedDevelopmentVersion = 1,
            schemaVersion = "2.0",
            operations = new[]
            {
                request
            }
        };
        var createContractOptionsResult3 = SereinJsonSerialization.CreateContractOptions();
        var serializeToElementResult = JsonSerializer.SerializeToElement(evaluatedValue, createContractOptionsResult3);
        var normalized = flowPatchContractNormalizer.Normalize(serializeToElementResult);
        var evaluatedValue4 = Assert.Single(normalized.Request.Operations).Op;
        Assert.Equal("addNode", evaluatedValue4);
    }
}
