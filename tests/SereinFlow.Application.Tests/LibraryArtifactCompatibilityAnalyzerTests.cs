using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;
public sealed class LibraryArtifactCompatibilityAnalyzerTests
{
    [Fact]
    public void IdenticalManifestIsExactAndCompatible()
    {
        var createParameterResult = CreateParameter("input");
        var createNodeResult = CreateNode("library.node", createParameterResult);
        var baseline = CreateLibrary("baseline", "1.0.0", createNodeResult);
        var createParameterResult1 = CreateParameter("input");
        var createNodeResult2 = CreateNode("library.node", createParameterResult1);
        var target = CreateLibrary("target", "1.1.0", createNodeResult2);
        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);
        Assert.True(result.IsCompatible);
        Assert.Contains(result.Issues, issue => issue.Classification == LibraryCompatibilityClassificationDto.Exact);
    }

    [Fact]
    public void RemovedNodeIsBreaking()
    {
        var createParameterResult3 = CreateParameter("input");
        var createNodeResult4 = CreateNode("library.node", createParameterResult3);
        var baseline = CreateLibrary("baseline", "1.0.0", createNodeResult4);
        var target = CreateLibrary("target", "2.0.0");
        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(LibraryCompatibilityClassificationDto.Breaking, issue.Classification);
        Assert.True(issue.BlocksApplication);
        Assert.Equal(LibraryErrorCodes.PackageNodeRemoved, issue.Code);
    }

    [Fact]
    public void ParameterAliasProducesExplicitMappingDiagnostic()
    {
        var createParameterResult5 = CreateParameter("old-name");
        var createNodeResult6 = CreateNode("library.node", createParameterResult5);
        var baseline = CreateLibrary("baseline", "1.0.0", createNodeResult6);
        var libraryManifestParameterDto = new LibraryManifestParameterDto("new-name", LibraryContractIdentityConfidenceDto.Explicit, ["old-name"], "old-name", "New name", "System.String", true, null, false, null, null);
        var createNodeResult7 = CreateNode("library.node", libraryManifestParameterDto);
        var target = CreateLibrary("target", "1.1.0", createNodeResult7);
        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);
        var issue = Assert.Single(result.Issues, item => item.Code == LibraryErrorCodes.PackageParameterMappingRequired);
        Assert.Equal(LibraryCompatibilityClassificationDto.RequiresMapping, issue.Classification);
        Assert.True(issue.BlocksApplication);
        Assert.Equal("old-name", issue.SourceParameterId);
        Assert.Equal("new-name", issue.TargetParameterId);
    }

    [Fact]
    public void MissingManifestBlocksCompatibilityAnalysis()
    {
        var baseline = resolveCreateLibraryResult()with
        {
            CompatibilityManifest = null
        };
        var createParameterResult10 = CreateParameter("input");
        var createNodeResult11 = CreateNode("library.node", createParameterResult10);
        var target = CreateLibrary("target", "1.1.0", createNodeResult11);
        var result = LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, target);
        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, issue => issue.Code == LibraryErrorCodes.PackageManifestMissing);
        LibraryDto resolveCreateLibraryResult()
        {
            var createParameterResult8 = CreateParameter("input");
            var createNodeResult9 = CreateNode("library.node", createParameterResult8);
            var createLibraryResult = CreateLibrary("baseline", "1.0.0", createNodeResult9);
            return createLibraryResult;
        }
    }

    private static LibraryDto CreateLibrary(string id, string version, params LibraryManifestNodeDto[] nodes)
    {
        var libraryArtifactManifestDto = new LibraryArtifactManifestDto(id, "TestLibrary", "1.0.0.0", version, nodes);
        LibraryDto evaluatedValue = new(id, "TestLibrary", version, "TestLibrary.zip", 1, id, DateTimeOffset.UtcNow, [], CompatibilityManifest: libraryArtifactManifestDto);
        return evaluatedValue;
    }

    private static LibraryManifestNodeDto CreateNode(string contractId, params LibraryManifestParameterDto[] parameters)
    {
        LibraryManifestNodeDto evaluatedValue12 = new(contractId, LibraryContractIdentityConfidenceDto.Explicit, NodeTypeDto.Action, "TestLibrary.Nodes", "Execute", "TestLibrary.Nodes.Execute(System.String)", "System.String", false, parameters);
        return evaluatedValue12;
    }

    private static LibraryManifestParameterDto CreateParameter(string contractId)
    {
        LibraryManifestParameterDto evaluatedValue13 = new(contractId, LibraryContractIdentityConfidenceDto.Explicit, [], contractId, contractId, "System.String", true, null, false, null, null);
        return evaluatedValue13;
    }
}
