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
using SereinFlow.ScriptAdapter;
using System.Text;
using System.Text.Json;

namespace SereinFlow.Mcp;

public sealed class SereinFlowMcpOptions
{
    public const string SectionName = "SereinFlow:Mcp";

    public long MaxRequestBytes { get; init; } = 16 * 1024 * 1024;
    public long MaxResponseBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxConcurrentRequests { get; init; } = 16;
    public int MaxRequestsPerMinute { get; init; } = 120;
    public int MaxToolExecutionSeconds { get; init; } = 60;
    public string[] AllowedOrigins { get; init; } = [];

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
            AllowedOrigins = ReadAllowedOrigins(configuration["SereinFlow:Mcp:Http:AllowedOrigins"])
        };
    }

    private static long ReadPositiveLong(string? value, long fallback)
        => long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static int ReadPositiveInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static string[] ReadAllowedOrigins(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    && !string.IsNullOrWhiteSpace(uri.Host))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
}

public static class SereinFlowMcpServiceCollectionExtensions
{
    public static IServiceCollection AddSereinFlowMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = SereinFlowMcpOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        services.AddCors(cors => cors.AddPolicy("sereinflow-mcp", policy =>
        {
            policy.AllowAnyHeader().AllowAnyMethod();
            if (options.AllowedOrigins.Length > 0)
                policy.WithOrigins(options.AllowedOrigins);
            else
                policy.SetIsOriginAllowed(static _ => false);
        }));

        services.TryAddScoped<ISereinLangCompiler, SereinLangCompiler>();
        services.TryAddScoped<McpSecurityService>(serviceProvider =>
            new McpSecurityService(
                serviceProvider.GetRequiredService<IMcpApiKeyStore>(),
                configuration["SereinFlow:Mcp:BootstrapAdminKey"]));
        services.TryAddSingleton<IMcpPrincipalAccessor, McpPrincipalAccessor>();
        services.TryAddSingleton<IMcpRequestContextAccessor, McpRequestContextAccessor>();
        services.TryAddSingleton<McpHttpSessionRegistry>();
        services.TryAddSingleton<McpRequestLimiter>(_ => new McpRequestLimiter(
            options.MaxConcurrentRequests,
            options.MaxRequestsPerMinute));
        services.TryAddSingleton<ISereinFlowMcpBackend, SereinFlowMcpBackend>();
        services.TryAddSingleton<SereinFlowMcpServer>(serviceProvider =>
            new SereinFlowMcpServer(
                serviceProvider.GetRequiredService<ISereinFlowMcpBackend>(),
                Console.Error,
                checked((int)options.MaxRequestBytes),
                checked((int)options.MaxResponseBytes),
                serviceProvider.GetRequiredService<IMcpRequestContextAccessor>()));

        return services;
    }
}

public static class SereinFlowMcpEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapSereinFlowMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = endpoints.ServiceProvider.GetRequiredService<SereinFlowMcpOptions>();

        var getEndpoint = endpoints.MapMethods(pattern, ["GET"], static () => Results.StatusCode(StatusCodes.Status405MethodNotAllowed))
            .RequireCors("sereinflow-mcp");
        var deleteEndpoint = endpoints.MapDelete(pattern, async (
            HttpContext context,
            McpSecurityService security,
            McpHttpSessionRegistry sessions,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthenticateAsync(context, security, cancellationToken);
            if (principal is null)
                return Results.Json(new { error = new { code = "mcp.unauthenticated", message = "MCP authentication is required." } }, statusCode: StatusCodes.Status401Unauthorized);
            var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Remove(sessionId, principal.Id))
                return Results.StatusCode(StatusCodes.Status404NotFound);
            return Results.NoContent();
        }).RequireCors("sereinflow-mcp");
        var postEndpoint = endpoints.MapPost(pattern, async (
            HttpContext context,
            SereinFlowMcpServer server,
            McpSecurityService security,
            IMcpPrincipalAccessor principalAccessor,
            IMcpRequestContextAccessor requestContextAccessor,
            McpHttpSessionRegistry sessions,
            McpRequestLimiter limiter,
            CancellationToken cancellationToken) =>
        {
            if (context.Request.ContentLength is > 0 && context.Request.ContentLength > options.MaxRequestBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var principal = await AuthenticateAsync(context, security, cancellationToken);
            if (principal is null)
                return Results.Json(new { error = new { code = "mcp.unauthenticated", message = "MCP authentication is required." } }, statusCode: StatusCodes.Status401Unauthorized);
            if (!limiter.TryAcquire(principal.Id, out var limiterLease))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            using (limiterLease)
            {
                string body;
                try
                {
                    body = await ReadBodyAsync(context.Request.Body, options.MaxRequestBytes, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                var method = TryGetMethod(body);
                var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
                if (string.Equals(method, "initialize", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(sessionId))
                        sessionId = sessions.Create(principal.Id);
                    else if (!sessions.Validate(sessionId, principal.Id))
                        return Results.StatusCode(StatusCodes.Status404NotFound);
                }
                else if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Validate(sessionId, principal.Id))
                {
                    return Results.Json(new { error = new { code = "mcp.session_required", message = "A valid Mcp-Session-Id is required." } }, statusCode: StatusCodes.Status400BadRequest);
                }

                principalAccessor.Current = principal;
                requestContextAccessor.Current = new McpRequestContext(principal, "http", sessionId);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(options.MaxToolExecutionSeconds));
                try
                {
                    var response = await server.HandleRequestAsync(body, requestTimeout.Token);
                    if (sessionId is not null)
                        context.Response.Headers["Mcp-Session-Id"] = sessionId;
                    if (response is null)
                        return Results.StatusCode(StatusCodes.Status202Accepted);
                    if (Encoding.UTF8.GetByteCount(response) > options.MaxResponseBytes)
                        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                    return Results.Text(response, "application/json", Encoding.UTF8);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var diagnosticId = Guid.NewGuid().ToString("N");
                    Console.Error.WriteLine($"MCP request exceeded its execution time limit; diagnosticId={diagnosticId}.");
                    return Results.Json(new
                    {
                        error = new
                        {
                            code = "mcp.tool_timeout",
                            message = "The MCP tool exceeded the configured execution time limit.",
                            data = new { code = "mcp.tool_timeout", diagnosticId }
                        }
                    }, statusCode: StatusCodes.Status504GatewayTimeout);
                }
                finally
                {
                    requestContextAccessor.Current = null;
                    principalAccessor.Current = null;
                }
            }
        }).RequireCors("sereinflow-mcp");

        return new CompositeEndpointConventionBuilder(getEndpoint, deleteEndpoint, postEndpoint);
    }

    private static async Task<McpPrincipal?> AuthenticateAsync(
        HttpContext context,
        McpSecurityService security,
        CancellationToken cancellationToken)
    {
        var header = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header)
            || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var secret = header["Bearer ".Length..].Trim();
        return await security.AuthenticateAsync(secret, cancellationToken);
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
                break;
            total += read;
            if (total > maxBytes)
                throw new InvalidOperationException("The MCP request body is too large.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string? TryGetMethod(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("method", out var method)
                && method.ValueKind == JsonValueKind.String
                ? method.GetString()
                : null;
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
                builder.Add(convention);
        }
    }
}
