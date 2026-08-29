using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.McpServer;
using SereinFlow.ScriptAdapter;

if (args.Any(static argument => string.Equals(argument, "--http", StringComparison.OrdinalIgnoreCase)))
{
    var webBuilder = WebApplication.CreateBuilder(args);
    ConfigureMcpServices(webBuilder.Services, webBuilder.Configuration, webBuilder.Environment.ContentRootPath);
    var maxRequestBytes = ReadPositiveLong(webBuilder.Configuration["SereinFlow:Mcp:Http:MaxRequestBytes"], 16 * 1024 * 1024);
    var maxResponseBytes = ReadPositiveLong(webBuilder.Configuration["SereinFlow:Mcp:Http:MaxResponseBytes"], 4 * 1024 * 1024);
    var maxConcurrentRequests = ReadPositiveInt(webBuilder.Configuration["SereinFlow:Mcp:Http:MaxConcurrentRequests"], 16);
    var maxRequestsPerMinute = ReadPositiveInt(webBuilder.Configuration["SereinFlow:Mcp:Http:MaxRequestsPerMinute"], 120);
    var maxToolExecutionSeconds = ReadPositiveInt(webBuilder.Configuration["SereinFlow:Mcp:Http:MaxToolExecutionSeconds"], 60);
    var listenUrl = webBuilder.Configuration["SereinFlow:Mcp:Http:ListenUrl"] ?? "http://127.0.0.1:5187";
    var allowedOrigins = ReadAllowedOrigins(webBuilder.Configuration["SereinFlow:Mcp:Http:AllowedOrigins"]);
    if (allowedOrigins.Length > 0)
    {
        webBuilder.Services.AddCors(options => options.AddPolicy("mcp", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }
    webBuilder.WebHost.UseUrls(listenUrl);
    webBuilder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBytes);

    var app = webBuilder.Build();
    if (allowedOrigins.Length > 0)
        app.UseCors("mcp");
    app.MapMethods("/mcp", ["GET"], static () => Results.StatusCode(StatusCodes.Status405MethodNotAllowed));
    app.MapDelete("/mcp", async (HttpContext context, McpSecurityService security, McpHttpSessionRegistry sessions, CancellationToken cancellationToken) =>
    {
        var principal = await AuthenticateAsync(context, security, cancellationToken);
        if (principal is null)
            return Results.Unauthorized();
        var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sessionId) || !sessions.Remove(sessionId, principal.Id))
            return Results.StatusCode(StatusCodes.Status404NotFound);
        return Results.NoContent();
    });
    app.MapPost("/mcp", async (
        HttpContext context,
        SereinFlowMcpServer server,
        McpSecurityService security,
        IMcpPrincipalAccessor principalAccessor,
        IMcpRequestContextAccessor requestContextAccessor,
        McpHttpSessionRegistry sessions,
        McpRequestLimiter limiter,
        CancellationToken cancellationToken) =>
    {
        var declaredLength = context.Request.ContentLength;
        if (declaredLength is > 0 && declaredLength > maxRequestBytes)
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
                body = await ReadBodyAsync(context.Request.Body, maxRequestBytes, cancellationToken);
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
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(maxToolExecutionSeconds));
            try
            {
                var response = await server.HandleRequestAsync(body, requestTimeout.Token);
                if (sessionId is not null)
                    context.Response.Headers["Mcp-Session-Id"] = sessionId;
                if (response is null)
                    return Results.StatusCode(StatusCodes.Status202Accepted);
                if (System.Text.Encoding.UTF8.GetByteCount(response) > maxResponseBytes)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                return Results.Text(response, "application/json", System.Text.Encoding.UTF8);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Json(new { error = new { code = "mcp.tool_timeout", message = "The MCP tool exceeded the configured execution time limit." } }, statusCode: StatusCodes.Status504GatewayTimeout);
            }
            finally
            {
                requestContextAccessor.Current = null;
                principalAccessor.Current = null;
            }
        }
    });
    await app.RunAsync();
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
ConfigureMcpServices(builder.Services, builder.Configuration, builder.Environment.ContentRootPath);

using var host = builder.Build();
var stdioPrincipalAccessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
var stdioRequestContextAccessor = host.Services.GetRequiredService<IMcpRequestContextAccessor>();
var stdioApiKey = builder.Configuration["SereinFlow:Mcp:Stdio:ApiKey"]
    ?? builder.Configuration["SereinFlow:Mcp:StdioApiKey"];
var stdioSecurity = host.Services.GetRequiredService<McpSecurityService>();
var configuredStdioPrincipal = string.IsNullOrWhiteSpace(stdioApiKey)
    ? null
    : await stdioSecurity.AuthenticateAsync(stdioApiKey, CancellationToken.None);
if (!string.IsNullOrWhiteSpace(stdioApiKey) && configuredStdioPrincipal is null)
{
    throw new InvalidOperationException(
        "The configured stdio MCP API key is invalid, expired or revoked. 配置的 stdio MCP API Key 无效、已过期或已撤销。");
}

stdioPrincipalAccessor.Current = configuredStdioPrincipal ?? new McpPrincipal(
    "local-stdio",
    null,
    Enum.GetValues<McpPermissionDto>().ToHashSet(),
    IsLocal: true,
    IsAdministrator: true);
stdioRequestContextAccessor.Current = new McpRequestContext(stdioPrincipalAccessor.Current, "stdio");
try
{
    var server = host.Services.GetRequiredService<SereinFlowMcpServer>();
    await server.RunAsync(Console.In, Console.Out);
}
finally
{
    stdioRequestContextAccessor.Current = null;
    stdioPrincipalAccessor.Current = null;
}

static void ConfigureMcpServices(IServiceCollection services, IConfiguration configuration, string contentRootPath)
{
    services.AddSereinFlowInfrastructure(configuration, contentRootPath);
    services.AddScoped<AiReadModelService>();
    services.AddScoped<ProjectLibraryService>();
    services.AddScoped<FlowDefinitionWriteService>();
    services.AddScoped<FlowDiffService>();
    services.AddScoped<FlowPatchService>();
    services.AddSingleton<IBuiltinNodeCatalog, BuiltinNodeCatalog>();
    services.AddScoped<McpPreviewService>();
    services.AddScoped<McpIdempotencyService>();
    services.AddScoped<McpSecurityService>(serviceProvider =>
        new McpSecurityService(
            serviceProvider.GetRequiredService<SereinFlow.Application.Persistence.IMcpApiKeyStore>(),
            configuration["SereinFlow:Mcp:BootstrapAdminKey"]));
    services.AddScoped<ISereinLangCompiler, SereinLangCompiler>();
    services.AddSingleton<IMcpPrincipalAccessor, McpPrincipalAccessor>();
    services.AddSingleton<IMcpRequestContextAccessor, McpRequestContextAccessor>();
    services.AddSingleton<McpHttpSessionRegistry>();
    services.AddSingleton(serviceProvider => new McpRequestLimiter(
        ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxConcurrentRequests"], 16),
        ReadPositiveInt(configuration["SereinFlow:Mcp:Http:MaxRequestsPerMinute"], 120)));
    services.AddSingleton<ISereinFlowMcpBackend, SereinFlowMcpBackend>();
    services.AddSingleton<SereinFlowMcpServer>(serviceProvider =>
        new SereinFlowMcpServer(
            serviceProvider.GetRequiredService<ISereinFlowMcpBackend>(),
            Console.Error,
            checked((int)ReadPositiveLong(configuration["SereinFlow:Mcp:Http:MaxRequestBytes"], 16 * 1024 * 1024)),
            checked((int)ReadPositiveLong(configuration["SereinFlow:Mcp:Http:MaxResponseBytes"], 4 * 1024 * 1024)),
            serviceProvider.GetRequiredService<IMcpRequestContextAccessor>()));
}

static long ReadPositiveLong(string? value, long fallback)
    => long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

static int ReadPositiveInt(string? value, int fallback)
    => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

static string[] ReadAllowedOrigins(string? value)
    => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

static async Task<McpPrincipal?> AuthenticateAsync(
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

static async Task<string> ReadBodyAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
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
    return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
}

static string? TryGetMethod(string body)
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
