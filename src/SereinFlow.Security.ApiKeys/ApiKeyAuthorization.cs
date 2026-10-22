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
        var argument4 = permissions.Select(permission =>
        {
            var argument5 = SereinFlowOperationNames.ToName(permission);
            var value1 = new Claim(PermissionClaim, argument5);
            return value1;
        });
        claims.AddRange(argument4);
        if (projectId is { } project)
        {
            var argument1 = project.ToString("D");
            var projectClaim = new Claim(ProjectClaim, argument1);
            claims.Add(projectClaim);
        }

        if (IsAdministrator)
        {
            var administratorClaim = new Claim(AdminClaim, "true");
            claims.Add(administratorClaim);
        }

        {
            var argument3 = new ClaimsIdentity(claims, "SereinFlow.ApiKey");
            var argument2 = new ClaimsPrincipal(argument3);
            SereinFlowActor result1 = new(id, argument2);
            return result1;
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

        var current = await service.ResolveActorAsync(actor.Id, cancellationToken);
        return current;
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
            var result5 = new AccessDecision(false, McpErrorCodes.ProjectAccessDenied);
            return result5;
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

        var projects = actor.Principal.FindAll(ApiKeyActors.ProjectClaim).Select(claim =>
        {
            var value6 = Guid.Parse(claim.Value);
            return value6;
        }).ToHashSet();
        {
            var result6 = new ProjectAccessScope(false, projects);
            return result6;
        }
    }
}
