using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Api;
public static class SereinFlowWebApiExtensions
{
    public static IServiceCollection AddSereinFlowWebApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            ConfigureJson(options.SerializerOptions);
        });
        var addControllersResult = services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        var addJsonOptionsResult = addControllersResult.AddJsonOptions(options =>
        {
            ConfigureJson(options.JsonSerializerOptions);
        });
        addJsonOptionsResult.AddApplicationPart(typeof(AssemblyMarker).Assembly);
        services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
        services.AddSingleton<IWorkspaceChangePublisher, WorkspaceChangePublisher>();
        services.AddScoped<SereinFlowApiAuthorizationService>();
        services.AddScoped<SereinFlowRestAuthorizationFilter>();
        services.AddScoped<SereinFlowApiExceptionFilter>();
        services.TryAddSingleton(provider =>
        {
            var evaluatedValue2 = provider.GetService<McpAiGuidanceProvider>() ?? resolveMcpAiGuidanceProvider();
            var mcpAiGuidanceManagementService = new McpAiGuidanceManagementService(evaluatedValue2);
            return mcpAiGuidanceManagementService;
            global::SereinFlow.Mcp.McpAiGuidanceProvider resolveMcpAiGuidanceProvider()
            {
                var getRequiredServiceResult = provider.GetRequiredService<IConfiguration>();
                var evaluatedValue = provider.GetRequiredService<IHostEnvironment>().ContentRootPath;
                var mcpAiGuidanceProvider = new McpAiGuidanceProvider(getRequiredServiceResult, evaluatedValue);
                return mcpAiGuidanceProvider;
            }
        });
        services.TryAddScoped<McpSecurityService>(provider =>
        {
            var getRequiredServiceResult3 = provider.GetRequiredService<IMcpApiKeyStore>();
            var evaluatedValue4 = provider.GetRequiredService<IConfiguration>()["SereinFlow:Mcp:BootstrapAdminKey"];
            var mcpSecurityService = new McpSecurityService(getRequiredServiceResult3, evaluatedValue4);
            return mcpSecurityService;
        });
        return services;
    }

    public static IServiceCollection AddSereinFlowApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var apiInformation = new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "SereinFlow API",
                Version = "v1",
                Description = "SereinFlow REST API. MCP JSON-RPC and SignalR transports are documented separately."
            };
            options.SwaggerDoc("v1", apiInformation);
        });
        return services;
    }

    public static WebApplication UseSereinFlowApiDocumentation(this WebApplication app, bool? enabledOverride = null, string routeTemplate = "openapi/{documentName}.json", string uiRoutePrefix = "swagger")
    {
        var enabled = enabledOverride ?? app.Configuration.GetValue<bool?>("SereinFlow:ApiDocumentation:Enabled") ?? app.Environment.IsDevelopment();
        if (!enabled)
        {
            return app;
        }

        if (string.IsNullOrWhiteSpace(routeTemplate) || !routeTemplate.Contains("{documentName}", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(uiRoutePrefix))
        {
            throw new ArgumentException("A Swagger route template with {documentName} and a UI route prefix are required.");
        }

        app.UseSwagger(options =>
        {
            options.RouteTemplate = routeTemplate.TrimStart('/');
        });
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = uiRoutePrefix.Trim('/');
            var evaluatedValue7 = "/" + resolveReplaceResult();
            options.SwaggerEndpoint(evaluatedValue7, "SereinFlow API v1");
            string resolveReplaceResult()
            {
                var trimResult = routeTemplate.Trim('/');
                var replaceResult = trimResult.Replace("{documentName}", "v1", StringComparison.Ordinal);
                return replaceResult;
            }
        });
        return app;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        var mcpPermissionJsonConverter = new McpPermissionJsonConverter();
        options.Converters.Insert(0, mcpPermissionJsonConverter);
        var jsonStringEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(jsonStringEnumConverter);
    }
}
