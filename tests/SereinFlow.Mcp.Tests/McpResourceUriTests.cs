using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Tests;

public sealed class McpResourceUriTests
{
    [Fact]
    public void CatalogPreservesThePublishedWireContract()
    {
        Assert.Equal("sereinflow", McpResourceUris.Scheme);
        Assert.Equal("sereinflow://projects", McpResourceUris.Projects);
        Assert.Equal("sereinflow://runs", McpResourceUris.Runs);
        Assert.Equal("sereinflow://projects/{projectId}", McpResourceUris.ProjectTemplate);
        Assert.Equal("sereinflow://projects/{projectId}/flows/{flowId}/topology", McpResourceUris.FlowTopologyTemplate);
        Assert.Equal("sereinflow://runs/{runId}/workpieces/{workpieceId}", McpResourceUris.RunWorkpieceTemplate);
        Assert.All(McpResourceCatalog.Templates, static template =>
        {
            Assert.Contains('{', template.UriTemplate);
        });
        Assert.DoesNotContain(McpResourceCatalog.Templates, static template => template.UriTemplate == McpResourceUris.Runs);
        var tryGetResult = McpResourceCatalog.TryGet(McpResourceId.FlowVersion, out var flowVersion);
        Assert.True(tryGetResult);
        Assert.Equal(new[] { "projectId", "flowId", "track", "version" }, flowVersion!.ParameterNames);
    }

    [Fact]
    public void EveryCatalogEntryHasOneIdentityAndCanBeParsed()
    {
        var ids = Enum.GetValues<McpResourceId>();
        foreach (var id in ids)
        {
            var tryGetResult1 = McpResourceCatalog.TryGet(id, out var definition);
            Assert.True(tryGetResult1);
            Assert.NotNull(definition);
            var uri = Expand(definition!.UriTemplate);
            var tryParseResult = McpResourceUriParser.TryParse(uri, out var match);
            Assert.True(tryParseResult, uri);
            Assert.Equal(id, match!.Id);
        }

        Assert.Equal(ids.Length, McpResourceCatalog.Definitions.Count + McpResourceCatalog.DirectResources.Count);
    }

    [Fact]
    public void DirectResourcesAndTemplatesAreDisjoint()
    {
        var directUris = McpResourceCatalog.DirectResources
            .Select(static definition => definition.UriTemplate)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(McpResourceCatalog.Definitions, definition =>
        {
            var containsResult = directUris.Contains(definition.UriTemplate);
            return containsResult;
        });
        Assert.All(McpResourceCatalog.Definitions, static definition =>
        {
            Assert.True(definition.IsTemplate);
        });
        Assert.All(McpResourceCatalog.DirectResources, static definition =>
        {
            Assert.False(definition.IsTemplate);
        });
    }

    [Fact]
    public void BuildersAndParserRoundTripTypedParameters()
    {
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var projectResult = McpResourceUris.Project(projectId);
        var evaluatedValue = ("projectId", projectId.ToString("D"));
        AssertMatch(projectResult, McpResourceId.Project, evaluatedValue);
        var flowTopologyResult = McpResourceUris.FlowTopology(projectId, flowId);
        var evaluatedValue2 = ("projectId", projectId.ToString("D"));
        var evaluatedValue3 = ("flowId", flowId.ToString("D"));
        AssertMatch(flowTopologyResult, McpResourceId.FlowTopology, evaluatedValue2, evaluatedValue3);
        var runWorkpieceResult = McpResourceUris.RunWorkpiece(runId, "image/a 1.png");
        var evaluatedValue4 = ("runId", runId.ToString("D"));
        AssertMatch(runWorkpieceResult, McpResourceId.RunWorkpiece, evaluatedValue4, ("workpieceId", "image/a 1.png"));
    }

    [Fact]
    public void ParserRejectsQueryAndFragmentSuffixes()
    {
        var tryParseResult5 = McpResourceUriParser.TryParse(McpResourceUris.Projects + "?page=1", out _);
        Assert.False(tryParseResult5);
        var tryParseResult6 = McpResourceUriParser.TryParse(McpResourceUris.Runs + "#latest", out _);
        Assert.False(tryParseResult6);
    }

    [Fact]
    public void ParserRejectsUnknownOrMalformedResourceShapes()
    {
        var tryParseResult7 = McpResourceUriParser.TryParse("sereinflow://projects/not-a-guid", out _);
        Assert.False(tryParseResult7);
        var tryParseResult8 = McpResourceUriParser.TryParse("sereinflow://projects/11111111-1111-1111-1111-111111111111/unknown", out _);
        Assert.False(tryParseResult8);
        var tryParseResult9 = McpResourceUriParser.TryParse("sereinflow://runs/not-a-guid/workpieces", out var match);
        Assert.False(tryParseResult9);
        Assert.Null(match);
        var tryParseResult10 = McpResourceUriParser.TryParse("https://runs/11111111-1111-1111-1111-111111111111", out _);
        Assert.False(tryParseResult10);
    }

    [Fact]
    public void HttpWorkpieceLinkEscapesTheIdentifier()
    {
        var runId = Guid.NewGuid();
        var runWorkpieceResult11 = SereinFlowApiUris.RunWorkpiece(runId, "image/a 1.png");
        Assert.Equal($"/api/runs/{runId:D}/workpieces/image%2Fa%201.png", runWorkpieceResult11);
    }

    private static void AssertMatch(string uri, McpResourceId expectedId, params (string Key, string Value)[] expectedParameters)
    {
        var tryParseResult12 = McpResourceUriParser.TryParse(uri, out var match);
        Assert.True(tryParseResult12);
        Assert.NotNull(match);
        Assert.Equal(expectedId, match!.Id);
        var toDictionaryResult = expectedParameters.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        Assert.Equal(toDictionaryResult, match.Parameters);
    }

    private static string Expand(string template)
    {
        var replaceResult = template.Replace("{projectId}", "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal);
        var replaceResult13 = replaceResult.Replace("{flowId}", "22222222-2222-2222-2222-222222222222", StringComparison.Ordinal);
        var replaceResult14 = replaceResult13.Replace("{previewId}", "33333333-3333-3333-3333-333333333333", StringComparison.Ordinal);
        var replaceResult15 = replaceResult14.Replace("{upgradeId}", "44444444-4444-4444-4444-444444444444", StringComparison.Ordinal);
        var replaceResult16 = replaceResult15.Replace("{runId}", "55555555-5555-5555-5555-555555555555", StringComparison.Ordinal);
        var replaceResult17 = replaceResult16.Replace("{sessionId}", "66666666-6666-6666-6666-666666666666", StringComparison.Ordinal);
        var replaceResult18 = replaceResult17.Replace("{libraryId}", "library-a", StringComparison.Ordinal);
        var replaceResult19 = replaceResult18.Replace("{familyId}", "family-a", StringComparison.Ordinal);
        var replaceResult20 = replaceResult19.Replace("{track}", "development", StringComparison.Ordinal);
        var replaceResult21 = replaceResult20.Replace("{version}", "42", StringComparison.Ordinal);
        var replaceResult22 = replaceResult21.Replace("{workpieceId}", "image-a.png", StringComparison.Ordinal);
        return replaceResult22;
    }
}
