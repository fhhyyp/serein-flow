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
    private static readonly IReadOnlyList<McpResourceTemplateDescriptor> ResourceTemplates =
        McpResourceCatalog.Templates;

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
        => Task.FromResult<IReadOnlyList<McpResourceDescriptor>>(CreateResources());

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

    private List<McpResourceDescriptor> CreateResources()
    {
        var resources = _aiGuidanceProvider.GetResourceDescriptors().ToList();
        resources.AddRange(McpResourceCatalog.DirectResources.Select(static resource =>
            new McpResourceDescriptor(resource.UriTemplate, resource.Name, resource.Description, resource.MimeType)));
        return resources;
    }
}
