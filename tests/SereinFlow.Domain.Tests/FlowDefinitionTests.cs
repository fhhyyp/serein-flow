using SereinFlow.Domain;

namespace SereinFlow.Domain.Tests;
public sealed class FlowDefinitionTests
{
    [Fact]
    public void ValidFlowAcceptsExecutionAndDataConnections()
    {
        var start = NodeDefinition.Create("start", NodeType.Action, "Start");
        global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.NodeParameterDefinition> evaluatedValue = [new NodeParameterDefinition("input", null, id: "input")];
        var finish = NodeDefinition.Create("finish", NodeType.Action, "Finish", parameters: evaluatedValue);
        global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.ConnectionDefinition> evaluatedValue1 = [ConnectionDefinition.Execution("start", "success", "finish", "in", ExecutionBranch.Success), ConnectionDefinition.Data("start", "result", "finish", "input", DataSource.PreviousNode)];
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [start, finish], evaluatedValue1);
        var newGuidResult = Guid.NewGuid();
        var flow = FlowDefinition.Create(newGuidResult, 1, [canvas], "start");
        var diagnostics = flow.Validate();
        Assert.Empty(diagnostics);
        Assert.Equal("start", flow.EntryNodeId);
    }

    [Fact]
    public void EmptyEditorFlowIsValidUntilItIsSentForExecution()
    {
        var newGuidResult2 = Guid.NewGuid();
        global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.CanvasDefinition> evaluatedValue3 = [CanvasDefinition.Create("main", CanvasLifecycle.Main, [], [])];
        var flow = FlowDefinition.Create(newGuidResult2, 1, evaluatedValue3, "");
        var diagnostics = flow.Validate();
        Assert.Empty(diagnostics);
        Assert.Equal(string.Empty, flow.EntryNodeId);
    }

    [Fact]
    public void FlowRejectsDuplicateNodeIds()
    {
        var duplicate = NodeDefinition.Create("same", NodeType.Action, "Duplicate");
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [duplicate, duplicate], []);
        var newGuidResult4 = Guid.NewGuid();
        var flow = FlowDefinition.Create(newGuidResult4, 1, [canvas], "same");
        var diagnostics = flow.Validate();
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.DuplicateNodeId);
    }

    [Fact]
    public void FlowRejectsConnectionsToUnknownNodes()
    {
        var node = NodeDefinition.Create("start", NodeType.Action, "Start");
        global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.ConnectionDefinition> evaluatedValue5 = [ConnectionDefinition.Execution("start", "success", "missing", "in", ExecutionBranch.Success)];
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], evaluatedValue5);
        var newGuidResult6 = Guid.NewGuid();
        var flow = FlowDefinition.Create(newGuidResult6, 1, [canvas], "start");
        var diagnostics = flow.Validate();
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.UnknownConnectionEndpoint);
    }

    [Fact]
    public void ScriptNodeHashIsDeterministicAndRejectsInvalidSourceHash()
    {
        const string source = "return 42;";
        var script = ScriptNodeDefinition.Create("script", source, "1");
        var computeSourceHashResult = ScriptNodeDefinition.ComputeSourceHash(source);
        Assert.Equal(computeSourceHashResult, script.SourceHash);
        Assert.Throws<ArgumentException>(() =>
        {
            var createResult = ScriptNodeDefinition.Create("script", source, "1", "invalid");
            return createResult;
        });
    }

    [Fact]
    public void FlowRejectsDuplicateAndMissingRequiredParameters()
    {
        global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.NodeParameterDefinition> evaluatedValue7 = [new NodeParameterDefinition("input", null, required: true), new NodeParameterDefinition("input", "42")];
        var node = NodeDefinition.Create("action", NodeType.Action, "Action", parameters: evaluatedValue7);
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], []);
        var newGuidResult8 = Guid.NewGuid();
        var createResult9 = FlowDefinition.Create(newGuidResult8, 1, [canvas], "action");
        var diagnostics = createResult9.Validate();
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.DuplicateParameterName);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DomainErrorCodes.MissingRequiredParameter);
    }

    [Fact]
    public void LegacySchemaIsRejectedInsteadOfRemappingRetiredNodeValues()
    {
        var newGuidResult10 = Guid.NewGuid();
        global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.CanvasDefinition> evaluatedValue13 = [resolveCreateResult()];
        var flow = FlowDefinition.Create(newGuidResult10, 1, evaluatedValue13, "node", schemaVersion: 1);
        var validateResult = flow.Validate();
        var diagnostic = Assert.Single(validateResult, item => item.Code == DomainErrorCodes.NodeTypeRemoved);
        Assert.Equal("schemaVersion", diagnostic.Path);
        global::SereinFlow.Domain.CanvasDefinition resolveCreateResult()
        {
            global::System.Collections.Generic.IEnumerable<global::SereinFlow.Domain.NodeDefinition> evaluatedValue11 = [NodeDefinition.Create("node", NodeType.Action, "Node")];
            var createResult12 = CanvasDefinition.Create("main", CanvasLifecycle.Main, evaluatedValue11, []);
            return createResult12;
        }
    }
}
