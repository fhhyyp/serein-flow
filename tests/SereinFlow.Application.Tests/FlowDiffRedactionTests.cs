using System.Collections.Generic;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;
public sealed class FlowDiffRedactionTests
{
    [Fact]
    public void RedactsFlowDefinitionSensitiveFields()
    {
        var definition = CreateDefinition("secret-source", "secret-value");
        var redacted = FlowDiffService.RedactSensitive(definition);
        var evaluatedValue = Assert.Single(redacted.Canvases).Nodes;
        var node = Assert.Single(evaluatedValue);
        var parameter = Assert.Single(node.Parameters);
        Assert.Null(parameter.ValueJson);
        Assert.Null(parameter.Ui!.LiteralValue);
        Assert.Null(parameter.Ui.ProjectInputKey);
        Assert.Null(parameter.Ui.Expression);
        Assert.Equal("[redacted]", node.Script!.Source);
    }

    [Fact]
    public void RedactsSensitiveFieldsInsideDiffJson()
    {
        var before = CreateDefinition("old-secret-source", "old-secret-value");
        var after = CreateDefinition("new-secret-source", "new-secret-value")with
        {
            Id = before.Id,
            Version = before.Version
        };
        var flowDiffService = new FlowDiffService();
        var diff = flowDiffService.Compare(before, after);
        var redacted = FlowDiffService.RedactSensitive(diff);
        var serialized = JsonSerializer.Serialize(redacted);
        Assert.DoesNotContain("old-secret-source", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret-source", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("old-secret-value", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret-value", serialized, StringComparison.Ordinal);
        Assert.Contains("isSensitive", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreservesScalarDiffValuesWhileRedactingStructuredPayloads()
    {
        var before = CreateDefinition("source", "value")with
        {
            EntryNodeId = "old-node"
        };
        var after = before with
        {
            EntryNodeId = "node",
            Canvases = [before.Canvases[0] with
            {
                Name = "Renamed canvas"
            }

            ],
            Version = before.Version,
        };
        var flowDiffService1 = new FlowDiffService();
        var diff = flowDiffService1.Compare(before, after);
        var redacted = FlowDiffService.RedactSensitive(diff);
        var entryChange = Assert.Single(redacted.Changes, change => change.Path == "entryNodeId");
        Assert.Equal("old-node", entryChange.Before);
        Assert.Equal("node", entryChange.After);
        var canvasNameChange = Assert.Single(redacted.Changes, change => change.Path == "canvases.main.name");
        Assert.Equal("Main", canvasNameChange.Before);
        Assert.Equal("Renamed canvas", canvasNameChange.After);
    }

    [Fact]
    public void ChecksumIgnoresVersionHistoryMetadata()
    {
        var definition = CreateDefinition("source", "value");
        var getChecksumResult = FlowDiffService.GetChecksum(definition);
        var getChecksumResult2 = FlowDiffService.GetChecksum(definition with { Version = definition.Version + 1 });
        Assert.Equal(getChecksumResult, getChecksumResult2);
    }

    private static FlowDefinitionDto CreateDefinition(string source, string value)
    {
        IReadOnlyList<NodeParameterDto> evaluatedValue3 = [resolveNodeParameterDto()];
        var scriptNodeDataDto = new ScriptNodeDataDto("node", source, "1", "hash", [], []);
        var node = new NodeDto("node", NodeTypeDto.Script, "Script", 0, 0, [], evaluatedValue3, scriptNodeDataDto);
        var newGuidResult = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue4 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [], "Main")];
        var flowDefinitionDto = new FlowDefinitionDto(newGuidResult, 1, 1, evaluatedValue4, "node", "checksum");
        return flowDefinitionDto;
        NodeParameterDto resolveNodeParameterDto()
        {
            var serializeResult = JsonSerializer.Serialize(value);
            var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("value", "value", "string", value, "secret-input", "secret-expression", null, null);
            var nodeParameterDto = new NodeParameterDto("value", serializeResult, DataSourceDto.Literal, true, nodeParameterUiMetadataDto);
            return nodeParameterDto;
        }
    }
}
