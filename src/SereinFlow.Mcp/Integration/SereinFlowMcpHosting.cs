using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Tools;

namespace SereinFlow.Mcp;

/// <summary>Business configuration hooks for generated host entry points.</summary>
[McpHost("AddSereinFlowMcpTools")]
public static partial class SereinFlowMcpHosting
{
    static partial void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<IMcpPrincipalAccessor, McpPrincipalAccessor>();
        services.TryAddScoped<SereinFlowMcpAuditProjection>();
        services.TryAddScoped<IMcpRequestBinder<NormalizedFlowPatchRequest>, FlowPatchRequestBinder>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMcpToolMiddleware, SereinFlowMcpPolicy>());
    }

    static partial void ConfigureJsonOptions(ref JsonSerializerOptions options)
        => options = McpToolSupport.ContractJsonOptions;

    static partial void ConfigureCatalog(McpToolCatalog catalog)
    {
        foreach (var tool in catalog.Tools)
        {
            if (tool.Metadata is not ISereinFlowMcpToolMetadata metadata || metadata.Permissions.Length == 0)
                throw new InvalidOperationException($"SereinFlow tool '{tool.Descriptor.Name}' requires a permission policy.");
        }
    }
}
