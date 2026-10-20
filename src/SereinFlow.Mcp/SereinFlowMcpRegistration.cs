using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.ScriptAdapter;
using System.Text;
using System.Text.Json;

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

    public static SereinFlowMcpOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var sereinFlowMcpOptions = new SereinFlowMcpOptions
        {
            MaxRequestBytes = ReadPositiveLong(configuration["SereinFlow:Mcp:Http:MaxRequestBytes"], 16 * 1024 * 1024),
            MaxResponseBytes = ReadPositiveLong(configuration["SereinFlow:Mcp:Http:MaxResponseBytes"], 4 * 1024 * 1024),
            MaxConcurrentRequests = ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxConcurrentRequests"], 16),
            MaxRequestsPerMinute = ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxRequestsPerMinute"], 120),
            MaxToolExecutionSeconds = ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxToolExecutionSeconds"], 60),
            AllowedOrigins = ReadAllowedOrigins(configuration["SereinFlow:Mcp:Http:AllowedOrigins"]),
            AiGuidance = McpAiGuidanceOptions.FromConfiguration(configuration)
        };
        return sereinFlowMcpOptions;
    }

    private static long ReadPositiveLong(string? value, long fallback)
    {
        long evaluatedValue = long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        return evaluatedValue;
    }

    private static int ReadPositiveInt(string? value, int fallback)
    {
        int evaluatedValue1 = int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        return evaluatedValue1;
    }

    private static string[] ReadAllowedOrigins(string? value)
    {
        string[] conditionalValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            conditionalValue = [];
        }
        else
        {
            var splitResult = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var whereResult = splitResult.Where(static origin =>
            {
                var evaluatedValue2 = Uri.TryCreate(origin, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrWhiteSpace(uri.Host);
                return evaluatedValue2;
            });
            var distinctResult = whereResult.Distinct(StringComparer.OrdinalIgnoreCase);
            conditionalValue = distinctResult.ToArray();
        }

        string[] evaluatedValue3 = conditionalValue;
        return evaluatedValue3;
    }
}

public static class SereinFlowMcpServiceCollectionExtensions
{
    public static IServiceCollection AddSereinFlowMcp(this IServiceCollection services, IConfiguration? configuration = null, string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(serviceProvider =>
        {
            var evaluatedValue4 = configuration ?? serviceProvider.GetRequiredService<IConfiguration>();
            var fromConfigurationResult = SereinFlowMcpOptions.FromConfiguration(evaluatedValue4);
            return fromConfigurationResult;
        });
        services.TryAddSingleton<McpAiGuidanceProvider>(serviceProvider =>
        {
            var evaluatedValue5 = configuration ?? serviceProvider.GetRequiredService<IConfiguration>();
            var evaluatedValue6 = contentRootPath ?? serviceProvider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>()?.ContentRootPath ?? AppContext.BaseDirectory;
            var mcpAiGuidanceProvider = new McpAiGuidanceProvider(evaluatedValue5, evaluatedValue6);
            return mcpAiGuidanceProvider;
        });
        services.AddCors();
        var addOptionsResult = services.AddOptions<CorsOptions>();
        addOptionsResult.Configure<IConfiguration>((cors, resolvedConfiguration) =>
        {
            var options = SereinFlowMcpOptions.FromConfiguration(configuration ?? resolvedConfiguration);
            cors.AddPolicy("sereinflow-mcp", policy =>
            {
                var allowAnyHeaderResult = policy.AllowAnyHeader();
                allowAnyHeaderResult.AllowAnyMethod();
                if (options.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(options.AllowedOrigins);
                }
                else
                {
                    policy.SetIsOriginAllowed(static _ => false);
                }
            });
        });
        services.TryAddScoped<ISereinLangCompiler, SereinLangCompiler>();
        services.TryAddScoped<McpSecurityService>(serviceProvider =>
        {
            var getRequiredServiceResult = serviceProvider.GetRequiredService<IMcpApiKeyStore>();
            var evaluatedValue7 = (configuration ?? serviceProvider.GetRequiredService<IConfiguration>())["SereinFlow:Mcp:BootstrapAdminKey"];
            var mcpSecurityService = new McpSecurityService(getRequiredServiceResult, evaluatedValue7);
            return mcpSecurityService;
        });
        services.TryAddSingleton<IMcpPrincipalAccessor, McpPrincipalAccessor>();
        services.TryAddSingleton<IMcpRequestContextAccessor, McpRequestContextAccessor>();
        services.TryAddSingleton<McpHttpSessionRegistry>();
        services.TryAddSingleton<McpRequestLimiter>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<SereinFlowMcpOptions>();
            var mcpRequestLimiter = new McpRequestLimiter(options.MaxConcurrentRequests, options.MaxRequestsPerMinute);
            return mcpRequestLimiter;
        });
        services.TryAddSingleton<McpMutationGate>();
        services.TryAddSingleton<McpToolAuditService>();
        services.TryAddSingleton<McpToolExecutor>();
        services.TryAddSingleton<McpResourceReader>();
        services.TryAddSingleton<McpToolCatalog>(_ =>
        {
            var createResult = SereinFlowMcpToolCatalogFactory.Create();
            return createResult;
        });
        services.TryAddSingleton<ISereinFlowMcpBackend, SereinFlowMcpBackend>();
        services.TryAddSingleton<SereinFlowMcpServer>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<SereinFlowMcpOptions>();
            var getRequiredServiceResult8 = serviceProvider.GetRequiredService<ISereinFlowMcpBackend>();
            var getRequiredServiceResult9 = serviceProvider.GetRequiredService<IMcpRequestContextAccessor>();
            var getRequiredServiceResult10 = serviceProvider.GetRequiredService<IFileUploadSettings>();
            var getRequiredServiceResult11 = serviceProvider.GetRequiredService<McpAiGuidanceProvider>();
            var sereinFlowMcpServer = new SereinFlowMcpServer(getRequiredServiceResult8, Console.Error, checked((int)options.MaxRequestBytes), checked((int)options.MaxResponseBytes), getRequiredServiceResult9, getRequiredServiceResult10, getRequiredServiceResult11);
            return sereinFlowMcpServer;
        });
        return services;
    }
}

public static class SereinFlowMcpEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapSereinFlowMcp(this IEndpointRouteBuilder endpoints, string pattern = McpHttpDefaults.EndpointPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = endpoints.ServiceProvider.GetRequiredService<SereinFlowMcpOptions>();
        var fileUploadSettings = endpoints.ServiceProvider.GetRequiredService<IFileUploadSettings>();
        var mapMethodsResult = endpoints.MapMethods(pattern, ["GET"], static () =>
        {
            var statusCodeResult = Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
            return statusCodeResult;
        });
        var getEndpoint = mapMethodsResult.RequireCors("sereinflow-mcp");
        var mapDeleteResult = endpoints.MapDelete(pattern, async (HttpContext context, McpSecurityService security, McpHttpSessionRegistry sessions, CancellationToken cancellationToken) =>
        {
            var principal = await AuthenticateAsync(context, security, cancellationToken);
            if (principal is null)
            {
                var unauthorizedResult = Unauthorized(context);
                return unauthorizedResult;
            }

            var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Remove(sessionId, principal.Id))
            {
                var statusCodeResult12 = Results.StatusCode(StatusCodes.Status404NotFound);
                return statusCodeResult12;
            }

            var noContentResult = Results.NoContent();
            return noContentResult;
        });
        var deleteEndpoint = mapDeleteResult.RequireCors("sereinflow-mcp");
        var mapPostResult = endpoints.MapPost(pattern, async (HttpContext context, SereinFlowMcpServer server, McpSecurityService security, IMcpPrincipalAccessor principalAccessor, IMcpRequestContextAccessor requestContextAccessor, McpHttpSessionRegistry sessions, McpRequestLimiter limiter, CancellationToken cancellationToken) =>
        {
            var maxRequestBytes = FileUploadLimits.GetMcpRequestBodyLimit(fileUploadSettings.MaxLibraryUploadBytes, options.MaxRequestBytes);
            if (context.Request.ContentLength is> 0 && context.Request.ContentLength > maxRequestBytes)
            {
                var statusCodeResult13 = Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                return statusCodeResult13;
            }

            var principal = await AuthenticateAsync(context, security, cancellationToken);
            if (principal is null)
            {
                var unauthorizedResult14 = Unauthorized(context);
                return unauthorizedResult14;
            }

            if (!limiter.TryAcquire(principal.Id, out var limiterLease))
            {
                var statusCodeResult15 = Results.StatusCode(StatusCodes.Status429TooManyRequests);
                return statusCodeResult15;
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
                    var statusCodeResult16 = Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                    return statusCodeResult16;
                }

                var method = TryGetMethod(body);
                var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
                if (string.Equals(method, "initialize", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        sessionId = sessions.Create(principal.Id);
                    }
                    else if (!sessions.Validate(sessionId, principal.Id))
                    {
                        var statusCodeResult17 = Results.StatusCode(StatusCodes.Status404NotFound);
                        return statusCodeResult17;
                    }
                }
                else if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Validate(sessionId, principal.Id))
                {
                    var jsonResult = Results.Json(new { error = new { code = McpErrorCodes.SessionRequired, message = "A valid Mcp-Session-Id is required." } }, statusCode: StatusCodes.Status400BadRequest);
                    return jsonResult;
                }

                principalAccessor.Current = principal;
                requestContextAccessor.Current = new McpRequestContext(principal, "http", sessionId);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var fromSecondsResult = TimeSpan.FromSeconds(options.MaxToolExecutionSeconds);
                requestTimeout.CancelAfter(fromSecondsResult);
                try
                {
                    var response = await server.HandleRequestAsync(body, requestTimeout.Token);
                    if (sessionId is not null)
                    {
                        context.Response.Headers["Mcp-Session-Id"] = sessionId;
                    }

                    if (response is null)
                    {
                        var statusCodeResult18 = Results.StatusCode(StatusCodes.Status202Accepted);
                        return statusCodeResult18;
                    }

                    if (Encoding.UTF8.GetByteCount(response) > options.MaxResponseBytes)
                    {
                        var statusCodeResult19 = Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                        return statusCodeResult19;
                    }

                    var textResult = Results.Text(response, "application/json", Encoding.UTF8);
                    return textResult;
                }
                catch (OperationCanceledException)when (!cancellationToken.IsCancellationRequested)
                {
                    var newGuidResult = Guid.NewGuid();
                    var diagnosticId = newGuidResult.ToString("N");
                    Console.Error.WriteLine($"MCP request exceeded its execution time limit; diagnosticId={diagnosticId}.");
                    var jsonResult20 = Results.Json(new { error = new { code = McpErrorCodes.ToolTimeout, message = "The MCP tool exceeded the configured execution time limit.", data = new { code = McpErrorCodes.ToolTimeout, diagnosticId } } }, statusCode: StatusCodes.Status504GatewayTimeout);
                    return jsonResult20;
                }
                finally
                {
                    requestContextAccessor.Current = null;
                    principalAccessor.Current = null;
                }
            }
        });
        var postEndpoint = mapPostResult.RequireCors("sereinflow-mcp");
        var compositeEndpointConventionBuilder = new CompositeEndpointConventionBuilder(getEndpoint, deleteEndpoint, postEndpoint);
        return compositeEndpointConventionBuilder;
    }

    private static async Task<McpPrincipal?> AuthenticateAsync(HttpContext context, McpSecurityService security, CancellationToken cancellationToken)
    {
        var header = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var secret = header["Bearer ".Length..].Trim();
        var authenticateAsyncResult = await security.AuthenticateAsync(secret, cancellationToken);
        return authenticateAsyncResult;
    }

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        var jsonResult21 = Results.Json(new { error = new { code = McpErrorCodes.Unauthenticated, message = "MCP authentication is required." } }, statusCode: StatusCodes.Status401Unauthorized);
        return jsonResult21;
    }

    private static async Task<string> ReadBodyAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var asMemoryResult = chunk.AsMemory();
            var read = await body.ReadAsync(asMemoryResult, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException("The MCP request body is too large.");
            }

            var asMemoryResult22 = chunk.AsMemory(0, read);
            await buffer.WriteAsync(asMemoryResult22, cancellationToken);
        }

        var toArrayResult = buffer.ToArray();
        var getStringResult = Encoding.UTF8.GetString(toArrayResult);
        return getStringResult;
    }

    private static string? TryGetMethod(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            string? evaluatedValue23 = document.RootElement.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String ? method.GetString() : null;
            return evaluatedValue23;
        }
        catch (JsonException)
        {
            return null;
        }
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
