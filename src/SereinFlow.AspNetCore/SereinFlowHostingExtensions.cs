using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Mcp;
using SereinFlow.Worker.Client;

namespace SereinFlow.AspNetCore;

public sealed class SereinFlowWebApiOptions
{
    public bool? EnableSwagger { get; set; }
    public string OpenApiRouteTemplate { get; set; } = "openapi/{documentName}.json";
    public string SwaggerUiRoutePrefix { get; set; } = "swagger";
    public bool AllowAnyOrigin { get; set; }
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class SereinFlowHttpMcpOptions
{
    public string EndpointPath { get; set; } = McpHttpDefaults.EndpointPath;
}

public sealed class SereinFlowHostBuilder
{
    internal bool WebApiEnabled { get; private set; }
    internal bool McpEnabled { get; private set; }
    internal SereinFlowWebApiOptions WebApiOptions { get; } = new();
    internal SereinFlowHttpMcpOptions McpOptions { get; } = new();
    internal IEndpointRouteBuilder? EndpointRouteBuilder { get; set; }

    public SereinFlowHostBuilder UseWebApi(Action<SereinFlowWebApiOptions>? configure = null)
    {
        if (WebApiEnabled)
            throw new InvalidOperationException("SereinFlow Web API has already been enabled.");
        WebApiEnabled = true;
        configure?.Invoke(WebApiOptions);
        return this;
    }

    public SereinFlowHostBuilder UseMcp(Action<SereinFlowHttpMcpOptions>? configure = null)
    {
        if (McpEnabled)
            throw new InvalidOperationException("SereinFlow HTTP MCP has already been enabled.");
        McpEnabled = true;
        configure?.Invoke(McpOptions);
        if (string.IsNullOrWhiteSpace(McpOptions.EndpointPath)
            || !McpOptions.EndpointPath.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("The MCP endpoint path must start with '/'.", nameof(configure));
        return this;
    }
}

public static class SereinFlowHostingExtensions
{
    public static IServiceCollection AddSereinFlow(
        this IServiceCollection services,
        Action<SereinFlowHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(SereinFlowHostBuilder)))
            throw new InvalidOperationException("AddSereinFlow can only be called once per host.");

        var options = new SereinFlowHostBuilder();
        configure(options);
        if (!options.WebApiEnabled && !options.McpEnabled)
            throw new InvalidOperationException("Enable UseWebApi() or UseMcp() in AddSereinFlow.");

        services.AddSingleton(options);
        services.AddSereinFlowStorage();
        services.AddSereinFlowApplication();
        services.AddSignalR();
        services.AddCors();
        services.AddHostedService<SereinFlowInitializationService>();
        if (options.WebApiEnabled)
        {
            services.AddProblemDetails();
            services.AddSereinFlowWebApi();
            services.AddSereinFlowApiDocumentation();
            services.AddOptions<CorsOptions>().Configure<IConfiguration>((cors, configuration) =>
            {
                cors.AddPolicy("sereinflow-webapi", policy =>
                {
                    policy.AllowAnyHeader().AllowAnyMethod();
                    if (options.WebApiOptions.AllowAnyOrigin)
                        policy.AllowAnyOrigin();
                    else
                    {
                        var origins = options.WebApiOptions.AllowedOrigins.Length > 0
                            ? options.WebApiOptions.AllowedOrigins
                            : configuration.GetSection("SereinFlow:Api:Cors:AllowedOrigins").Get<string[]>() ?? [];
                        if (origins.Length > 0)
                            policy.WithOrigins(origins);
                        else
                            policy.SetIsOriginAllowed(static _ => false);
                    }
                });
            });
            services.Configure<FormOptions>(form =>
                form.MultipartBodyLengthLimit = FileUploadLimits.GetApiRequestBodyLimit(
                    FileUploadLimits.MaximumMaxFileSizeBytes));
        }
        if (options.McpEnabled)
            services.AddSereinFlowMcp();

        services.AddOptions<KestrelServerOptions>().Configure<IConfiguration>((kestrel, configuration) =>
        {
            var required = RequiredRequestBodyLimit(options, configuration);
            // Kestrel's built-in default is 30,000,000 bytes. Preserve an explicit
            // larger or unlimited host limit instead of replacing it.
            if (kestrel.Limits.MaxRequestBodySize == 30_000_000)
                kestrel.Limits.MaxRequestBodySize = required;
        });
        services.AddOptions<KestrelServerOptions>().PostConfigure<IConfiguration>((kestrel, configuration) =>
        {
            var required = RequiredRequestBodyLimit(options, configuration);
            if (kestrel.Limits.MaxRequestBodySize is long current && current < required)
                throw new InvalidOperationException(
                    $"Kestrel MaxRequestBodySize ({current}) is below SereinFlow's required upload limit ({required}).");
        });
        services.AddSereinFlowExecution();
        return services;
    }

    private static long RequiredRequestBodyLimit(SereinFlowHostBuilder options, IConfiguration configuration)
    {
        var mcpMax = options.McpEnabled
            ? SereinFlowMcpOptions.FromConfiguration(configuration).MaxRequestBytes
            : 0;
        return Math.Max(
            options.WebApiEnabled
                ? FileUploadLimits.GetApiRequestBodyLimit(FileUploadLimits.MaximumMaxFileSizeBytes)
                : 0,
            options.McpEnabled
                ? FileUploadLimits.GetMcpRequestBodyLimit(FileUploadLimits.MaximumMaxFileSizeBytes, mcpMax)
                : 0);
    }

    public static WebApplication UseSereinFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var properties = ((IApplicationBuilder)app).Properties;
        if (properties.ContainsKey("SereinFlow:MiddlewareInstalled"))
            throw new InvalidOperationException("UseSereinFlow can only be called once per host.");
        properties["SereinFlow:MiddlewareInstalled"] = true;
        var options = app.Services.GetRequiredService<SereinFlowHostBuilder>();
        if (options.WebApiEnabled)
        {
            var swagger = options.WebApiOptions.EnableSwagger
                ?? app.Configuration.GetValue<bool?>("SereinFlow:ApiDocumentation:Enabled")
                ?? app.Environment.IsDevelopment();
            if (swagger)
                app.UseSereinFlowApiDocumentation(
                    true,
                    options.WebApiOptions.OpenApiRouteTemplate,
                    options.WebApiOptions.SwaggerUiRoutePrefix);
        }
        app.UseCors();
        return app;
    }

    public static WebApplication MapSereinFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var properties = ((IApplicationBuilder)app).Properties;
        if (properties.ContainsKey("SereinFlow:EndpointsMapped"))
            throw new InvalidOperationException("MapSereinFlow can only be called once per host.");
        properties["SereinFlow:EndpointsMapped"] = true;
        var options = app.Services.GetRequiredService<SereinFlowHostBuilder>();
        options.EndpointRouteBuilder = app;
        if (options.WebApiEnabled)
        {
            app.MapControllers();
            app.MapHub<RunEventsHub>("/hubs/runs").RequireCors("sereinflow-webapi");
            app.MapHub<WorkspaceEventsHub>("/hubs/workspace").RequireCors("sereinflow-webapi");
        }
        if (options.McpEnabled)
            app.MapSereinFlowMcp(options.McpOptions.EndpointPath);
        return app;
    }
}

internal sealed class SereinFlowInitializationService(
    SqliteDatabase database,
    SupervisorWorkerRunClient workerClient,
    IOptions<KestrelServerOptions> kestrelOptions,
    IServiceProvider services,
    SereinFlowHostBuilder options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateRoutes(options.EndpointRouteBuilder);
        _ = database;
        _ = workerClient;
        _ = kestrelOptions.Value;
        if (options.McpEnabled)
            _ = services.GetRequiredService<McpAiGuidanceProvider>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void ValidateRoutes(IEndpointRouteBuilder? endpoints)
    {
        if (endpoints is null)
            throw new InvalidOperationException("MapSereinFlow() must be called before SereinFlow starts.");

        var routes = endpoints.DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Endpoint: endpoint,
                Action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>(),
                Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods))
            .ToArray();

        foreach (var route in routes.Where(static route =>
                     route.Action?.ControllerTypeInfo.Assembly == typeof(AssemblyMarker).Assembly))
        {
            foreach (var other in routes)
            {
                if (ReferenceEquals(route.Endpoint, other.Endpoint)
                    || !string.Equals(route.Endpoint.RoutePattern.RawText?.TrimStart('/'),
                        other.Endpoint.RoutePattern.RawText?.TrimStart('/'),
                        StringComparison.OrdinalIgnoreCase)
                    || route.Methods is not null && other.Methods is not null
                        && !route.Methods.Intersect(other.Methods, StringComparer.OrdinalIgnoreCase).Any())
                    continue;

                throw new InvalidOperationException(
                    $"SereinFlow route conflict at '{route.Endpoint.RoutePattern.RawText}'. " +
                    $"Endpoints '{route.Endpoint.DisplayName}' and '{other.Endpoint.DisplayName}' overlap.");
            }
        }
    }
}
