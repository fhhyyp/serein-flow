using System.Collections.Generic;
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
        var createParameterResult = CreateParameter("pass-count");
        var createNodeResult = CreateNode(createParameterResult);
        var source = CreateLibrary(SourceArtifactId, createNodeResult);
        var createParameterResult1 = CreateParameter("qualified-count", aliases: ["pass-count"]);
        var createNodeResult2 = CreateNode(createParameterResult1);
        var target = CreateLibrary(TargetArtifactId, createNodeResult2);
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
        var createParameterResult3 = CreateParameter("pass-count");
        var createNodeResult4 = CreateNode(createParameterResult3);
        var source = CreateLibrary(SourceArtifactId, createNodeResult4);
        var createNodeResult5 = CreateNode();
        var target = CreateLibrary(TargetArtifactId, createNodeResult5);
        var flow = CreateFlow(SourceArtifactId, "pass-count");
        var preview = _analyzer.Analyze(flow, source, target);
        Assert.False(preview.CanApply);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Breaking, issue.Classification);
        Assert.Equal(LibraryErrorCodes.UpgradeParameterRemoved, issue.Code);
        Assert.True(issue.BlocksApplication);
    }

    [Fact]
    public void RemovedUnusedParameterIsCompatible()
    {
        var createParameterResult6 = CreateParameter("pass-count");
        var createNodeResult7 = CreateNode(createParameterResult6);
        var source = CreateLibrary(SourceArtifactId, createNodeResult7);
        var createNodeResult8 = CreateNode();
        var target = CreateLibrary(TargetArtifactId, createNodeResult8);
        var flow = CreateFlow(SourceArtifactId, "pass-count", valueJson: null);
        var preview = _analyzer.Analyze(flow, source, target);
        Assert.True(preview.CanApply);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Compatible, issue.Classification);
        Assert.Equal(LibraryErrorCodes.UpgradeParameterRemovedUnused, issue.Code);
        Assert.False(issue.BlocksApplication);
    }

    [Fact]
    public void AmbiguousLegacyNodeIdentityIsUnknownAndBlocked()
    {
        var createParameterResult9 = CreateParameter("first");
        var createNodeResult10 = CreateNode(createParameterResult9, LibraryContractIdentityConfidenceDto.Legacy);
        var createParameterResult11 = CreateParameter("second");
        var createNodeResult12 = CreateNode(createParameterResult11, LibraryContractIdentityConfidenceDto.Legacy);
        var source = CreateLibrary(SourceArtifactId, createNodeResult10, createNodeResult12);
        var createParameterResult13 = CreateParameter("first");
        var createNodeResult14 = CreateNode(createParameterResult13, LibraryContractIdentityConfidenceDto.Legacy);
        var target = CreateLibrary(TargetArtifactId, createNodeResult14);
        var flow = CreateFlow(SourceArtifactId, "first", contractId: null);
        var preview = _analyzer.Analyze(flow, source, target);
        Assert.False(preview.CanApply);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Unknown, issue.Classification);
        Assert.Equal(LibraryErrorCodes.UpgradeSourceContractUnknown, issue.Code);
        Assert.True(issue.BlocksApplication);
    }

    private static FlowDefinitionDto CreateFlow(string artifactId, string parameterId, string? contractId = "quality-rate", string? valueJson = "10")
    {
        IReadOnlyList<NodeParameterDto> evaluatedValue = [resolveNodeParameterDto()];
        var nodeUiMetadataDto = new NodeUiMetadataDto("action", "Quality rate", "", null, "ready", true, null, "library", artifactId, "Quality.Nodes", "Calculate", "Quality.dll", "1.0.0", "System.Decimal", LibraryNodeContractId: contractId);
        var node = new NodeDto("quality-rate-node", NodeTypeDto.Action, "Quality rate", 100, 100, [], evaluatedValue, null, nodeUiMetadataDto);
        var newGuidResult = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue15 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowDefinitionDto = new FlowDefinitionDto(newGuidResult, 5, 1, evaluatedValue15, node.Id, "checksum");
        return flowDefinitionDto;
        NodeParameterDto resolveNodeParameterDto()
        {
            var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto(parameterId, "Pass count", "System.Int32", "10", null, null, null, null);
            var nodeParameterDto = new NodeParameterDto("passCount", valueJson, DataSourceDto.Literal, true, nodeParameterUiMetadataDto);
            return nodeParameterDto;
        }
    }

    private static LibraryDto CreateLibrary(string artifactId, params LibraryManifestNodeDto[] manifestNodes)
    {
        var selectResult16 = manifestNodes.Select(node =>
        {
            var selectResult = node.Parameters.Select(parameter =>
            {
                var libraryParameterDto = new LibraryParameterDto(parameter.ContractId, parameter.ClrName, parameter.Type, null, parameter.Required, DefaultValue: parameter.DefaultValue, Aliases: parameter.Aliases, IdentityConfidence: parameter.IdentityConfidence);
                return libraryParameterDto;
            });
            var toArrayResult = selectResult.ToArray();
            var libraryNodeDto = new LibraryNodeDto(node.ContractId, node.Type, node.MethodName, null, artifactId, node.DeclaringType, node.MethodName, "Quality.dll", "1.0.0", node.ReturnType, toArrayResult, node.IsAwaitable, node.ContractId, node.OverloadSignature, node.IdentityConfidence);
            return libraryNodeDto;
        });
        var nodes = selectResult16.ToArray();
        var libraryArtifactManifestDto = new LibraryArtifactManifestDto(artifactId, "Quality", "1.0.0", "1.0.0", manifestNodes);
        var libraryDto = new LibraryDto(artifactId, "Quality library", "1.0.0", "Quality-1.0.0.zip", 1024, artifactId, DateTimeOffset.UtcNow, nodes, LibraryLifecycleDto.Available, FamilyId, "1.0.0", libraryArtifactManifestDto);
        return libraryDto;
    }

    private static LibraryManifestNodeDto CreateNode(LibraryManifestParameterDto? parameter = null, LibraryContractIdentityConfidenceDto identity = LibraryContractIdentityConfidenceDto.Explicit)
    {
        LibraryManifestNodeDto evaluatedValue17 = new("quality-rate", identity, NodeTypeDto.Action, "Quality.Nodes", "Calculate", "Quality.Nodes::Calculate(System.Int32)", "System.Decimal", false, parameter is null ? [] : [parameter]);
        return evaluatedValue17;
    }

    private static LibraryManifestParameterDto CreateParameter(string id, IReadOnlyList<string>? aliases = null)
    {
        LibraryManifestParameterDto evaluatedValue18 = new(id, LibraryContractIdentityConfidenceDto.Explicit, aliases ?? [], "passCount", "Pass count", "System.Int32", true, null, false, null, null);
        return evaluatedValue18;
    }
}
