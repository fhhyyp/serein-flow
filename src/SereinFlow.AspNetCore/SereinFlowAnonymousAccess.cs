using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Application;

namespace SereinFlow.AspNetCore;

internal sealed class AnonymousAuthorization : ISereinFlowAuthorizationService, ISereinFlowProjectScopeProvider
{
    internal static SereinFlowActor Actor { get; } = CreateActor();

    private static SereinFlowActor CreateActor()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        var actor = new SereinFlowActor("anonymous", principal);
        return actor;
    }

    public ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = ValueTask.FromResult(AccessDecision.Allowed);
        return result;
    }

    public ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = ValueTask.FromResult(ProjectAccessScope.All);
        return result;
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

        var securityOptions = new SereinFlowHttpSecurityOptions { Mode = "None" };
        services.AddSingleton(securityOptions);
        services.TryAddSingleton<ISereinFlowAuthorizationService, AnonymousAuthorization>();
        services.TryAddSingleton<ISereinFlowProjectScopeProvider, AnonymousAuthorization>();
    }
}
