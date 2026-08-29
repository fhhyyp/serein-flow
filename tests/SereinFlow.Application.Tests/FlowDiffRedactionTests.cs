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
        var node = Assert.Single(Assert.Single(redacted.Canvases).Nodes);
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
        var after = CreateDefinition("new-secret-source", "new-secret-value") with { Id = before.Id, Version = before.Version };
        var diff = new FlowDiffService().Compare(before, after);

        var redacted = FlowDiffService.RedactSensitive(diff);
        var serialized = JsonSerializer.Serialize(redacted);

        Assert.DoesNotContain("old-secret-source", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret-source", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("old-secret-value", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret-value", serialized, StringComparison.Ordinal);
        Assert.Contains("isSensitive", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChecksumIgnoresVersionHistoryMetadata()
    {
        var definition = CreateDefinition("source", "value");

        Assert.Equal(
            FlowDiffService.GetChecksum(definition),
            FlowDiffService.GetChecksum(definition with { Version = definition.Version + 1 }));
    }

    private static FlowDefinitionDto CreateDefinition(string source, string value)
    {
        var node = new NodeDto(
            "node",
            NodeTypeDto.Script,
            "Script",
            0,
            0,
            [],
            [new NodeParameterDto(
                "value",
                JsonSerializer.Serialize(value),
                DataSourceDto.Literal,
                true,
                new NodeParameterUiMetadataDto("value", "value", "string", value, "secret-input", "secret-expression", null, null))],
            new ScriptNodeDataDto("node", source, "1", "hash", [], []));
        return new FlowDefinitionDto(Guid.NewGuid(), 1, 1, [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [], "Main")], "node", "checksum");
    }
}
