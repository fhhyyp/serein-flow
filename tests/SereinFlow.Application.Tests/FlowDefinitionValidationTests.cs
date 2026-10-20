using System.Collections.Generic;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;
public sealed class FlowDefinitionValidationTests
{
    [Fact]
    public void ScriptNormalizationDerivesHashFromSourceInsteadOfClientMetadata()
    {
        const string source = "return 1";
        var scriptNodeDataDto = new ScriptNodeDataDto("script-node", source, "0.1", "stale-source-hash", [], []);
        var node = new NodeDto("script-node", NodeTypeDto.Script, "Script", 0, 0, [], [], scriptNodeDataDto);
        var newGuidResult = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var definition = new FlowDefinitionDto(newGuidResult, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue, node.Id, string.Empty);
        var normalized = FlowDefinitionContractNormalizer.NormalizeForExecution(definition);
        var evaluatedValue1 = Assert.Single(normalized.Canvases).Nodes;
        var script = Assert.Single(evaluatedValue1).Script;
        Assert.NotNull(script);
        var computeSourceHashResult = ScriptNodeDefinition.ComputeSourceHash(source);
        Assert.Equal(computeSourceHashResult, script.SourceHash);
        var getChecksumResult = FlowDiffService.GetChecksum(normalized);
        Assert.Equal(getChecksumResult, normalized.Checksum);
    }

    [Fact]
    public void BlankEditorDraftCanPersistButCannotExecute()
    {
        var definition = CreateBlankDraft();
        var persistence = FlowDefinitionContractValidator.ValidateForPersistence(definition);
        var normalized = FlowDefinitionContractNormalizer.NormalizeForPersistence(definition);
        var execution = FlowDefinitionContractValidator.ValidateForExecution(definition);
        Assert.True(persistence.IsValid);
        Assert.Same(definition, normalized);
        Assert.False(execution.IsValid);
        Assert.Contains(execution.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode);
    }

    [Fact]
    public void LegacyValidatorRemainsStrictForExecutionCallers()
    {
        var createBlankDraftResult = CreateBlankDraft();
        var validation = FlowDefinitionContractValidator.Validate(createBlankDraftResult);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode);
    }

    [Theory]
    [InlineData("Automatic", true)]
    [InlineData("unknown", false)]
    [InlineData("Automatic, Manual", false)]
    public void KnownEnumLiteralsAreValidatedDuringPersistence(string value, bool expectedValid)
    {
        var createEnumDraftResult = CreateEnumDraft(value, isFlags: false);
        var validation = FlowDefinitionContractValidator.ValidateForPersistence(createEnumDraftResult);
        Assert.Equal(expectedValid, validation.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.InvalidEnumLiteral);
        }
    }

    [Theory]
    [InlineData("None", true)]
    [InlineData("Read, Write", true)]
    [InlineData("None, Read", false)]
    [InlineData("Read, Read", false)]
    [InlineData("Execute", false)]
    public void FlagsEnumLiteralsValidateMembersAndZeroValueExclusivity(string value, bool expectedValid)
    {
        var createEnumDraftResult2 = CreateEnumDraft(value, isFlags: true);
        var validation = FlowDefinitionContractValidator.ValidateForPersistence(createEnumDraftResult2);
        Assert.Equal(expectedValid, validation.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.InvalidEnumLiteral);
        }
    }

    [Fact]
    public void DynamicEnumSourcesRemainRuntimeValidated()
    {
        var definition = CreateEnumDraft("not-a-member", isFlags: false)with
        {
            Canvases = [resolveCanvasDto()],
        };
        var evaluatedValue4 = FlowDefinitionContractValidator.ValidateForPersistence(definition).IsValid;
        Assert.True(evaluatedValue4);
        CanvasDto resolveCanvasDto()
        {
            IReadOnlyList<NodeDto> evaluatedValue3 = [CreateEnumNode("not-a-member", isFlags: false, DataSourceDto.ProjectInput)];
            var canvasDto = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue3, []);
            return canvasDto;
        }
    }

    private static FlowDefinitionDto CreateBlankDraft()
    {
        var newGuidResult5 = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue6 = [new CanvasDto("main", CanvasLifecycleDto.Main, [], [])];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        FlowDefinitionDto evaluatedValue7 = new(newGuidResult5, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue6, string.Empty, string.Empty, RunPolicy: flowRunPolicyDto);
        return evaluatedValue7;
    }

    private static FlowDefinitionDto CreateEnumDraft(string value, bool isFlags)
    {
        var newGuidResult8 = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue12 = [resolveCanvasDto9()];
        var flowRunPolicyDto13 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        FlowDefinitionDto evaluatedValue14 = new(newGuidResult8, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue12, string.Empty, string.Empty, RunPolicy: flowRunPolicyDto13);
        return evaluatedValue14;
        CanvasDto resolveCanvasDto9()
        {
            IReadOnlyList<NodeDto> evaluatedValue10 = [CreateEnumNode(value, isFlags, DataSourceDto.Literal)];
            var canvasDto11 = new CanvasDto("main", CanvasLifecycleDto.Main, evaluatedValue10, []);
            return canvasDto11;
        }
    }

    private static NodeDto CreateEnumNode(string value, bool isFlags, DataSourceDto source)
    {
        var options = isFlags ? new[]
        {
            new EnumValueOptionDto("None", "0"),
            new EnumValueOptionDto("Read", "1"),
            new EnumValueOptionDto("Write", "2"),
        }

        : new[]
        {
            new EnumValueOptionDto("Automatic", "0"),
            new EnumValueOptionDto("Manual", "1"),
        };
        var metadata = new EnumParameterMetadataDto(isFlags ? "Tests.Access" : "Tests.Mode", isFlags, "System.Int32", options);
        var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("mode", "mode", isFlags ? "Tests.Access" : "Tests.Mode", value, source == DataSourceDto.ProjectInput ? "mode" : null, null, null, null, isFlags ? "Tests.Access" : "Tests.Mode", EnumMetadata: metadata);
        var parameter = new NodeParameterDto("mode", value, source, false, nodeParameterUiMetadataDto);
        var nodeDto = new NodeDto("enum-node", NodeTypeDto.Action, "Enum node", 0, 0, [], [parameter], null);
        return nodeDto;
    }
}
