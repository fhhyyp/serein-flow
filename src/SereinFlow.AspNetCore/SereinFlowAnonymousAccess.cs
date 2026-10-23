using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;

namespace SereinFlow.AspNetCore;

internal sealed class AnonymousAuthorization : ISereinFlowAuthorizationService, ISereinFlowProjectScopeProvider
{
    internal static SereinFlowActor Actor { get; } = new("anonymous", new ClaimsPrincipal(new ClaimsIdentity()));

    public ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AccessDecision.Allowed);
    }

    public ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProjectAccessScope.All);
    }
}

internal static class AnonymousAccessRegistration
{
    internal static void AddDefaultSereinFlowAccess(this IServiceCollection services)
    {
        if (services.Any(service => service.ServiceType == typeof(SereinFlowHttpSecurityOptions)))
        {
            return;
        }

        services.AddSingleton(new SereinFlowHttpSecurityOptions { Mode = "None" });
        services.TryAddSingleton<ISereinFlowAuthorizationService, AnonymousAuthorization>();
        services.TryAddSingleton<ISereinFlowProjectScopeProvider, AnonymousAuthorization>();
    }
}
