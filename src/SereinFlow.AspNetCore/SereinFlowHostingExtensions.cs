using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
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
    public IServiceCollection Services { get; internal set; } = null!;
    internal bool WebApiEnabled { get; private set; }
    internal bool McpEnabled { get; private set; }
    internal SereinFlowWebApiOptions WebApiOptions { get; } = new();
    internal SereinFlowHttpMcpOptions McpOptions { get; } = new();
    internal IEndpointRouteBuilder? EndpointRouteBuilder { get; set; }

    /// <summary>
    /// Enables and configures the SereinFlow Web API.
    /// 启用并配置 SereinFlow Web API。
    /// </summary>
    public SereinFlowHostBuilder UseWebApi(Action<SereinFlowWebApiOptions>? configure = null)
    {
        if (WebApiEnabled)
        {
            throw new InvalidOperationException("SereinFlow Web API has already been enabled. SereinFlow Web API 已启用。");
        }

        WebApiEnabled = true;
        configure?.Invoke(WebApiOptions);
        return this;
    }

    /// <summary>
    /// Enables and configures the SereinFlow HTTP MCP endpoint.
    /// 启用并配置 SereinFlow HTTP MCP 端点。
    /// </summary>
    public SereinFlowHostBuilder UseMcp(Action<SereinFlowHttpMcpOptions>? configure = null)
    {
        if (McpEnabled)
        {
            throw new InvalidOperationException("SereinFlow HTTP MCP has already been enabled. SereinFlow HTTP MCP 已启用。");
        }

        McpEnabled = true;
        configure?.Invoke(McpOptions);
        if (string.IsNullOrWhiteSpace(McpOptions.EndpointPath) || !McpOptions.EndpointPath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("The MCP endpoint path must start with '/'. MCP 端点路径必须以“/”开头。", nameof(configure));
        }

        return this;
    }
}

public static class SereinFlowHostingExtensions
{
    /// <summary>
    /// Registers SereinFlow services and host options.
    /// 注册 SereinFlow 服务和宿主选项。
    /// </summary>
    public static IServiceCollection AddSereinFlow(this IServiceCollection services, Action<SereinFlowHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(SereinFlowHostBuilder)))
        {
            throw new InvalidOperationException("AddSereinFlow can only be called once per host. 每个宿主只能调用一次 AddSereinFlow。");
        }

        var options = new SereinFlowHostBuilder
        {
            Services = services
        };
        configure(options);
        if (!options.WebApiEnabled && !options.McpEnabled)
        {
            throw new InvalidOperationException("Enable UseWebApi() or UseMcp() in AddSereinFlow. 请在 AddSereinFlow 中启用 UseWebApi() 或 UseMcp()。");
        }

        services.AddSingleton(options);
        services.AddDefaultSereinFlowAccess();
        services.AddSereinFlowStorage();
        services.AddSereinFlowApplication();
        services.AddSignalR(options =>
        {
            options.AddFilter<SereinFlowHubSecurity>();
        });
        services.AddAuthorization();
        services.AddCors();
        services.AddHostedService<SereinFlowInitializationService>();
        if (options.WebApiEnabled)
        {
            services.AddProblemDetails();
            services.AddSereinFlowWebApi();
            services.AddSereinFlowApiDocumentation();
            var addOptions = services.AddOptions<CorsOptions>();
            addOptions.Configure<IConfiguration>((cors, configuration) =>
            {
                cors.AddPolicy("sereinflow-webapi", policy =>
                {
                    var allowAnyHeader = policy.AllowAnyHeader();
                    allowAnyHeader.AllowAnyMethod();
                    if (options.WebApiOptions.AllowAnyOrigin)
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        string[] origins;
                        if (options.WebApiOptions.AllowedOrigins.Length > 0)
                        {
                            origins = options.WebApiOptions.AllowedOrigins;
                        }
                        else
                        {
                            var allowedOriginsSection = configuration.GetSection("SereinFlow:Api:Cors:AllowedOrigins");
                            origins = allowedOriginsSection.Get<string[]>() ?? [];
                        }

                        if (origins.Length > 0)
                        {
                            policy.WithOrigins(origins).AllowCredentials();
                        }
                        else
                        {
                            policy.SetIsOriginAllowed(static _ => false);
                        }
                    }
                });
            });
            services.Configure<FormOptions>(form =>
            {
                form.MultipartBodyLengthLimit = FileUploadLimits.GetApiRequestBodyLimit(FileUploadLimits.MaximumMaxFileSizeBytes);
            });
        }

        if (options.McpEnabled)
        {
            services.AddSereinFlowMcp();
        }

        var mappedAddOptions = services.AddOptions<KestrelServerOptions>();
        mappedAddOptions.Configure<IConfiguration>((kestrel, configuration) =>
        {
            var required = RequiredRequestBodyLimit(options, configuration);
            // Kestrel's built-in default is 30,000,000 bytes. Preserve an explicit
            // larger or unlimited host limit instead of replacing it.
            // Kestrel 内置默认限制为 30,000,000 字节；保留宿主显式设置的更大限制或无限制配置。
            if (kestrel.Limits.MaxRequestBodySize == 30_000_000)
            {
                kestrel.Limits.MaxRequestBodySize = required;
            }
        });
        var resolvedAddOptions = services.AddOptions<KestrelServerOptions>();
        resolvedAddOptions.PostConfigure<IConfiguration>((kestrel, configuration) =>
        {
            var required = RequiredRequestBodyLimit(options, configuration);
            if (kestrel.Limits.MaxRequestBodySize is long current && current < required)
            {
                throw new InvalidOperationException($"Kestrel MaxRequestBodySize ({current}) is below SereinFlow's required upload limit ({required}). Kestrel MaxRequestBodySize 小于 SereinFlow 所需的上传限制。");
            }
        });
        services.AddSereinFlowExecution();
        return services;
    }

    private static long RequiredRequestBodyLimit(SereinFlowHostBuilder options, IConfiguration configuration)
    {
        var mcpMax = options.McpEnabled ? SereinFlowMcpOptions.FromConfiguration(configuration).MaxRequestBytes : 0;
        return Math.Max((options.WebApiEnabled ? FileUploadLimits.GetApiRequestBodyLimit(FileUploadLimits.MaximumMaxFileSizeBytes) : 0), (options.McpEnabled ? FileUploadLimits.GetMcpRequestBodyLimit(FileUploadLimits.MaximumMaxFileSizeBytes, mcpMax) : 0));
    }

    /// <summary>
    /// Adds configured SereinFlow middleware to the application.
    /// 将配置好的 SereinFlow 中间件加入应用。
    /// </summary>
    public static WebApplication UseSereinFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var properties = ((IApplicationBuilder)app).Properties;
        if (properties.ContainsKey("SereinFlow:MiddlewareInstalled"))
        {
            throw new InvalidOperationException("UseSereinFlow can only be called once per host. 每个宿主只能调用一次 UseSereinFlow。");
        }

        properties["SereinFlow:MiddlewareInstalled"] = true;
        var sereinFlowHostBuilder = app.Services.GetRequiredService<SereinFlowHostBuilder>();
        if (sereinFlowHostBuilder.WebApiEnabled)
        {
            var swagger = sereinFlowHostBuilder.WebApiOptions.EnableSwagger ?? app.Configuration.GetValue<bool?>("SereinFlow:ApiDocumentation:Enabled") ?? app.Environment.IsDevelopment();
            if (swagger)
            {
                app.UseSereinFlowApiDocumentation(true, sereinFlowHostBuilder.WebApiOptions.OpenApiRouteTemplate, sereinFlowHostBuilder.WebApiOptions.SwaggerUiRoutePrefix);
            }
        }

        var security = app.Services.GetService<SereinFlowHttpSecurityOptions>();
        if (security is null || string.IsNullOrEmpty(security.Mode))
        {
            throw new InvalidOperationException("SereinFlow HTTP security mode is not configured.");
        }

        var serviceProviderIsService = app.Services.GetRequiredService<IServiceProviderIsService>();
        if (!serviceProviderIsService.IsService(typeof(ISereinFlowAuthorizationService)) || !serviceProviderIsService.IsService(typeof(ISereinFlowProjectScopeProvider)))
        {
            throw new InvalidOperationException("SereinFlow HTTP security requires an authorization service and an ISereinFlowProjectScopeProvider.");
        }

        app.UseMiddleware<SereinFlowIdentityMiddleware>();
        app.UseCors();
        return app;
    }

    /// <summary>
    /// Maps SereinFlow API and real-time endpoints.
    /// 映射 SereinFlow API 和实时通信端点。
    /// </summary>
    public static WebApplication MapSereinFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var properties = ((IApplicationBuilder)app).Properties;
        if (properties.ContainsKey("SereinFlow:EndpointsMapped"))
        {
            throw new InvalidOperationException("MapSereinFlow can only be called once per host. 每个宿主只能调用一次 MapSereinFlow。");
        }

        properties["SereinFlow:EndpointsMapped"] = true;
        var sereinFlowHostBuilder = app.Services.GetRequiredService<SereinFlowHostBuilder>();
        sereinFlowHostBuilder.EndpointRouteBuilder = app;
        if (sereinFlowHostBuilder.WebApiEnabled)
        {
            var sereinFlowHttpSecurityOptions = app.Services.GetRequiredService<SereinFlowHttpSecurityOptions>();
            var policy = new AuthorizationPolicyBuilder();
            if (sereinFlowHttpSecurityOptions.AuthenticationScheme is { } scheme)
            {
                policy.AddAuthenticationSchemes(scheme);
            }

            app.MapControllers();
            var hub = app.MapHub<RunEventsHub>("/hubs/runs");
            hub.RequireCors("sereinflow-webapi");
            var mappedHub = app.MapHub<WorkspaceEventsHub>("/hubs/workspace");
            mappedHub.RequireCors("sereinflow-webapi");
            if (sereinFlowHttpSecurityOptions.Mode != "None")
            {
                var authenticated = policy.RequireAuthenticatedUser().Build();
                hub.RequireAuthorization(authenticated);
                mappedHub.RequireAuthorization(authenticated);
            }
        }

        if (sereinFlowHostBuilder.McpEnabled)
        {
            app.MapSereinFlowMcp(sereinFlowHostBuilder.McpOptions.EndpointPath);
        }

        return app;
    }
}

internal sealed class SereinFlowInitializationService(SqliteDatabase database, SupervisorWorkerRunClient workerClient, IOptions<KestrelServerOptions> kestrelOptions, IServiceProvider services, SereinFlowHostBuilder options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateRoutes(options.EndpointRouteBuilder);
        _ = database;
        _ = workerClient;
        _ = kestrelOptions.Value;
        if (options.McpEnabled)
        {
            _ = services.GetRequiredService<McpAiGuidanceProvider>();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private static void ValidateRoutes(IEndpointRouteBuilder? endpoints)
    {
        if (endpoints is null)
        {
            throw new InvalidOperationException("MapSereinFlow() must be called before SereinFlow starts. 必须在 SereinFlow 启动前调用 MapSereinFlow()。");
        }

        var selectMany = endpoints.DataSources.SelectMany(static source => source.Endpoints);
        var ofType = selectMany.OfType<RouteEndpoint>();
        var mappedOfType = ofType.Select(endpoint =>
        {
            return (Endpoint: endpoint, Action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>(), Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
        });
        var routes = mappedOfType.ToArray();
        foreach (var route in routes.Where(static route => route.Action?.ControllerTypeInfo.Assembly == typeof(AssemblyMarker).Assembly))
        {
            foreach (var other in routes)
            {
                if (ReferenceEquals(route.Endpoint, other.Endpoint) || !RoutePatternsMatch() || route.Methods is not null && other.Methods is not null && !HttpMethodsOverlap())
                {
                    continue;
                }

                throw new InvalidOperationException($"SereinFlow route conflict at '{route.Endpoint.RoutePattern.RawText}'. " + $"Endpoints '{route.Endpoint.DisplayName}' and '{other.Endpoint.DisplayName}' overlap. SereinFlow 路由冲突，列出的端点相互重叠。");
                bool RoutePatternsMatch()
                {
                    var trimStart = route.Endpoint.RoutePattern.RawText?.TrimStart('/');
                    var rawText = other.Endpoint.RoutePattern.RawText?.TrimStart('/');
                    return string.Equals(trimStart, rawText, StringComparison.OrdinalIgnoreCase);
                }

                bool HttpMethodsOverlap()
                {
                    var intersect = route.Methods.Intersect(other.Methods, StringComparer.OrdinalIgnoreCase);
                    return intersect.Any();
                }
            }
        }
    }
}
