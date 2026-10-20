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
    private static readonly IReadOnlyList<McpResourceTemplateDescriptor> ResourceTemplates = McpResourceCatalog.Templates;
    private readonly McpResourceReader _resourceReader;
    private readonly McpAiGuidanceProvider _aiGuidanceProvider;
    private readonly McpToolCatalog _toolCatalog;
    private readonly McpToolExecutor _toolExecutor;
    public SereinFlowMcpBackend(McpResourceReader resourceReader, McpAiGuidanceProvider aiGuidanceProvider, McpToolCatalog toolCatalog, McpToolExecutor toolExecutor)
    {
        _resourceReader = resourceReader ?? throw new ArgumentNullException(nameof(resourceReader));
        _aiGuidanceProvider = aiGuidanceProvider ?? throw new ArgumentNullException(nameof(aiGuidanceProvider));
        _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
    }

    public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
    {
        var createResourcesResult = CreateResources();
        var fromResultResult = Task.FromResult<IReadOnlyList<McpResourceDescriptor>>(createResourcesResult);
        return fromResultResult;
    }

    public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
    {
        var fromResultResult1 = Task.FromResult(ResourceTemplates);
        return fromResultResult1;
    }

    public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
    {
        var fromResultResult2 = Task.FromResult(McpPromptCatalog.Descriptors);
        return fromResultResult2;
    }

    public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        var getAsyncResult = McpPromptCatalog.GetAsync(name, arguments, _aiGuidanceProvider, cancellationToken);
        return getAsyncResult;
    }

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var fromResultResult3 = Task.FromResult(_toolCatalog.Descriptors);
        return fromResultResult3;
    }

    public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
    {
        var readAsyncResult = _resourceReader.ReadAsync(uri, cancellationToken);
        return readAsyncResult;
    }

    public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        var executeAsyncResult = _toolExecutor.ExecuteAsync(_toolCatalog, name, arguments, cancellationToken);
        return executeAsyncResult;
    }

    private List<McpResourceDescriptor> CreateResources()
    {
        var getResourceDescriptorsResult = _aiGuidanceProvider.GetResourceDescriptors();
        var resources = getResourceDescriptorsResult.ToList();
        var selectResult = McpResourceCatalog.DirectResources.Select(static resource =>
        {
            var mcpResourceDescriptor = new McpResourceDescriptor(resource.UriTemplate, resource.Name, resource.Description, resource.MimeType);
            return mcpResourceDescriptor;
        });
        resources.AddRange(selectResult);
        return resources;
    }
}
