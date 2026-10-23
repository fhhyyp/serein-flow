using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Tools;
using SereinFlow.ScriptAdapter;
using System.Text;

namespace SereinFlow.Mcp;

public static class McpHttpDefaults
{
    public const string EndpointPath = "/mcp";
}

public sealed class SereinFlowMcpOptions
{
    public const string SectionName = "SereinFlow:Mcp";
    public long MaxRequestBytes { get; init; } = 16 * 1024 * 1024;
    public long MaxResponseBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxConcurrentRequests { get; init; } = 16;
    public int MaxRequestsPerMinute { get; init; } = 120;
    public int MaxToolExecutionSeconds { get; init; } = 60;
    public string[] AllowedOrigins { get; init; } = [];
    public McpAiGuidanceOptions AiGuidance { get; init; } = new();

    /// <summary>
    /// Creates MCP options from application configuration.
    /// 根据应用配置创建 MCP 选项。
    /// </summary>
    public static SereinFlowMcpOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new SereinFlowMcpOptions
        {
            MaxRequestBytes = ReadPositiveLong(configuration["SereinFlow:Mcp:Http:MaxRequestBytes"], 16 * 1024 * 1024),
            MaxResponseBytes = ReadPositiveLong(configuration["SereinFlow:Mcp:Http:MaxResponseBytes"], 4 * 1024 * 1024),
            MaxConcurrentRequests = ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxConcurrentRequests"], 16),
            MaxRequestsPerMinute = ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxRequestsPerMinute"], 120),
            MaxToolExecutionSeconds = ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxToolExecutionSeconds"], 60),
            AllowedOrigins = ReadAllowedOrigins(configuration["SereinFlow:Mcp:Http:AllowedOrigins"]),
            AiGuidance = McpAiGuidanceOptions.FromConfiguration(configuration)
        };
    }

    private static long ReadPositiveLong(string? value, long fallback)
    {
        return long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static int ReadPositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static string[] ReadAllowedOrigins(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public static class SereinFlowMcpServiceCollectionExtensions
{
    /// <summary>
    /// Registers MCP services and their backend dependencies.
    /// 注册 MCP 服务及其后端依赖。
    /// </summary>
    public static IServiceCollection AddSereinFlowMcp(this IServiceCollection services, IConfiguration? configuration = null, string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(serviceProvider =>
            SereinFlowMcpOptions.FromConfiguration(configuration ?? serviceProvider.GetRequiredService<IConfiguration>()));
        services.TryAddSingleton<McpAiGuidanceProvider>(serviceProvider =>
        {
            var resolvedConfiguration = configuration ?? serviceProvider.GetRequiredService<IConfiguration>();
            var root = contentRootPath ?? serviceProvider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>()?.ContentRootPath ?? AppContext.BaseDirectory;
            return new McpAiGuidanceProvider(resolvedConfiguration, root);
        });
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((cors, resolvedConfiguration) =>
        {
            var options = SereinFlowMcpOptions.FromConfiguration(configuration ?? resolvedConfiguration);
            cors.AddPolicy("sereinflow-mcp", policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod();
                if (options.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(options.AllowedOrigins).AllowCredentials();
                }
                else
                {
                    policy.SetIsOriginAllowed(static _ => false);
                }
            });
        });
        services.TryAddScoped<ISereinLangCompiler, SereinLangCompiler>();
        services.AddSereinFlowAccess();
        services.TryAddSingleton<IMcpRequestContextAccessor, McpRequestContextAccessor>();
        services.TryAddSingleton<McpHttpSessionRegistry>();
        services.TryAddSingleton<McpRequestLimiter>(serviceProvider =>
        {
            var sereinFlowMcpOptions = serviceProvider.GetRequiredService<SereinFlowMcpOptions>();
            return new McpRequestLimiter(sereinFlowMcpOptions.MaxConcurrentRequests, sereinFlowMcpOptions.MaxRequestsPerMinute);
        });
        services.AddSereinFlowMcpTools();
        services.TryAddSingleton<McpResourceReader>();
        services.TryAddSingleton<McpToolCatalog>(_ => SereinFlowMcpToolCatalogFactory.Create());
        services.TryAddSingleton<ISereinFlowMcpBackend, SereinFlowMcpBackend>();
        services.TryAddSingleton<SereinFlowMcpServer>(serviceProvider =>
        {
            var sereinFlowMcpOptions = serviceProvider.GetRequiredService<SereinFlowMcpOptions>();
            return new SereinFlowMcpServer(
                serviceProvider.GetRequiredService<ISereinFlowMcpBackend>(),
                diagnostics: Console.Error,
                maxRequestBytes: checked((int)sereinFlowMcpOptions.MaxRequestBytes),
                maxResponseBytes: checked((int)sereinFlowMcpOptions.MaxResponseBytes),
                requestContextAccessor: serviceProvider.GetRequiredService<IMcpRequestContextAccessor>(),
                fileUploadSettings: serviceProvider.GetRequiredService<IFileUploadSettings>(),
                aiGuidanceProvider: serviceProvider.GetRequiredService<McpAiGuidanceProvider>());
        });
        return services;
    }
}

public static class SereinFlowMcpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the MCP HTTP endpoint.
    /// 映射 MCP HTTP 端点。
    /// </summary>
    public static IEndpointConventionBuilder MapSereinFlowMcp(this IEndpointRouteBuilder endpoints, string pattern = McpHttpDefaults.EndpointPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var sereinFlowMcpOptions = endpoints.ServiceProvider.GetRequiredService<SereinFlowMcpOptions>();
        var fileUploadSettings = endpoints.ServiceProvider.GetRequiredService<IFileUploadSettings>();
        var getEndpoint = endpoints.MapMethods(pattern, ["GET"],
            static () => Results.StatusCode(StatusCodes.Status405MethodNotAllowed))
            .RequireCors("sereinflow-mcp");
        var delete = endpoints.MapDelete(pattern, async (HttpContext context, SereinFlowAccess security, McpHttpSessionRegistry sessions, CancellationToken cancellationToken) =>
        {
            var principal = await AuthenticateAsync(context, security, cancellationToken);
            if (principal is null)
            {
                return Unauthorized(context);
            }

            var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Remove(sessionId, principal.Id))
            {
                return Results.StatusCode(StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });
        var deleteEndpoint = delete.RequireCors("sereinflow-mcp");
        var post = endpoints.MapPost(pattern, async (HttpContext context, SereinFlowMcpServer server, SereinFlowAccess security, ISereinFlowActorAccessor principalAccessor, IMcpRequestContextAccessor requestContextAccessor, McpHttpSessionRegistry sessions, McpRequestLimiter limiter, CancellationToken cancellationToken) =>
        {
            var maxRequestBytes = FileUploadLimits.GetMcpRequestBodyLimit(fileUploadSettings.MaxLibraryUploadBytes, sereinFlowMcpOptions.MaxRequestBytes);
            if (context.Request.ContentLength is > 0 && context.Request.ContentLength > maxRequestBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var principal = await AuthenticateAsync(context, security, cancellationToken);
            if (principal is null)
            {
                return Unauthorized(context);
            }

            if (!limiter.TryAcquire(principal.Id, out var limiterLease))
            {
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            using (limiterLease)
            {
                string body;
                try
                {
                    body = await ReadBodyAsync(context.Request.Body, maxRequestBytes, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                var request = SereinFlowMcpServer.ParseRequest(body);
                var method = request.Method;
                var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
                if (string.Equals(method, "initialize", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        sessionId = sessions.Create(principal.Id);
                    }
                    else if (!sessions.Validate(sessionId, principal.Id))
                    {
                        return Results.StatusCode(StatusCodes.Status404NotFound);
                    }
                }
                else if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Validate(sessionId, principal.Id))
                {
                    return Results.Json(new { error = new { code = McpErrorCodes.SessionRequired, message = "A valid Mcp-Session-Id is required. 必须提供有效的 Mcp-Session-Id。" } }, statusCode: StatusCodes.Status400BadRequest);
                }

                using var actorScope = principalAccessor.Push(principal);
                var previousContext = requestContextAccessor.Current;
                requestContextAccessor.Current = new McpRequestContext("http", sessionId);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(sereinFlowMcpOptions.MaxToolExecutionSeconds));
                try
                {
                    var response = await server.HandleParsedRequestAsync(request, requestTimeout.Token);
                    if (sessionId is not null)
                    {
                        context.Response.Headers["Mcp-Session-Id"] = sessionId;
                    }

                    if (response is null)
                    {
                        return Results.StatusCode(StatusCodes.Status202Accepted);
                    }

                    if (Encoding.UTF8.GetByteCount(response) > sereinFlowMcpOptions.MaxResponseBytes)
                    {
                        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                    }

                    return Results.Text(response, "application/json", Encoding.UTF8);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var diagnosticId = Guid.NewGuid().ToString("N");
                    Console.Error.WriteLine($"MCP request exceeded its execution time limit; diagnosticId={diagnosticId}. MCP 请求超过执行时间限制，诊断标识见上述 diagnosticId。");
                    return Results.Json(new { error = new { code = McpErrorCodes.ToolTimeout, message = "The MCP tool exceeded the configured execution time limit. MCP 工具超过配置的执行时间限制。", data = new { code = McpErrorCodes.ToolTimeout, diagnosticId } } }, statusCode: StatusCodes.Status504GatewayTimeout);
                }
                finally
                {
                    requestContextAccessor.Current = previousContext;
                }
            }
        });
        var postEndpoint = post.RequireCors("sereinflow-mcp");
        return new CompositeEndpointConventionBuilder(getEndpoint, deleteEndpoint, postEndpoint);
    }

    private static async Task<SereinFlowActor?> AuthenticateAsync(HttpContext context, SereinFlowAccess security, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return context.RequestServices.GetRequiredService<ISereinFlowActorAccessor>().Current;
    }

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Json(new { error = new { code = McpErrorCodes.Unauthenticated, message = "MCP authentication is required. 需要进行 MCP 身份验证。" } }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<string> ReadBodyAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException("The MCP request body is too large. MCP 请求体过大。");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private sealed class CompositeEndpointConventionBuilder : IEndpointConventionBuilder
    {
        private readonly IEndpointConventionBuilder[] _builders;
        public CompositeEndpointConventionBuilder(params IEndpointConventionBuilder[] builders)
        {
            _builders = builders;
        }

        public void Add(Action<EndpointBuilder> convention)
        {
            ArgumentNullException.ThrowIfNull(convention);
            foreach (var builder in _builders)
            {
                builder.Add(convention);
            }
        }
    }
}
