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
        Assert.Contains(execution.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode);
    }

    [Fact]
    public void LegacyValidatorRemainsStrictForExecutionCallers()
    {
        var validation = FlowDefinitionContractValidator.Validate(CreateBlankDraft());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.UnknownEntryNode);
    }

    [Theory]
    [InlineData("Automatic", true)]
    [InlineData("unknown", false)]
    [InlineData("Automatic, Manual", false)]
    public void KnownEnumLiteralsAreValidatedDuringPersistence(string value, bool expectedValid)
    {
        var validation = FlowDefinitionContractValidator.ValidateForPersistence(CreateEnumDraft(value, isFlags: false));

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
        var validation = FlowDefinitionContractValidator.ValidateForPersistence(CreateEnumDraft(value, isFlags: true));

        Assert.Equal(expectedValid, validation.IsValid);
        if (!expectedValid)
        {
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == SereinFlow.Domain.DomainErrorCodes.InvalidEnumLiteral);
        }
    }

    [Fact]
    public void DynamicEnumSourcesRemainRuntimeValidated()
    {
        var definition = CreateEnumDraft("not-a-member", isFlags: false) with
        {
            Canvases = [new CanvasDto(
                "main",
                CanvasLifecycleDto.Main,
                [CreateEnumNode("not-a-member", isFlags: false, DataSourceDto.ProjectInput)],
                [])],
        };

        Assert.True(FlowDefinitionContractValidator.ValidateForPersistence(definition).IsValid);
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

    private static FlowDefinitionDto CreateEnumDraft(string value, bool isFlags)
        => new(
            Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [CreateEnumNode(value, isFlags, DataSourceDto.Literal)], [])],
            string.Empty,
            string.Empty,
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));

    private static NodeDto CreateEnumNode(string value, bool isFlags, DataSourceDto source)
    {
        var options = isFlags
            ? new[]
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
        var metadata = new EnumParameterMetadataDto(
            isFlags ? "Tests.Access" : "Tests.Mode",
            isFlags,
            "System.Int32",
            options);
        var parameter = new NodeParameterDto(
            "mode",
            value,
            source,
            false,
            new NodeParameterUiMetadataDto(
                "mode",
                "mode",
                isFlags ? "Tests.Access" : "Tests.Mode",
                value,
                source == DataSourceDto.ProjectInput ? "mode" : null,
                null,
                null,
                null,
                isFlags ? "Tests.Access" : "Tests.Mode",
                EnumMetadata: metadata));
        return new NodeDto("enum-node", NodeTypeDto.Action, "Enum node", 0, 0, [], [parameter], null);
    }
}
