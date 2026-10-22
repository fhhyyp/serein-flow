using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Security.ApiKeys;
internal static class SecurityTestSupport
{
    internal static IServiceCollection AddTestAuthorization(this IServiceCollection services)
    {
        services.AddSereinFlowAccess();
        services.AddScoped<ISereinFlowAuthorizationService, ApiKeyAuthorization>();
        services.AddScoped<ISereinFlowProjectScopeProvider, ApiKeyAuthorization>();
        return services;
    }

    private static string? ProjectClaim(SereinFlowActor actor) => actor.Principal.FindFirst("sereinflow.project")?.Value;
    internal static Guid? ProjectId(SereinFlowActor actor)
    {
        var value = ProjectClaim(actor);
        {
            Guid? result1 = Guid.TryParse(value, out var id) ? id : null;
            return result1;
        }
    }

    internal static IReadOnlySet<SereinFlowPermission> Permissions(SereinFlowActor actor) => actor.Principal.FindAll("sereinflow.operation").Select(claim =>
    {
        var value1 = SereinFlowOperationNames.TryParse(claim.Value, out var permission) ? permission : throw new InvalidOperationException();
        return value1;
    }).ToHashSet();
    private static SereinFlowAccess CreateAccess(ApiKeyService service)
    {
        var policy = new ApiKeyAuthorization(service: service);
        var access = new SereinFlowAccess(policy, policy);
        return access;
    }

    internal static void Require(this ApiKeyService service, SereinFlowActor actor, SereinFlowPermission permission, Guid? projectId = null) => CreateAccess(service).RequireAsync(actor, permission, projectId).AsTask().GetAwaiter().GetResult();
    internal static void RequireAdministrator(this ApiKeyService service, SereinFlowActor actor) => CreateAccess(service).RequireWorkspaceAsync(actor).AsTask().GetAwaiter().GetResult();
}
