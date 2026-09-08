using System.Globalization;

namespace SereinFlow.Mcp;

public sealed record McpResourceMatch(
    McpResourceId Id,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Parses canonical MCP resource URIs into internal resource identities. It
/// intentionally does not invoke handlers or create dependency scopes.
/// </summary>
public static class McpResourceUriParser
{
    public static bool TryParse(string uri, out McpResourceMatch? match)
    {
        match = null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || !string.Equals(parsed.Scheme, McpResourceUris.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
            return false;

        var segments = parsed.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        var collection = parsed.Host.ToLowerInvariant();

        if (segments.Length == 0)
        {
            var directId = collection switch
            {
                McpResourceSegments.Projects => McpResourceId.Projects,
                McpResourceSegments.ArchivedProjects => McpResourceId.ArchivedProjects,
                McpResourceSegments.Libraries => McpResourceId.Libraries,
                McpResourceSegments.ArchivedLibraries => McpResourceId.ArchivedLibraries,
                McpResourceSegments.LibraryFamilies => McpResourceId.LibraryFamilies,
                McpResourceSegments.Runs => McpResourceId.Runs,
                McpResourceSegments.DebugSessions => McpResourceId.DebugSessions,
                _ => (McpResourceId?)null
            };
            if (directId is not null)
                return Set(directId.Value, out match);
        }

        if (collection == McpResourceSegments.Projects)
        {
            if (segments.Length == 0)
                return false;
            if (!Guid.TryParse(segments[0], out var projectId))
                return false;
            if (segments.Length == 1)
                return Set(McpResourceId.Project, ("projectId", projectId.ToString("D")), out match);
            if (segments.Length == 2 && Equals(segments[1], McpResourceSegments.Libraries))
                return Set(McpResourceId.ProjectLibraries, ("projectId", projectId.ToString("D")), out match);
            if (segments.Length == 3 && Equals(segments[1], McpResourceSegments.LibraryUpgrades) && Guid.TryParse(segments[2], out var upgradeId))
                return Set(McpResourceId.LibraryUpgrade, ("projectId", projectId.ToString("D")), ("upgradeId", upgradeId.ToString("D")), out match);
            if (segments.Length >= 4 && Equals(segments[1], McpResourceSegments.Flows) && Guid.TryParse(segments[2], out var flowId))
            {
                if (segments.Length == 4 && Equals(segments[3], McpResourceSegments.Topology))
                    return Set(McpResourceId.FlowTopology, ("projectId", projectId.ToString("D")), ("flowId", flowId.ToString("D")), out match);
                if (segments.Length >= 5 && Equals(segments[3], McpResourceSegments.Versions))
                {
                    if (segments.Length == 5)
                        return Set(McpResourceId.FlowVersions, ("projectId", projectId.ToString("D")), ("flowId", flowId.ToString("D")), ("track", segments[4]), out match);
                    if (segments.Length == 6 && long.TryParse(segments[5], out var version))
                        return Set(McpResourceId.FlowVersion, ("projectId", projectId.ToString("D")), ("flowId", flowId.ToString("D")), ("track", segments[4]), ("version", version.ToString(CultureInfo.InvariantCulture)), out match);
                }
            }
        }
        else if (collection == McpResourceSegments.Libraries)
        {
            if (segments.Length == 1)
                return Set(McpResourceId.Library, ("libraryId", segments[0]), out match);
        }
        else if (collection == McpResourceSegments.LibraryFamilies)
        {
            if (segments.Length == 1)
                return Set(McpResourceId.LibraryFamily, ("familyId", segments[0]), out match);
        }
        else if (collection == McpResourceSegments.McpPreviews && segments.Length == 1 && Guid.TryParse(segments[0], out var previewId))
            return Set(McpResourceId.Preview, ("previewId", previewId.ToString("D")), out match);
        else if (collection == McpResourceSegments.Runs)
        {
            if (segments.Length == 1 && Guid.TryParse(segments[0], out var runId))
                return Set(McpResourceId.Run, ("runId", runId.ToString("D")), out match);
            if (segments.Length == 2 && Guid.TryParse(segments[0], out runId) && Equals(segments[1], McpResourceSegments.Workpieces))
                return Set(McpResourceId.RunWorkpieces, ("runId", runId.ToString("D")), out match);
            if (segments.Length == 3 && Guid.TryParse(segments[0], out runId) && Equals(segments[1], McpResourceSegments.Workpieces))
                return Set(McpResourceId.RunWorkpiece, ("runId", runId.ToString("D")), ("workpieceId", segments[2]), out match);
        }
        else if (collection == McpResourceSegments.DebugSessions && segments.Length == 1 && Guid.TryParse(segments[0], out var sessionId))
            return Set(McpResourceId.DebugSession, ("sessionId", sessionId.ToString("D")), out match);

        return false;
    }

    private static bool Set(McpResourceId id, out McpResourceMatch? match)
        => Set(id, Array.Empty<(string, string)>(), out match);

    private static bool Set(McpResourceId id, (string Key, string Value) first, out McpResourceMatch? match)
        => Set(id, new[] { first }, out match);

    private static bool Set(McpResourceId id, (string Key, string Value) first, (string Key, string Value) second, out McpResourceMatch? match)
        => Set(id, new[] { first, second }, out match);

    private static bool Set(McpResourceId id, (string Key, string Value) first, (string Key, string Value) second, (string Key, string Value) third, out McpResourceMatch? match)
        => Set(id, new[] { first, second, third }, out match);

    private static bool Set(McpResourceId id, (string Key, string Value) first, (string Key, string Value) second, (string Key, string Value) third, (string Key, string Value) fourth, out McpResourceMatch? match)
        => Set(id, new[] { first, second, third, fourth }, out match);

    private static bool Set(McpResourceId id, IEnumerable<(string Key, string Value)> values, out McpResourceMatch? match)
    {
        match = new McpResourceMatch(id, values.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal));
        return true;
    }

    private static bool Equals(string value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
