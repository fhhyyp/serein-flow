using SereinFlow.Domain;

namespace SereinFlow.Domain.Tests;

public sealed class FlowDefinitionTests
{
    [Fact]
    public void ValidFlowAcceptsExecutionAndDataConnections()
    {
        var start = NodeDefinition.Create("start", NodeType.Action, "Start");
        var finish = NodeDefinition.Create(
            "finish",
            NodeType.Action,
            "Finish",
            parameters: [new NodeParameterDefinition("input", null, id: "input")]);
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [start, finish],
        [
            ConnectionDefinition.Execution("start", "success", "finish", "in", ExecutionBranch.Success),
            ConnectionDefinition.Data("start", "result", "finish", "input", DataSource.PreviousNode)
        ]);

        var flow = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [canvas],
            "start");

        var diagnostics = flow.Validate();

        Assert.Empty(diagnostics);
        Assert.Equal("start", flow.EntryNodeId);
    }

    [Fact]
    public void EmptyEditorFlowIsValidUntilItIsSentForExecution()
    {
        var flow = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [], [])],
            "");

        var diagnostics = flow.Validate();

        Assert.Empty(diagnostics);
        Assert.Equal(string.Empty, flow.EntryNodeId);
    }

    [Fact]
    public void FlowRejectsDuplicateNodeIds()
    {
        var duplicate = NodeDefinition.Create("same", NodeType.Action, "Duplicate");
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [duplicate, duplicate], []);

        var flow = FlowDefinition.Create(Guid.NewGuid(), 1, [canvas], "same");

        var diagnostics = flow.Validate();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.DuplicateNodeId);
    }

    [Fact]
    public void FlowRejectsConnectionsToUnknownNodes()
    {
        var node = NodeDefinition.Create("start", NodeType.Action, "Start");
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [node],
        [ConnectionDefinition.Execution("start", "success", "missing", "in", ExecutionBranch.Success)]);

        var flow = FlowDefinition.Create(Guid.NewGuid(), 1, [canvas], "start");

        var diagnostics = flow.Validate();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.UnknownConnectionEndpoint);
    }

    [Fact]
    public void ScriptNodeHashIsDeterministicAndRejectsInvalidSourceHash()
    {
        const string source = "return 42;";
        var script = ScriptNodeDefinition.Create("script", source, "1");

        Assert.Equal(ScriptNodeDefinition.ComputeSourceHash(source), script.SourceHash);
        Assert.Throws<ArgumentException>(() => ScriptNodeDefinition.Create("script", source, "1", "invalid"));
    }

    [Fact]
    public void FlowRejectsDuplicateAndMissingRequiredParameters()
    {
        var node = NodeDefinition.Create(
            "action",
            NodeType.Action,
            "Action",
            parameters:
            [
                new NodeParameterDefinition("input", null, required: true),
                new NodeParameterDefinition("input", "42")
            ]);
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], []);

        var diagnostics = FlowDefinition.Create(Guid.NewGuid(), 1, [canvas], "action").Validate();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.DuplicateParameterName);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.MissingRequiredParameter);
    }

    [Fact]
    public void LegacySchemaIsRejectedInsteadOfRemappingRetiredNodeValues()
    {
        var flow = FlowDefinition.Create(
            Guid.NewGuid(),
            1,
            [CanvasDefinition.Create("main", CanvasLifecycle.Main, [NodeDefinition.Create("node", NodeType.Action, "Node")], [])],
            "node",
            schemaVersion: 1);

        var diagnostic = Assert.Single(flow.Validate(), item => item.Code == DomainErrorCodes.NodeTypeRemoved);

        Assert.Equal("schemaVersion", diagnostic.Path);
    }
}
