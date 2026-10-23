using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.AspNetCore;
using SereinFlow.Contracts;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Security.ApiKeys;

public static class ApiKeyHosting
{
    public const string Scheme = "SereinFlow.ApiKey";
    public const string SessionScheme = "SereinFlow.ApiKeySession";
    public static SereinFlowHostBuilder UseApiKeySecurity(this SereinFlowHostBuilder builder)
    {
        builder.Services.AddSereinFlowApiKeys();
        return builder;
    }

    public static IServiceCollection AddSereinFlowApiKeys(this IServiceCollection services)
    {
        if (services.Any(service => service.ServiceType == typeof(SereinFlowHttpSecurityOptions)))
        {
            throw new InvalidOperationException("SereinFlow HTTP security has already been selected.");
        }

        services.AddSereinFlowAccess();
        services.AddSingleton(new SereinFlowHttpSecurityOptions
        {
            Mode = "ApiKey",
            AuthenticationScheme = Scheme,
            ApiKeyManagement = true
        });
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        services.AddScoped(provider =>
        {
            var apiKeyStore = provider.GetRequiredService<IApiKeyStore>();
            var configuration = provider.GetRequiredService<IConfiguration>();
            var bootstrapKey = configuration["SereinFlow:Security:ApiKeys:BootstrapAdminKey"];
            return new ApiKeyService(apiKeyStore, bootstrapKey);
        });
        services.AddScoped<ApiKeyManagementService>();
        services.AddScoped<ISereinFlowAuthorizationService, ApiKeyAuthorization>();
        services.AddScoped<ISereinFlowProjectScopeProvider, ApiKeyAuthorization>();
        services.AddScoped<ISereinFlowSecurityCapabilities, ApiKeyCapabilities>();
        services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyHandler>(Scheme, _ =>
        {
        })
            .AddCookie(SessionScheme, options =>
        {
            options.Cookie.Name = "SereinFlow.ApiKeySession";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });
        services.AddControllers().AddApplicationPart(typeof(ApiKeysController).Assembly);
        return services;
    }

    private sealed class ApiKeyCapabilities(IApiKeyStore keys, IConfiguration configuration) : ISereinFlowSecurityCapabilities
    {
        public async Task<bool> RequiresInitialSetupAsync(CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(configuration["SereinFlow:Security:ApiKeys:BootstrapAdminKey"]) && (await keys.ListAsync(cancellationToken)).Count == 0;
    }
}

public sealed class ApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ApiKeyService keys) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        SereinFlowActor? actor;
        var raw = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Fail("A Bearer credential is required.");
            }

            var secret = raw[7..].Trim();
            actor = await keys.AuthenticateAsync(secret, Context.RequestAborted);
        }
        else
        {
            var session = await Context.AuthenticateAsync(ApiKeyHosting.SessionScheme);
            var id = session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!session.Succeeded || string.IsNullOrEmpty(id))
            {
                return AuthenticateResult.NoResult();
            }

            actor = await keys.ResolveActorAsync(id, Context.RequestAborted);
        }

        if (actor is not null)
        {
            ApiKeyActors.WithStoreReference(actor);
        }

        {
            if (actor is null)
            {
                return AuthenticateResult.Fail("The credential is invalid, expired or revoked.");
            }

            var ticket = new AuthenticationTicket(actor.Principal, ApiKeyHosting.Scheme);
            return AuthenticateResult.Success(ticket);
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}

[ApiController]
[Route("api/security")]
public sealed class ApiKeysController(ApiKeyManagementService management, IApiKeyStore store, SereinFlowAccess access) : SereinFlow.Api.Controllers.ApiControllerBase
{
    private async Task RequireManagementAsync()
    {
        var actor = SereinFlowHttpActor.Get(HttpContext);
        await access.RequireAsync(actor, SereinFlowPermission.SecurityManage, cancellationToken: HttpContext.RequestAborted);
    }

    [HttpPost("api-keys/initial-admin")]
    [SereinFlow.Api.SereinFlowAnonymous]
    public async Task<IActionResult> InitialAdmin(CancellationToken cancellationToken)
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        if (address is null || !IPAddress.IsLoopback(address) || Request.Headers.ContainsKey("Forwarded") || Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            return StatusCode(403);
        }

        var created = await management.CreateInitialAdministratorAsync(cancellationToken);
        {
            return Created("/api/security/api-keys/" + created.Key.Id, created);
        }
    }

    [HttpGet("api-keys")]
    [SereinFlow.Api.SereinFlowScopedAuthorization]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        await RequireManagementAsync();
        {
            var apiKeys = await management.ListAsync(cancellationToken);
            return Ok(apiKeys);
        }
    }

    [HttpPost("api-keys")]
    [SereinFlow.Api.SereinFlowScopedAuthorization]
    public async Task<IActionResult> Create(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        await RequireManagementAsync();
        var created = await management.CreateAsync(request, cancellationToken);
        {
            return Created("/api/security/api-keys/" + created.Key.Id, created);
        }
    }

    [HttpPost("api-keys/{id}/rotate")]
    [SereinFlow.Api.SereinFlowScopedAuthorization]
    public async Task<IActionResult> Rotate(string id, CancellationToken cancellationToken)
    {
        await RequireManagementAsync();
        var rotated = await management.RotateAsync(id, cancellationToken);
        var session = await HttpContext.AuthenticateAsync(ApiKeyHosting.SessionScheme);
        if (SereinFlowHttpActor.Get(HttpContext)?.Id == id && session.Succeeded
            && session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) == id)
        {
            // Move only this browser's existing session to the replacement key.
            // Other sessions and Bearer credentials keep their revocation semantics.
            // 仅迁移当前浏览器的既有会话；其他会话和 Bearer 凭据仍按撤销规则失效。
            var subject = new Claim(ClaimTypes.NameIdentifier, rotated.Key.Id);
            var identity = new ClaimsIdentity([subject], ApiKeyHosting.SessionScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(ApiKeyHosting.SessionScheme, principal, session.Properties);
        }
        return Ok(rotated);
    }

    [HttpDelete("api-keys/{id}")]
    [SereinFlow.Api.SereinFlowScopedAuthorization]
    public async Task<IActionResult> Revoke(string id, CancellationToken cancellationToken)
    {
        await RequireManagementAsync();
        {
            var revoke = await management.RevokeAsync(id, cancellationToken);
            return Ok(revoke);
        }
    }

    [HttpPost("session")]
    [SereinFlow.Api.SereinFlowScopedAuthorization]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        var actor = SereinFlowHttpActor.Get(HttpContext);
        if (actor is null)
        {
            return Unauthorized();
        }

        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        if (actor.Id != "bootstrap-admin" && await store.FindAsync(actor.Id, cancellationToken) is null)
        {
            return Unauthorized();
        }

        var subjectClaim = new Claim(ClaimTypes.NameIdentifier, actor.Id);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([subjectClaim], ApiKeyHosting.SessionScheme));
        var authenticationProperties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = false
        };
        await HttpContext.SignInAsync(ApiKeyHosting.SessionScheme, principal, authenticationProperties);
        {
            return NoContent();
        }
    }

    [HttpDelete("session")]
    [SereinFlow.Api.SereinFlowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(ApiKeyHosting.SessionScheme);
        {
            return NoContent();
        }
    }

    [HttpGet("session")]
    [SereinFlow.Api.SereinFlowScopedAuthorization]
    public IActionResult Session() => SereinFlowHttpActor.Get(HttpContext) is { } actor ? Ok(new { id = actor.Id }) : Unauthorized();
}
