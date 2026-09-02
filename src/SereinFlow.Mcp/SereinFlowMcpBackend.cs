using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

/// <summary>
/// MCP facade for resources and the shared tool execution pipeline.
/// Tool schemas and handlers live in the dedicated tool catalogue.
/// MCP facade 仅保留资源读取和统一工具执行；工具合同与处理器位于独立目录中。
/// </summary>
public sealed class SereinFlowMcpBackend : ISereinFlowMcpBackend
{
    private static readonly IReadOnlyList<McpResourceDescriptor> Resources = CreateResources();

    private static readonly IReadOnlyList<McpResourceTemplateDescriptor> ResourceTemplates =
    [
        new("sereinflow://projects/{projectId}", "project", "One SereinFlow project"),
        new("sereinflow://projects/{projectId}/flows/{flowId}/topology", "flow topology", "A development flow topology"),
        new("sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}", "flow versions", "Flow version history for one track"),
        new("sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}", "flow version", "One immutable flow version"),
        new("sereinflow://mcp-previews/{previewId}", "MCP preview", "One pending or completed MCP mutation preview"),
        new("sereinflow://libraries/{libraryId}", "library", "One SereinFlow library contract"),
        new("sereinflow://library-families/{familyId}", "library family", "One SereinFlow library family and its artifacts"),
        new("sereinflow://projects/{projectId}/libraries", "project libraries", "Library artifacts referenced by one project"),
        new("sereinflow://projects/{projectId}/library-upgrades/{upgradeId}", "library upgrade", "One persisted project library upgrade plan"),
        new("sereinflow://runs", "runs", "Bounded SereinFlow run summaries"),
        new("sereinflow://debug-sessions", "debug sessions", "Bounded active debug session summaries"),
        new("sereinflow://runs/{runId}", "run inspection", "A bounded run timeline, node output and debug inspection"),
        new("sereinflow://debug-sessions/{sessionId}", "debug session", "A structured debug session state")
    ];

    private readonly McpResourceReader _resourceReader;
    private readonly McpAiGuidanceProvider _aiGuidanceProvider;
    private readonly McpToolCatalog _toolCatalog;
    private readonly McpToolExecutor _toolExecutor;

    public SereinFlowMcpBackend(
        McpResourceReader resourceReader,
        McpAiGuidanceProvider aiGuidanceProvider,
        McpToolCatalog toolCatalog,
        McpToolExecutor toolExecutor)
    {
        _resourceReader = resourceReader ?? throw new ArgumentNullException(nameof(resourceReader));
        _aiGuidanceProvider = aiGuidanceProvider ?? throw new ArgumentNullException(nameof(aiGuidanceProvider));
        _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
    }

    public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
        => Task.FromResult(Resources);

    public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
        => Task.FromResult(ResourceTemplates);

    public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
        => Task.FromResult(McpPromptCatalog.Descriptors);

    public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        => McpPromptCatalog.GetAsync(name, arguments, _aiGuidanceProvider, cancellationToken);

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        => Task.FromResult(_toolCatalog.Descriptors);

    public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        => _resourceReader.ReadAsync(uri, cancellationToken);

    public Task<McpToolCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
        => _toolExecutor.ExecuteAsync(_toolCatalog, name, arguments, cancellationToken);

    private static List<McpResourceDescriptor> CreateResources()
    {
        var resources = new List<McpResourceDescriptor>
        {
            new(McpAiGuidance.ResourceUri, McpAiGuidance.ResourceName, "Compact capability index for SereinFlow AI guidance", McpAiGuidance.MimeType),
            new(McpAiGuidance.SereinFlowResourceUri, "sereinflow", "SereinFlow capability index", McpAiGuidance.MimeType),
            new(McpAiGuidance.SereinLangResourceUri, "sereinlang", "SereinLang capability index", McpAiGuidance.MimeType),
            new(McpAiGuidance.LibraryPackageResourceUri, "sereinflow-library-package", "Library package capability index", McpAiGuidance.MimeType)
        };

        resources.AddRange(McpAiGuidance.ModuleResources.Select(static resource =>
            new McpResourceDescriptor(
                resource.Uri,
                resource.Name,
                resource.Description,
                McpAiGuidance.MimeType)));
        resources.AddRange(
        [
            new("sereinflow://projects", "projects", "Non-archived SereinFlow project summaries"),
            new("sereinflow://archived-projects", "archived-projects", "Archived SereinFlow project summaries"),
            new("sereinflow://libraries", "libraries", "Available SereinFlow library artifacts"),
            new("sereinflow://archived-libraries", "archived-libraries", "Archived SereinFlow library artifacts"),
            new("sereinflow://library-families", "library-families", "SereinFlow library families and immutable artifact versions"),
            new("sereinflow://runs", "runs", "Bounded SereinFlow run summaries"),
            new("sereinflow://debug-sessions", "debug-sessions", "Bounded active debug session summaries")
        ]);
        return resources;
    }
}
