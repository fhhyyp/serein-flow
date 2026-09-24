using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Api;

public static class SereinFlowWebApiExtensions
{
    public static IServiceCollection AddSereinFlowWebApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        services.AddControllers(options =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
            .AddApplicationPart(typeof(AssemblyMarker).Assembly);
        services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);
        services.AddSingleton<IWorkspaceChangePublisher, WorkspaceChangePublisher>();
        services.AddScoped<SereinFlowApiAuthorizationService>();
        services.AddScoped<SereinFlowRestAuthorizationFilter>();
        services.AddScoped<SereinFlowApiExceptionFilter>();
        services.TryAddScoped<McpSecurityService>(provider => new McpSecurityService(
            provider.GetRequiredService<IMcpApiKeyStore>(),
            provider.GetRequiredService<IConfiguration>()["SereinFlow:Mcp:BootstrapAdminKey"]));
        return services;
    }

    public static IServiceCollection AddSereinFlowApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
            options.SwaggerDoc("v1", new()
            {
                Title = "SereinFlow API",
                Version = "v1",
                Description = "SereinFlow REST API. MCP JSON-RPC and SignalR transports are documented separately.",
            }));
        return services;
    }

    public static WebApplication UseSereinFlowApiDocumentation(
        this WebApplication app,
        bool? enabledOverride = null,
        string routeTemplate = "openapi/{documentName}.json",
        string uiRoutePrefix = "swagger")
    {
        var enabled = enabledOverride ?? app.Configuration.GetValue<bool?>("SereinFlow:ApiDocumentation:Enabled")
            ?? app.Environment.IsDevelopment();
        if (!enabled)
            return app;

        if (string.IsNullOrWhiteSpace(routeTemplate)
            || !routeTemplate.Contains("{documentName}", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(uiRoutePrefix))
            throw new ArgumentException("A Swagger route template with {documentName} and a UI route prefix are required.");

        app.UseSwagger(options => options.RouteTemplate = routeTemplate.TrimStart('/'));
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = uiRoutePrefix.Trim('/');
            options.SwaggerEndpoint("/" + routeTemplate.Trim('/').Replace("{documentName}", "v1", StringComparison.Ordinal), "SereinFlow API v1");
        });
        return app;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Converters.Insert(0, new McpPermissionJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
