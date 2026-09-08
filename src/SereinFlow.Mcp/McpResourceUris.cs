namespace SereinFlow.Mcp;

internal static class McpResourceSegments
{
    public const string Projects = "projects";
    public const string ArchivedProjects = "archived-projects";
    public const string Libraries = "libraries";
    public const string ArchivedLibraries = "archived-libraries";
    public const string LibraryFamilies = "library-families";
    public const string McpPreviews = "mcp-previews";
    public const string Runs = "runs";
    public const string DebugSessions = "debug-sessions";
    public const string Flows = "flows";
    public const string Topology = "topology";
    public const string Versions = "versions";
    public const string LibraryUpgrades = "library-upgrades";
    public const string Workpieces = "workpieces";
}

/// <summary>
/// Canonical MCP resource URI vocabulary. These values are part of the wire
/// contract and must not be changed without a compatibility decision.
/// </summary>
public static class McpResourceUris
{
    public const string Scheme = "sereinflow";
    private const string Root = Scheme + "://";
    private const string ProjectsRoot = Root + McpResourceSegments.Projects;
    private const string RunsRoot = Root + McpResourceSegments.Runs;
    private const string LibrariesRoot = Root + McpResourceSegments.Libraries;
    private const string LibraryFamiliesRoot = Root + McpResourceSegments.LibraryFamilies;
    private const string DebugSessionsRoot = Root + McpResourceSegments.DebugSessions;

    public const string Projects = ProjectsRoot;
    public const string ArchivedProjects = Root + McpResourceSegments.ArchivedProjects;
    public const string Libraries = LibrariesRoot;
    public const string ArchivedLibraries = Root + McpResourceSegments.ArchivedLibraries;
    public const string LibraryFamilies = LibraryFamiliesRoot;
    public const string Runs = RunsRoot;
    public const string DebugSessions = DebugSessionsRoot;

    public const string ProjectTemplate = ProjectsRoot + "/{projectId}";
    public const string FlowTopologyTemplate = ProjectsRoot + "/{projectId}/flows/{flowId}/topology";
    public const string FlowVersionsTemplate = ProjectsRoot + "/{projectId}/flows/{flowId}/versions/{track}";
    public const string FlowVersionTemplate = ProjectsRoot + "/{projectId}/flows/{flowId}/versions/{track}/{version}";
    public const string PreviewTemplate = Root + McpResourceSegments.McpPreviews + "/{previewId}";
    public const string LibraryTemplate = LibrariesRoot + "/{libraryId}";
    public const string LibraryFamilyTemplate = LibraryFamiliesRoot + "/{familyId}";
    public const string ProjectLibrariesTemplate = ProjectsRoot + "/{projectId}/libraries";
    public const string LibraryUpgradeTemplate = ProjectsRoot + "/{projectId}/library-upgrades/{upgradeId}";
    public const string RunTemplate = RunsRoot + "/{runId}";
    public const string RunWorkpiecesTemplate = RunsRoot + "/{runId}/workpieces";
    public const string RunWorkpieceTemplate = RunsRoot + "/{runId}/workpieces/{workpieceId}";
    public const string DebugSessionTemplate = DebugSessionsRoot + "/{sessionId}";

    public static string Project(Guid projectId) => $"{Projects}/{projectId:D}";
    public static string FlowTopology(Guid projectId, Guid flowId) => $"{Projects}/{projectId:D}/flows/{flowId:D}/topology";
    public static string FlowVersions(Guid projectId, Guid flowId, string track)
        => $"{Projects}/{projectId:D}/flows/{flowId:D}/versions/{Uri.EscapeDataString(track)}";
    public static string FlowVersion(Guid projectId, Guid flowId, string track, long version)
        => $"{Projects}/{projectId:D}/flows/{flowId:D}/versions/{Uri.EscapeDataString(track)}/{version}";
    public static string Preview(Guid previewId) => $"{Scheme}://mcp-previews/{previewId:D}";
    public static string Library(string libraryId) => $"{Libraries}/{Uri.EscapeDataString(libraryId)}";
    public static string LibraryFamily(string familyId) => $"{LibraryFamilies}/{Uri.EscapeDataString(familyId)}";
    public static string ProjectLibraries(Guid projectId) => $"{Projects}/{projectId:D}/libraries";
    public static string LibraryUpgrade(Guid projectId, Guid upgradeId)
        => $"{Projects}/{projectId:D}/library-upgrades/{upgradeId:D}";
    public static string Run(Guid runId) => $"{Runs}/{runId:D}";
    public static string RunWorkpieces(Guid runId) => $"{Runs}/{runId:D}/workpieces";
    public static string RunWorkpiece(Guid runId, string workpieceId)
        => $"{Runs}/{runId:D}/workpieces/{Uri.EscapeDataString(workpieceId)}";
    public static string DebugSession(Guid sessionId) => $"{DebugSessions}/{sessionId:D}";
}

/// <summary>
/// Internal identity for a parsed MCP resource. The enum is deliberately not
/// serialized; clients always see the URI strings above.
/// </summary>
public enum McpResourceId
{
    Projects,
    ArchivedProjects,
    Libraries,
    ArchivedLibraries,
    LibraryFamilies,
    Project,
    FlowTopology,
    FlowVersions,
    FlowVersion,
    Preview,
    Library,
    LibraryFamily,
    ProjectLibraries,
    LibraryUpgrade,
    Runs,
    DebugSessions,
    Run,
    RunWorkpieces,
    RunWorkpiece,
    DebugSession
}

public sealed record McpResourceDefinition(
    McpResourceId Id,
    string UriTemplate,
    string Name,
    string Description,
    bool IsTemplate,
    string MimeType = "application/json")
{
    public IReadOnlyList<string> ParameterNames { get; } = ExtractParameterNames(UriTemplate);

    private static List<string> ExtractParameterNames(string template)
    {
        var names = new List<string>();
        var offset = 0;
        while (offset < template.Length)
        {
            var start = template.IndexOf('{', offset);
            if (start < 0)
                break;
            var end = template.IndexOf('}', start + 1);
            if (end <= start + 1)
                throw new InvalidOperationException($"Invalid MCP resource URI template '{template}'.");
            names.Add(template[(start + 1)..end]);
            offset = end + 1;
        }

        return names;
    }
}

public static class McpResourceCatalog
{
    public static IReadOnlyList<McpResourceDefinition> Definitions { get; } =
    [
        new(McpResourceId.Project, McpResourceUris.ProjectTemplate, "project", "One SereinFlow project", true),
        new(McpResourceId.FlowTopology, McpResourceUris.FlowTopologyTemplate, "flow topology", "A development flow topology", true),
        new(McpResourceId.FlowVersions, McpResourceUris.FlowVersionsTemplate, "flow versions", "Flow version history for one track", true),
        new(McpResourceId.FlowVersion, McpResourceUris.FlowVersionTemplate, "flow version", "One immutable flow version", true),
        new(McpResourceId.Preview, McpResourceUris.PreviewTemplate, "MCP preview", "One pending or completed MCP mutation preview", true),
        new(McpResourceId.Library, McpResourceUris.LibraryTemplate, "library", "One SereinFlow library contract", true),
        new(McpResourceId.LibraryFamily, McpResourceUris.LibraryFamilyTemplate, "library family", "One SereinFlow library family and its artifacts", true),
        new(McpResourceId.ProjectLibraries, McpResourceUris.ProjectLibrariesTemplate, "project libraries", "Library artifacts referenced by one project", true),
        new(McpResourceId.LibraryUpgrade, McpResourceUris.LibraryUpgradeTemplate, "library upgrade", "One persisted project library upgrade plan", true),
        new(McpResourceId.Run, McpResourceUris.RunTemplate, "run inspection", "A bounded run timeline, node output and debug inspection", true),
        new(McpResourceId.RunWorkpieces, McpResourceUris.RunWorkpiecesTemplate, "run workpieces", "Metadata for image and file workpieces uploaded by a run", true),
        new(McpResourceId.RunWorkpiece, McpResourceUris.RunWorkpieceTemplate, "run workpiece", "One image or file workpiece uploaded by a run", true),
        new(McpResourceId.DebugSession, McpResourceUris.DebugSessionTemplate, "debug session", "A structured debug session state", true)
    ];

    public static IReadOnlyList<McpResourceDefinition> DirectResources { get; } =
    [
        new(McpResourceId.Projects, McpResourceUris.Projects, "projects", "Non-archived SereinFlow project summaries", false),
        new(McpResourceId.ArchivedProjects, McpResourceUris.ArchivedProjects, "archived-projects", "Archived SereinFlow project summaries", false),
        new(McpResourceId.Libraries, McpResourceUris.Libraries, "libraries", "Available SereinFlow library artifacts", false),
        new(McpResourceId.ArchivedLibraries, McpResourceUris.ArchivedLibraries, "archived-libraries", "Archived SereinFlow library artifacts", false),
        new(McpResourceId.LibraryFamilies, McpResourceUris.LibraryFamilies, "library-families", "SereinFlow library families and immutable artifact versions", false),
        new(McpResourceId.Runs, McpResourceUris.Runs, "runs", "Bounded SereinFlow run summaries", false),
        new(McpResourceId.DebugSessions, McpResourceUris.DebugSessions, "debug-sessions", "Bounded active debug session summaries", false)
    ];

    public static IReadOnlyList<McpResourceTemplateDescriptor> Templates { get; } =
        Definitions.Select(static definition => new McpResourceTemplateDescriptor(
            definition.UriTemplate,
            definition.Name,
            definition.Description,
            definition.MimeType)).ToArray();

    private static Dictionary<McpResourceId, McpResourceDefinition> ById { get; } =
        Definitions.Concat(DirectResources)
            .ToDictionary(static definition => definition.Id);

    static McpResourceCatalog()
    {
        var all = Definitions.Concat(DirectResources).ToArray();
        if (all.Select(static definition => definition.UriTemplate)
            .Distinct(StringComparer.Ordinal)
            .Count() != all.Length)
        {
            throw new InvalidOperationException("MCP resource URI templates must be unique.");
        }

        if (Definitions.Any(static definition => !definition.IsTemplate || definition.ParameterNames.Count == 0)
            || DirectResources.Any(static definition => definition.IsTemplate || definition.ParameterNames.Count != 0))
        {
            throw new InvalidOperationException("MCP resource direct/template metadata is inconsistent.");
        }
    }

    public static bool TryGet(McpResourceId id, out McpResourceDefinition? definition)
        => ById.TryGetValue(id, out definition);
}
