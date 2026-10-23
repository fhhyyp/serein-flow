using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;

namespace SereinFlow.AspNetCore;
public sealed class SereinFlowHttpSecurityOptions
{
    public string Mode { get; set; } = "";
    public string? AuthenticationScheme { get; set; }
    public bool ApiKeyManagement { get; set; }
}

public interface ISereinFlowSecurityCapabilities
{
    Task<bool> RequiresInitialSetupAsync(CancellationToken cancellationToken);
}

public static class SereinFlowHostSecurityExtensions
{
    public static SereinFlowHostBuilder UseHostSecurity(this SereinFlowHostBuilder builder)
    {
        if (builder.Services.Any(service => service.ServiceType == typeof(SereinFlowHttpSecurityOptions)))
        {
            throw new InvalidOperationException("SereinFlow HTTP security has already been selected.");
        }

        var argument1 = new SereinFlowHttpSecurityOptions
        {
            Mode = "Host"
        };
        builder.Services.AddSingleton(argument1);
        builder.Services.AddScoped<ISereinFlowAuthorizationService, HostAuthorization>();
        return builder;
    }
}

public sealed class HostAuthorization(IAuthorizationService authorization) : ISereinFlowAuthorizationService
{
    public async ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await authorization.AuthorizeAsync(actor.Principal, resource, operation);
        return result.Succeeded ? AccessDecision.Allowed : AccessDecision.Denied;
    }
}

public static class SereinFlowHttpActor
{
    private static readonly object ActorKey = new();
    public static SereinFlowActor? Get(HttpContext context) => context.Items.TryGetValue(ActorKey, out var value) ? value as SereinFlowActor : null;
    internal static void Set(HttpContext context, SereinFlowActor? actor) => context.Items[ActorKey] = actor;
    public static async Task<SereinFlowActor?> ResolveAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<SereinFlowHttpSecurityOptions>();
        if (options.Mode == "None")
        {
            return AnonymousAuthorization.Actor;
        }
        var principal = context.User;
        if (options.AuthenticationScheme is { } scheme)
        {
            var result = await context.AuthenticateAsync(scheme);
            if (!result.Succeeded)
            {
                return null;
            }

            principal = result.Principal!;
            context.User = principal;
        }

        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subject = principal.FindFirst("sub") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
        if (subject is null || string.IsNullOrWhiteSpace(subject.Value))
        {
            return null;
        }

        var id = options.Mode == "ApiKey" ? subject.Value : $"host:{Uri.EscapeDataString(principal.Identity.AuthenticationType ?? "host")}:{Uri.EscapeDataString(subject.Issuer)}:{Uri.EscapeDataString(subject.Value)}";
        {
            SereinFlowActor? result1 = new(id, principal);
            return result1;
        }
    }
}

public sealed class SereinFlowIdentityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISereinFlowActorAccessor actors)
    {
        var actor = await SereinFlowHttpActor.ResolveAsync(context);
        SereinFlowHttpActor.Set(context, actor);
        using var scope = actors.Push(actor);
        await next(context);
    }
}
