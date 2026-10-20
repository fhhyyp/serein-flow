using System.Globalization;

namespace SereinFlow.Mcp;

public sealed record McpResourceMatch(McpResourceId Id, IReadOnlyDictionary<string, string> Parameters);
/// <summary>
/// Parses canonical MCP resource URIs into internal resource identities. It
/// intentionally does not invoke handlers or create dependency scopes.
/// </summary>
public static class McpResourceUriParser
{
    public static bool TryParse(string uri, out McpResourceMatch? match)
    {
        match = null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !string.Equals(parsed.Scheme, McpResourceUris.Scheme, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        var segments = parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
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
            {
                var setResult = Set(directId.Value, out match);
                return setResult;
            }
        }

        if (collection == McpResourceSegments.Projects)
        {
            if (segments.Length == 0)
            {
                return false;
            }

            if (!Guid.TryParse(segments[0], out var projectId))
            {
                return false;
            }

            if (segments.Length == 1)
            {
                var evaluatedValue = ("projectId", projectId.ToString("D"));
                var setResult1 = Set(McpResourceId.Project, evaluatedValue, out match);
                return setResult1;
            }

            if (segments.Length == 2 && Equals(segments[1], McpResourceSegments.Libraries))
            {
                var evaluatedValue2 = ("projectId", projectId.ToString("D"));
                var setResult3 = Set(McpResourceId.ProjectLibraries, evaluatedValue2, out match);
                return setResult3;
            }

            if (segments.Length == 3 && Equals(segments[1], McpResourceSegments.LibraryUpgrades) && Guid.TryParse(segments[2], out var upgradeId))
            {
                var evaluatedValue4 = ("projectId", projectId.ToString("D"));
                var evaluatedValue5 = ("upgradeId", upgradeId.ToString("D"));
                var setResult6 = Set(McpResourceId.LibraryUpgrade, evaluatedValue4, evaluatedValue5, out match);
                return setResult6;
            }

            if (segments.Length >= 4 && Equals(segments[1], McpResourceSegments.Flows) && Guid.TryParse(segments[2], out var flowId))
            {
                if (segments.Length == 4 && Equals(segments[3], McpResourceSegments.Topology))
                {
                    var evaluatedValue7 = ("projectId", projectId.ToString("D"));
                    var evaluatedValue8 = ("flowId", flowId.ToString("D"));
                    var setResult9 = Set(McpResourceId.FlowTopology, evaluatedValue7, evaluatedValue8, out match);
                    return setResult9;
                }

                if (segments.Length >= 5 && Equals(segments[3], McpResourceSegments.Versions))
                {
                    if (segments.Length == 5)
                    {
                        var evaluatedValue10 = ("projectId", projectId.ToString("D"));
                        var evaluatedValue11 = ("flowId", flowId.ToString("D"));
                        var setResult12 = Set(McpResourceId.FlowVersions, evaluatedValue10, evaluatedValue11, ("track", segments[4]), out match);
                        return setResult12;
                    }

                    if (segments.Length == 6 && long.TryParse(segments[5], out var version))
                    {
                        var evaluatedValue13 = ("projectId", projectId.ToString("D"));
                        var evaluatedValue14 = ("flowId", flowId.ToString("D"));
                        var evaluatedValue15 = ("version", version.ToString(CultureInfo.InvariantCulture));
                        var setResult16 = Set(McpResourceId.FlowVersion, evaluatedValue13, evaluatedValue14, ("track", segments[4]), evaluatedValue15, out match);
                        return setResult16;
                    }
                }
            }
        }
        else if (collection == McpResourceSegments.Libraries)
        {
            if (segments.Length == 1)
            {
                var setResult17 = Set(McpResourceId.Library, ("libraryId", segments[0]), out match);
                return setResult17;
            }
        }
        else if (collection == McpResourceSegments.LibraryFamilies)
        {
            if (segments.Length == 1)
            {
                var setResult18 = Set(McpResourceId.LibraryFamily, ("familyId", segments[0]), out match);
                return setResult18;
            }
        }
        else if (collection == McpResourceSegments.McpPreviews && segments.Length == 1 && Guid.TryParse(segments[0], out var previewId))
        {
            var evaluatedValue27 = ("previewId", previewId.ToString("D"));
            var setResult28 = Set(McpResourceId.Preview, evaluatedValue27, out match);
            return setResult28;
        }
        else if (collection == McpResourceSegments.Runs)
        {
            if (segments.Length == 1 && Guid.TryParse(segments[0], out var runId))
            {
                var evaluatedValue19 = ("runId", runId.ToString("D"));
                var setResult20 = Set(McpResourceId.Run, evaluatedValue19, out match);
                return setResult20;
            }

            if (segments.Length == 2 && Guid.TryParse(segments[0], out runId) && Equals(segments[1], McpResourceSegments.Workpieces))
            {
                var evaluatedValue21 = ("runId", runId.ToString("D"));
                var setResult22 = Set(McpResourceId.RunWorkpieces, evaluatedValue21, out match);
                return setResult22;
            }

            if (segments.Length == 3 && Guid.TryParse(segments[0], out runId) && Equals(segments[1], McpResourceSegments.Workpieces))
            {
                var evaluatedValue23 = ("runId", runId.ToString("D"));
                var setResult24 = Set(McpResourceId.RunWorkpiece, evaluatedValue23, ("workpieceId", segments[2]), out match);
                return setResult24;
            }
        }
        else if (collection == McpResourceSegments.DebugSessions && segments.Length == 1 && Guid.TryParse(segments[0], out var sessionId))
        {
            var evaluatedValue25 = ("sessionId", sessionId.ToString("D"));
            var setResult26 = Set(McpResourceId.DebugSession, evaluatedValue25, out match);
            return setResult26;
        }

        return false;
    }

    private static bool Set(McpResourceId id, out McpResourceMatch? match)
    {
        var emptyResult = Array.Empty<(string, string)>();
        var setResult29 = Set(id, emptyResult, out match);
        return setResult29;
    }

    private static bool Set(McpResourceId id, (string Key, string Value) first, out McpResourceMatch? match)
    {
        var setResult30 = Set(id, new[] { first }, out match);
        return setResult30;
    }

    private static bool Set(McpResourceId id, (string Key, string Value) first, (string Key, string Value) second, out McpResourceMatch? match)
    {
        var setResult31 = Set(id, new[] { first, second }, out match);
        return setResult31;
    }

    private static bool Set(McpResourceId id, (string Key, string Value) first, (string Key, string Value) second, (string Key, string Value) third, out McpResourceMatch? match)
    {
        var setResult32 = Set(id, new[] { first, second, third }, out match);
        return setResult32;
    }

    private static bool Set(McpResourceId id, (string Key, string Value) first, (string Key, string Value) second, (string Key, string Value) third, (string Key, string Value) fourth, out McpResourceMatch? match)
    {
        var setResult33 = Set(id, new[] { first, second, third, fourth }, out match);
        return setResult33;
    }

    private static bool Set(McpResourceId id, IEnumerable<(string Key, string Value)> values, out McpResourceMatch? match)
    {
        var toDictionaryResult = values.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        match = new McpResourceMatch(id, toDictionaryResult);
        return true;
    }

    private static bool Equals(string value, string expected)
    {
        var equalsResult = string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        return equalsResult;
    }
}
