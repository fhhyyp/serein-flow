using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;
using SereinFlow.Mcp.Previews;
using SereinFlow.Mcp.Support;

namespace SereinFlow.Mcp;

/// <summary>
/// Business configuration hooks for generated host entry points.
/// 生成的宿主入口使用的业务配置钩子。
/// </summary>
[McpHost("AddSereinFlowMcpTools")]
public static partial class SereinFlowMcpHosting
{
    static partial void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<IMcpPrincipalAccessor, McpPrincipalAccessor>();
        services.TryAddScoped<SereinFlowMcpAuditProjection>();
        services.TryAddScoped<McpProjectQueries>();
        services.TryAddScoped<McpFlowQueries>();
        services.TryAddScoped<McpLibraryQueries>();
        services.TryAddScoped<McpRunQueries>();
        services.TryAddScoped<McpDebugQueries>();
        services.TryAddScoped<McpPreviewReader>();
        services.TryAddScoped<McpResourceAuthorization>();
        services.TryAddScoped<McpFlowValidation>();
        services.TryAddScoped<IMcpRequestBinder<NormalizedFlowPatchRequest>, FlowPatchRequestBinder>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMcpToolMiddleware, SereinFlowMcpPolicy>());
    }

    static partial void ConfigureJsonOptions(ref JsonSerializerOptions options)
        => options = Support.McpJson.ContractJsonOptions;

    static partial void ConfigureCatalog(McpToolCatalog catalog)
    {
        foreach (var tool in catalog.Tools)
        {
            if (tool.Metadata is not ISereinFlowMcpToolMetadata metadata || metadata.Permissions.Length == 0)
                throw new InvalidOperationException($"SereinFlow tool '{tool.Descriptor.Name}' requires a permission policy. SereinFlow 工具必须配置权限策略。");
        }
    }
}
