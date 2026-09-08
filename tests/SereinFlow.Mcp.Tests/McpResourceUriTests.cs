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
        Assert.Equal(
            "sereinflow://projects/{projectId}/flows/{flowId}/topology",
            McpResourceUris.FlowTopologyTemplate);
        Assert.Equal(
            "sereinflow://runs/{runId}/workpieces/{workpieceId}",
            McpResourceUris.RunWorkpieceTemplate);

        Assert.All(McpResourceCatalog.Templates, static template => Assert.Contains('{', template.UriTemplate));
        Assert.DoesNotContain(McpResourceCatalog.Templates, static template => template.UriTemplate == McpResourceUris.Runs);
        Assert.True(McpResourceCatalog.TryGet(McpResourceId.FlowVersion, out var flowVersion));
        Assert.Equal(
            new[] { "projectId", "flowId", "track", "version" },
            flowVersion!.ParameterNames);
    }

    [Fact]
    public void EveryCatalogEntryHasOneIdentityAndCanBeParsed()
    {
        var ids = Enum.GetValues<McpResourceId>();
        foreach (var id in ids)
        {
            Assert.True(McpResourceCatalog.TryGet(id, out var definition));
            Assert.NotNull(definition);

            var uri = Expand(definition!.UriTemplate);
            Assert.True(McpResourceUriParser.TryParse(uri, out var match), uri);
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

        Assert.DoesNotContain(
            McpResourceCatalog.Definitions,
            definition => directUris.Contains(definition.UriTemplate));
        Assert.All(McpResourceCatalog.Definitions, static definition => Assert.True(definition.IsTemplate));
        Assert.All(McpResourceCatalog.DirectResources, static definition => Assert.False(definition.IsTemplate));
    }

    [Fact]
    public void BuildersAndParserRoundTripTypedParameters()
    {
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        AssertMatch(McpResourceUris.Project(projectId), McpResourceId.Project, ("projectId", projectId.ToString("D")));
        AssertMatch(
            McpResourceUris.FlowTopology(projectId, flowId),
            McpResourceId.FlowTopology,
            ("projectId", projectId.ToString("D")),
            ("flowId", flowId.ToString("D")));
        AssertMatch(
            McpResourceUris.RunWorkpiece(runId, "image/a 1.png"),
            McpResourceId.RunWorkpiece,
            ("runId", runId.ToString("D")),
            ("workpieceId", "image/a 1.png"));
    }

    [Fact]
    public void ParserRejectsQueryAndFragmentSuffixes()
    {
        Assert.False(McpResourceUriParser.TryParse(McpResourceUris.Projects + "?page=1", out _));
        Assert.False(McpResourceUriParser.TryParse(McpResourceUris.Runs + "#latest", out _));
    }

    [Fact]
    public void ParserRejectsUnknownOrMalformedResourceShapes()
    {
        Assert.False(McpResourceUriParser.TryParse("sereinflow://projects/not-a-guid", out _));
        Assert.False(McpResourceUriParser.TryParse("sereinflow://projects/11111111-1111-1111-1111-111111111111/unknown", out _));
        Assert.False(McpResourceUriParser.TryParse("sereinflow://runs/not-a-guid/workpieces", out var match));
        Assert.Null(match);
        Assert.False(McpResourceUriParser.TryParse("https://runs/11111111-1111-1111-1111-111111111111", out _));
    }

    [Fact]
    public void HttpWorkpieceLinkEscapesTheIdentifier()
    {
        var runId = Guid.NewGuid();
        Assert.Equal(
            $"/api/runs/{runId:D}/workpieces/image%2Fa%201.png",
            SereinFlowApiUris.RunWorkpiece(runId, "image/a 1.png"));
    }

    private static void AssertMatch(
        string uri,
        McpResourceId expectedId,
        params (string Key, string Value)[] expectedParameters)
    {
        Assert.True(McpResourceUriParser.TryParse(uri, out var match));
        Assert.NotNull(match);
        Assert.Equal(expectedId, match!.Id);
        Assert.Equal(
            expectedParameters.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal),
            match.Parameters);
    }

    private static string Expand(string template)
        => template
            .Replace("{projectId}", "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal)
            .Replace("{flowId}", "22222222-2222-2222-2222-222222222222", StringComparison.Ordinal)
            .Replace("{previewId}", "33333333-3333-3333-3333-333333333333", StringComparison.Ordinal)
            .Replace("{upgradeId}", "44444444-4444-4444-4444-444444444444", StringComparison.Ordinal)
            .Replace("{runId}", "55555555-5555-5555-5555-555555555555", StringComparison.Ordinal)
            .Replace("{sessionId}", "66666666-6666-6666-6666-666666666666", StringComparison.Ordinal)
            .Replace("{libraryId}", "library-a", StringComparison.Ordinal)
            .Replace("{familyId}", "family-a", StringComparison.Ordinal)
            .Replace("{track}", "development", StringComparison.Ordinal)
            .Replace("{version}", "42", StringComparison.Ordinal)
            .Replace("{workpieceId}", "image-a.png", StringComparison.Ordinal);
}
