using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class FlowDefinitionValidationTests
{
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
        Assert.Contains(execution.Diagnostics, diagnostic => diagnostic.Code == "flow.unknown_entry_node");
    }

    [Fact]
    public void LegacyValidatorRemainsStrictForExecutionCallers()
    {
        var validation = FlowDefinitionContractValidator.Validate(CreateBlankDraft());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == "flow.unknown_entry_node");
    }

    private static FlowDefinitionDto CreateBlankDraft()
        => new(
            Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [], [])],
            string.Empty,
            string.Empty,
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
}
