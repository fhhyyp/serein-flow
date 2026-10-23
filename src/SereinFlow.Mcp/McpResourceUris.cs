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
/// 规范的 MCP 资源 URI 集合；这些值属于传输契约，未经兼容性决策不得更改。
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
    {
        return $"{Projects}/{projectId:D}/flows/{flowId:D}/versions/{Uri.EscapeDataString(track)}";
    }

    public static string FlowVersion(Guid projectId, Guid flowId, string track, long version)
    {
        return $"{Projects}/{projectId:D}/flows/{flowId:D}/versions/{Uri.EscapeDataString(track)}/{version}";
    }

    public static string Preview(Guid previewId) => $"{Scheme}://mcp-previews/{previewId:D}";
    public static string Library(string libraryId)
    {
        return $"{Libraries}/{Uri.EscapeDataString(libraryId)}";
    }

    public static string LibraryFamily(string familyId)
    {
        return $"{LibraryFamilies}/{Uri.EscapeDataString(familyId)}";
    }

    public static string ProjectLibraries(Guid projectId) => $"{Projects}/{projectId:D}/libraries";
    public static string LibraryUpgrade(Guid projectId, Guid upgradeId) => $"{Projects}/{projectId:D}/library-upgrades/{upgradeId:D}";
    public static string Run(Guid runId) => $"{Runs}/{runId:D}";
    public static string RunWorkpieces(Guid runId) => $"{Runs}/{runId:D}/workpieces";
    public static string RunWorkpiece(Guid runId, string workpieceId)
    {
        return $"{Runs}/{runId:D}/workpieces/{Uri.EscapeDataString(workpieceId)}";
    }

    public static string DebugSession(Guid sessionId) => $"{DebugSessions}/{sessionId:D}";
}

/// <summary>
/// Internal identity for a parsed MCP resource. The enum is deliberately not
/// serialized; clients always see the URI strings above.
/// 解析后的 MCP 资源内部标识；该枚举不参与序列化，客户端始终看到上述 URI 字符串。
/// </summary>
public enum McpResourceId
{
    /// <summary>
    /// Indicates the projects value.
    /// 表示“Projects”这一取值。
    /// </summary>
    Projects,
    /// <summary>
    /// Indicates the archived projects value.
    /// 表示“已归档Projects”这一取值。
    /// </summary>
    ArchivedProjects,
    /// <summary>
    /// Indicates the libraries value.
    /// 表示“Libraries”这一取值。
    /// </summary>
    Libraries,
    /// <summary>
    /// Indicates the archived libraries value.
    /// 表示“已归档Libraries”这一取值。
    /// </summary>
    ArchivedLibraries,
    /// <summary>
    /// Indicates the library families value.
    /// 表示“库Families”这一取值。
    /// </summary>
    LibraryFamilies,
    /// <summary>
    /// Indicates the project value.
    /// 表示“项目”这一取值。
    /// </summary>
    Project,
    /// <summary>
    /// Indicates the flow topology value.
    /// 表示“流程Topology”这一取值。
    /// </summary>
    FlowTopology,
    /// <summary>
    /// Indicates the flow versions value.
    /// 表示“流程Versions”这一取值。
    /// </summary>
    FlowVersions,
    /// <summary>
    /// Indicates the flow version value.
    /// 表示“流程Version”这一取值。
    /// </summary>
    FlowVersion,
    /// <summary>
    /// Indicates the preview value.
    /// 表示“Preview”这一取值。
    /// </summary>
    Preview,
    /// <summary>
    /// Indicates the library value.
    /// 表示“库”这一取值。
    /// </summary>
    Library,
    /// <summary>
    /// Indicates the library family value.
    /// 表示“库Family”这一取值。
    /// </summary>
    LibraryFamily,
    /// <summary>
    /// Indicates the project libraries value.
    /// 表示“项目Libraries”这一取值。
    /// </summary>
    ProjectLibraries,
    /// <summary>
    /// Indicates the library upgrade value.
    /// 表示“库Upgrade”这一取值。
    /// </summary>
    LibraryUpgrade,
    /// <summary>
    /// Indicates the runs value.
    /// 表示“Runs”这一取值。
    /// </summary>
    Runs,
    /// <summary>
    /// Indicates the debug sessions value.
    /// 表示“调试Sessions”这一取值。
    /// </summary>
    DebugSessions,
    /// <summary>
    /// Indicates the run value.
    /// 表示“运行”这一取值。
    /// </summary>
    Run,
    /// <summary>
    /// Indicates the run workpieces value.
    /// 表示“运行Workpieces”这一取值。
    /// </summary>
    RunWorkpieces,
    /// <summary>
    /// Indicates the run workpiece value.
    /// 表示“运行Workpiece”这一取值。
    /// </summary>
    RunWorkpiece,
    /// <summary>
    /// Indicates the debug session value.
    /// 表示“调试Session”这一取值。
    /// </summary>
    DebugSession
}

/// <summary>
/// Represents MCP resource definition data used by SereinFlow.
/// 表示 SereinFlow 使用的MCP资源定义数据。
/// </summary>
public sealed record McpResourceDefinition(McpResourceId Id, string UriTemplate, string Name, string Description, bool IsTemplate, string MimeType = "application/json")
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
            {
                break;
            }

            var end = template.IndexOf('}', start + 1);
            if (end <= start + 1)
            {
                throw new InvalidOperationException($"Invalid MCP resource URI template '{template}'. MCP 资源 URI 模板无效。");
            }

            names.Add(template[(start + 1)..end]);
            offset = end + 1;
        }

        return names;
    }
}

public static class McpResourceCatalog
{
    private static readonly IReadOnlyList<McpResourceDefinition> AllDefinitions =
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
        new(McpResourceId.DebugSession, McpResourceUris.DebugSessionTemplate, "debug session", "A structured debug session state", true),
        new(McpResourceId.Projects, McpResourceUris.Projects, "projects", "Non-archived SereinFlow project summaries", false),
        new(McpResourceId.ArchivedProjects, McpResourceUris.ArchivedProjects, "archived-projects", "Archived SereinFlow project summaries", false),
        new(McpResourceId.Libraries, McpResourceUris.Libraries, "libraries", "Available SereinFlow library artifacts", false),
        new(McpResourceId.ArchivedLibraries, McpResourceUris.ArchivedLibraries, "archived-libraries", "Archived SereinFlow library artifacts", false),
        new(McpResourceId.LibraryFamilies, McpResourceUris.LibraryFamilies, "library-families", "SereinFlow library families and immutable artifact versions", false),
        new(McpResourceId.Runs, McpResourceUris.Runs, "runs", "Bounded SereinFlow run summaries", false),
        new(McpResourceId.DebugSessions, McpResourceUris.DebugSessions, "debug-sessions", "Bounded active debug session summaries", false)
    ];
    public static IReadOnlyList<McpResourceDefinition> Definitions { get; } = AllDefinitions
        .Where(static definition => definition.IsTemplate)
        .ToArray();
    public static IReadOnlyList<McpResourceDefinition> DirectResources { get; } = AllDefinitions
        .Where(static definition => !definition.IsTemplate)
        .ToArray();
    public static IReadOnlyList<McpResourceTemplateDescriptor> Templates { get; } = Definitions
        .Select(static definition => new McpResourceTemplateDescriptor(definition.UriTemplate, definition.Name, definition.Description, definition.MimeType))
        .ToArray();
    private static readonly Dictionary<McpResourceId, McpResourceDefinition> ById =
        AllDefinitions.ToDictionary(static definition => definition.Id);

    static McpResourceCatalog()
    {
        if (AllDefinitions
            .Select(static definition => definition.UriTemplate)
            .Distinct(StringComparer.Ordinal)
            .Count() != AllDefinitions.Count)
        {
            throw new InvalidOperationException("MCP resource URI templates must be unique. MCP 资源 URI 模板不能重复。");
        }

        if (AllDefinitions.Any(static definition => definition.IsTemplate != (definition.ParameterNames.Count > 0)))
        {
            throw new InvalidOperationException("MCP resource direct/template metadata is inconsistent. MCP 资源的直接注册与模板元数据不一致。");
        }
    }

    public static bool TryGet(McpResourceId id, out McpResourceDefinition? definition) => ById.TryGetValue(id, out definition);
}
