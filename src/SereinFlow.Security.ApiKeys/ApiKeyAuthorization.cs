using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Security.ApiKeys;

public static class ApiKeyActors
{
    internal const string PermissionClaim = "sereinflow.operation";
    internal const string ProjectClaim = "sereinflow.project";
    internal const string AdminClaim = "sereinflow.admin";
    internal const string StoreReferenceClaim = "sereinflow.key-reference";
    public static SereinFlowActor Create(string id, Guid? projectId, IReadOnlySet<SereinFlowPermission> permissions, bool IsAdministrator = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id)
        };
        claims.AddRange(permissions.Select(permission =>
        {
            return new Claim(PermissionClaim, SereinFlowOperationNames.ToName(permission));
        }));
        if (projectId is { } project)
        {
            var projectClaim = new Claim(ProjectClaim, project.ToString("D"));
            claims.Add(projectClaim);
        }

        if (IsAdministrator)
        {
            var administratorClaim = new Claim(AdminClaim, "true");
            claims.Add(administratorClaim);
        }

        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "SereinFlow.ApiKey"));
            return new(id, principal);
        }
    }

    internal static SereinFlowActor WithStoreReference(SereinFlowActor actor)
    {
        var reference = new Claim(StoreReferenceClaim, actor.Id);
        ((ClaimsIdentity)actor.Principal.Identity!).AddClaim(reference);
        return actor;
    }
}

public sealed class ApiKeyAuthorization(ApiKeyService? service = null) : ISereinFlowAuthorizationService, ISereinFlowProjectScopeProvider
{
    private async ValueTask<SereinFlowActor?> RefreshAsync(SereinFlowActor actor, CancellationToken cancellationToken)
    {
        if (!actor.Principal.HasClaim(ApiKeyActors.StoreReferenceClaim, actor.Id))
        {
            return actor;
        }

        if (service is null)
        {
            return null;
        }
        return await service.ResolveActorAsync(actor.Id, cancellationToken);
    }

    public async ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await RefreshAsync(actor, cancellationToken);
        if (current is null)
        {
            return AccessDecision.Denied;
        }

        actor = current;

        var admin = actor.Principal.HasClaim(ApiKeyActors.AdminClaim, "true");
        if (admin)
        {
            return AccessDecision.Allowed;
        }

        if (operation is SereinFlowOperations.SecurityManage or SereinFlowOperations.WorkspaceManage)
        {
            return AccessDecision.Denied;
        }

        if (!actor.Principal.HasClaim(ApiKeyActors.PermissionClaim, operation))
        {
            return AccessDecision.Denied;
        }

        var projectKey = resource.ProjectId?.ToString("D");
        if (projectKey is not null && !actor.Principal.HasClaim(ApiKeyActors.ProjectClaim, projectKey))
        {
            return new AccessDecision(false, McpErrorCodes.ProjectAccessDenied);
        }

        return AccessDecision.Allowed;
    }

    public async ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await RefreshAsync(actor, cancellationToken);
        if (current is null)
        {
            return ProjectAccessScope.None;
        }

        actor = current;

        if (actor.Principal.HasClaim(ApiKeyActors.AdminClaim, "true"))
        {
            return ProjectAccessScope.All;
        }

        if (!actor.Principal.HasClaim(ApiKeyActors.PermissionClaim, operation))
        {
            return ProjectAccessScope.None;
        }

        var projects = actor.Principal
            .FindAll(ApiKeyActors.ProjectClaim)
            .Select(claim =>
        {
            return Guid.Parse(claim.Value);
        })
            .ToHashSet();
        {
            return new ProjectAccessScope(false, projects);
        }
    }
}
