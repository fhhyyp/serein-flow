using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;

public sealed class LibraryCompatibilityAnalyzerTests
{
    private const string SourceArtifactId = "source-artifact";
    private const string TargetArtifactId = "target-artifact";
    private const string FamilyId = "quality-family";
    private readonly LibraryCompatibilityAnalyzer _analyzer = new();

    [Fact]
    public void ExplicitParameterAliasRequiresConfirmationWithoutBlockingApplication()
    {
        var source = CreateLibrary(SourceArtifactId, CreateNode(CreateParameter("pass-count")));
        var target = CreateLibrary(TargetArtifactId, CreateNode(CreateParameter("qualified-count", aliases: ["pass-count"])));
        var flow = CreateFlow(SourceArtifactId, "pass-count");

        var preview = _analyzer.Analyze(flow, source, target);

        var issue = Assert.Single(preview.Issues);
        Assert.True(preview.CanApply);
        Assert.Equal(LibraryCompatibilityClassificationDto.RequiresMapping, issue.Classification);
        Assert.True(issue.RequiresAcknowledgement);
        Assert.False(issue.BlocksApplication);
        Assert.Equal("pass-count", issue.SourceParameterId);
        Assert.Equal("qualified-count", issue.TargetParameterId);
    }

    [Fact]
    public void RemovedUsedParameterBlocksApplication()
    {
        var source = CreateLibrary(SourceArtifactId, CreateNode(CreateParameter("pass-count")));
        var target = CreateLibrary(TargetArtifactId, CreateNode());
        var flow = CreateFlow(SourceArtifactId, "pass-count");

        var preview = _analyzer.Analyze(flow, source, target);

        Assert.False(preview.CanApply);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Breaking, issue.Classification);
        Assert.Equal("library.upgrade_parameter_removed", issue.Code);
        Assert.True(issue.BlocksApplication);
    }

    [Fact]
    public void RemovedUnusedParameterIsCompatible()
    {
        var source = CreateLibrary(SourceArtifactId, CreateNode(CreateParameter("pass-count")));
        var target = CreateLibrary(TargetArtifactId, CreateNode());
        var flow = CreateFlow(SourceArtifactId, "pass-count", valueJson: null);

        var preview = _analyzer.Analyze(flow, source, target);

        Assert.True(preview.CanApply);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Compatible, issue.Classification);
        Assert.Equal("library.upgrade_parameter_removed_unused", issue.Code);
        Assert.False(issue.BlocksApplication);
    }

    [Fact]
    public void AmbiguousLegacyNodeIdentityIsUnknownAndBlocked()
    {
        var source = CreateLibrary(
            SourceArtifactId,
            CreateNode(CreateParameter("first"), LibraryContractIdentityConfidenceDto.Legacy),
            CreateNode(CreateParameter("second"), LibraryContractIdentityConfidenceDto.Legacy));
        var target = CreateLibrary(TargetArtifactId, CreateNode(CreateParameter("first"), LibraryContractIdentityConfidenceDto.Legacy));
        var flow = CreateFlow(SourceArtifactId, "first", contractId: null);

        var preview = _analyzer.Analyze(flow, source, target);

        Assert.False(preview.CanApply);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Unknown, issue.Classification);
        Assert.Equal("library.upgrade_source_contract_unknown", issue.Code);
        Assert.True(issue.BlocksApplication);
    }

    private static FlowDefinitionDto CreateFlow(
        string artifactId,
        string parameterId,
        string? contractId = "quality-rate",
        string? valueJson = "10")
    {
        var node = new NodeDto(
            "quality-rate-node",
            NodeTypeDto.Action,
            "Quality rate",
            100,
            100,
            [],
            [new NodeParameterDto(
                "passCount",
                valueJson,
                DataSourceDto.Literal,
                true,
                new NodeParameterUiMetadataDto(parameterId, "Pass count", "System.Int32", "10", null, null, null, null))],
            null,
            new NodeUiMetadataDto(
                "action",
                "Quality rate",
                "",
                null,
                "ready",
                true,
                null,
                "library",
                artifactId,
                "Quality.Nodes",
                "Calculate",
                "Quality.dll",
                "1.0.0",
                "System.Decimal",
                LibraryNodeContractId: contractId));
        return new FlowDefinitionDto(
            Guid.NewGuid(),
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "checksum");
    }

    private static LibraryDto CreateLibrary(string artifactId, params LibraryManifestNodeDto[] manifestNodes)
    {
        var nodes = manifestNodes.Select(node => new LibraryNodeDto(
            node.ContractId,
            node.Type,
            node.MethodName,
            null,
            artifactId,
            node.DeclaringType,
            node.MethodName,
            "Quality.dll",
            "1.0.0",
            node.ReturnType,
            node.Parameters.Select(parameter => new LibraryParameterDto(
                parameter.ContractId,
                parameter.ClrName,
                parameter.Type,
                null,
                parameter.Required,
                DefaultValue: parameter.DefaultValue,
                Aliases: parameter.Aliases,
                IdentityConfidence: parameter.IdentityConfidence)).ToArray(),
            node.IsAwaitable,
            node.ContractId,
            node.OverloadSignature,
            node.IdentityConfidence)).ToArray();
        return new LibraryDto(
            artifactId,
            "Quality library",
            "1.0.0",
            "Quality-1.0.0.zip",
            1024,
            artifactId,
            DateTimeOffset.UtcNow,
            nodes,
            LibraryLifecycleDto.Available,
            FamilyId,
            "1.0.0",
            new LibraryArtifactManifestDto(
                artifactId,
                "Quality",
                "1.0.0",
                "1.0.0",
                manifestNodes));
    }

    private static LibraryManifestNodeDto CreateNode(
        LibraryManifestParameterDto? parameter = null,
        LibraryContractIdentityConfidenceDto identity = LibraryContractIdentityConfidenceDto.Explicit)
        => new(
            "quality-rate",
            identity,
            NodeTypeDto.Action,
            "Quality.Nodes",
            "Calculate",
            "Quality.Nodes::Calculate(System.Int32)",
            "System.Decimal",
            false,
            parameter is null ? [] : [parameter]);

    private static LibraryManifestParameterDto CreateParameter(string id, IReadOnlyList<string>? aliases = null)
        => new(
            id,
            LibraryContractIdentityConfidenceDto.Explicit,
            aliases ?? [],
            "passCount",
            "Pass count",
            "System.Int32",
            true,
            null,
            false,
            null,
            null);
}
