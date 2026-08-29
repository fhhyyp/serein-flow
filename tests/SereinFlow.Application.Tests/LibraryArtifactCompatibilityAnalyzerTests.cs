using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;

public sealed class LibraryArtifactCompatibilityAnalyzerTests
{
    [Fact]
    public void IdenticalManifestIsExactAndCompatible()
    {
        var baseline = CreateLibrary("baseline", "1.0.0", CreateNode("library.node", CreateParameter("input")));
        var target = CreateLibrary("target", "1.1.0", CreateNode("library.node", CreateParameter("input")));

        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);

        Assert.True(result.IsCompatible);
        Assert.Contains(result.Issues, issue => issue.Classification == LibraryCompatibilityClassificationDto.Exact);
    }

    [Fact]
    public void RemovedNodeIsBreaking()
    {
        var baseline = CreateLibrary("baseline", "1.0.0", CreateNode("library.node", CreateParameter("input")));
        var target = CreateLibrary("target", "2.0.0");

        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Breaking, issue.Classification);
        Assert.True(issue.BlocksApplication);
        Assert.Equal("library.package_node_removed", issue.Code);
    }

    [Fact]
    public void ParameterAliasProducesExplicitMappingDiagnostic()
    {
        var baseline = CreateLibrary("baseline", "1.0.0", CreateNode("library.node", CreateParameter("old-name")));
        var target = CreateLibrary(
            "target",
            "1.1.0",
            CreateNode("library.node", new LibraryManifestParameterDto(
                "new-name",
                LibraryContractIdentityConfidenceDto.Explicit,
                ["old-name"],
                "old-name",
                "New name",
                "System.String",
                true,
                null,
                false,
                null,
                null)));

        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);

        var issue = Assert.Single(result.Issues, item => item.Code == "library.package_parameter_mapping_required");
        Assert.Equal(LibraryCompatibilityClassificationDto.RequiresMapping, issue.Classification);
        Assert.True(issue.BlocksApplication);
        Assert.Equal("old-name", issue.SourceParameterId);
        Assert.Equal("new-name", issue.TargetParameterId);
    }

    [Fact]
    public void MissingManifestBlocksCompatibilityAnalysis()
    {
        var baseline = CreateLibrary("baseline", "1.0.0", CreateNode("library.node", CreateParameter("input"))) with
        {
            CompatibilityManifest = null
        };
        var target = CreateLibrary("target", "1.1.0", CreateNode("library.node", CreateParameter("input")));

        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, issue => issue.Code == "library.package_manifest_missing");
    }

    private static LibraryDto CreateLibrary(string id, string version, params LibraryManifestNodeDto[] nodes)
        => new(
            id,
            "TestLibrary",
            version,
            "TestLibrary.zip",
            1,
            id,
            DateTimeOffset.UtcNow,
            [],
            CompatibilityManifest: new LibraryArtifactManifestDto(
                id,
                "TestLibrary",
                "1.0.0.0",
                version,
                nodes));

    private static LibraryManifestNodeDto CreateNode(string contractId, params LibraryManifestParameterDto[] parameters)
        => new(
            contractId,
            LibraryContractIdentityConfidenceDto.Explicit,
            NodeTypeDto.Action,
            "TestLibrary.Nodes",
            "Execute",
            "TestLibrary.Nodes.Execute(System.String)",
            "System.String",
            false,
            parameters);

    private static LibraryManifestParameterDto CreateParameter(string contractId)
        => new(
            contractId,
            LibraryContractIdentityConfidenceDto.Explicit,
            [],
            contractId,
            contractId,
            "System.String",
            true,
            null,
            false,
            null,
            null);
}
