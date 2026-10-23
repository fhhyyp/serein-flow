using System.Security.Claims;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed record SereinFlowActor(string Id, ClaimsPrincipal Principal);
public sealed record SereinFlowResource(string Kind, string? Id = null, Guid? ProjectId = null)
{
    public static SereinFlowResource Workspace { get; } = new("workspace");

    public static SereinFlowResource Project(Guid id)
    {
        var key = id.ToString("D");
        return new SereinFlowResource("project", key, id);
    }

    public static SereinFlowResource Flow(Guid id, Guid projectId)
    {
        var key = id.ToString("D");
        return new SereinFlowResource("flow", key, projectId);
    }

    public static SereinFlowResource Run(Guid id, Guid projectId)
    {
        var key = id.ToString("D");
        return new SereinFlowResource("run", key, projectId);
    }
    public static SereinFlowResource Library(string id, Guid? projectId = null) => new("library", id, projectId);
}

public sealed record AccessDecision(bool IsAllowed, string? Code = null)
{
    public static AccessDecision Allowed { get; } = new(true);
    public static AccessDecision Denied { get; } = new(false, McpErrorCodes.PermissionDenied);
}

public sealed record ProjectAccessScope(bool AllProjects, IReadOnlySet<Guid> ProjectIds)
{
    public static ProjectAccessScope All { get; } = new(true, ImmutableHashSet<Guid>.Empty);
    public static ProjectAccessScope None { get; } = new(false, ImmutableHashSet<Guid>.Empty);

    public bool Contains(Guid id) => AllProjects || ProjectIds.Contains(id);
}

public interface ISereinFlowAuthorizationService
{
    ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default);
}

public interface ISereinFlowProjectScopeProvider
{
    ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default);
}

public interface ISereinFlowActorAccessor
{
    SereinFlowActor? Current { get; set; }

    IDisposable Push(SereinFlowActor? actor);
}

public sealed class SereinFlowActorAccessor : ISereinFlowActorAccessor
{
    private readonly AsyncLocal<SereinFlowActor?> _current = new();
    public SereinFlowActor? Current { get => _current.Value; set => _current.Value = value; }

    public IDisposable Push(SereinFlowActor? actor)
    {
        var previous = Current;
        Current = actor;
        {
            return new Restore(() => Current = previous);
        }
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        private Action? _restore = restore;
        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}

public sealed record McpRequestContext(string Transport, string? SessionId = null, string? RequestId = null);
public interface IMcpRequestContextAccessor
{
    McpRequestContext? Current { get; set; }
}

public sealed class McpRequestContextAccessor : IMcpRequestContextAccessor
{
    private readonly AsyncLocal<McpRequestContext?> _current = new();
    public McpRequestContext? Current { get => _current.Value; set => _current.Value = value; }
}

public sealed class SereinFlowAccess(ISereinFlowAuthorizationService authorization, ISereinFlowProjectScopeProvider scopes)
{
    public async ValueTask RequireAsync(SereinFlowActor? actor, SereinFlowPermission permission, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var resource = projectId is { } id ? SereinFlowResource.Project(id) : SereinFlowResource.Workspace;
        var operation = SereinFlowOperationNames.ToName(permission);
        await RequireResourceAsync(actor, operation, resource, cancellationToken);
    }

    public async ValueTask RequireResourceAsync(SereinFlowActor? actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        if (actor is null)
        {
            throw new SereinFlowAccessException(McpErrorCodes.Unauthenticated, "SereinFlow authentication is required.", 401);
        }

        var result = await authorization.AuthorizeAsync(actor, operation, resource, cancellationToken);
        if (!result.IsAllowed)
        {
            throw new SereinFlowAccessException(result.Code ?? McpErrorCodes.PermissionDenied, "Access denied.", 403);
        }
    }

    public ValueTask RequireAnyAsync(SereinFlowActor? actor, Guid? projectId, params SereinFlowPermission[] permissions)
        => RequireAnyAsync(actor, projectId, CancellationToken.None, permissions);

    public async ValueTask RequireAnyAsync(SereinFlowActor? actor, Guid? projectId, CancellationToken cancellationToken, params SereinFlowPermission[] permissions)
    {
        if (actor is null)
        {
            throw new SereinFlowAccessException(McpErrorCodes.Unauthenticated, "SereinFlow authentication is required.", 401);
        }

        foreach (var permission in permissions)
        {
            var resource = projectId is { } id ? SereinFlowResource.Project(id) : SereinFlowResource.Workspace;
            var operation = SereinFlowOperationNames.ToName(permission);
            var decision = await authorization.AuthorizeAsync(actor, operation, resource, cancellationToken);
            if (decision.IsAllowed)
            {
                return;
            }
        }

        throw new SereinFlowAccessException(McpErrorCodes.PermissionDenied, "Access denied.", 403);
    }

    public ValueTask RequireWorkspaceAsync(SereinFlowActor? actor) => RequireAsync(actor, SereinFlowPermission.WorkspaceManage);
    public ValueTask RequireWorkspaceAsync(SereinFlowActor? actor, CancellationToken cancellationToken)
        => RequireAsync(actor, SereinFlowPermission.WorkspaceManage, cancellationToken: cancellationToken);
    public async ValueTask<bool> CanManageWorkspaceAsync(SereinFlowActor actor, CancellationToken cancellationToken = default) => (await authorization.AuthorizeAsync(actor, SereinFlowOperations.WorkspaceManage, SereinFlowResource.Workspace, cancellationToken)).IsAllowed;
    public ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, SereinFlowPermission permission, CancellationToken cancellationToken = default)
    {
        var operation = SereinFlowOperationNames.ToName(permission);
        return scopes.GetScopeAsync(actor, operation, cancellationToken);
    }

    public async ValueTask<ProjectAccessScope> GetReadScopeAsync(SereinFlowActor actor, CancellationToken cancellationToken = default)
    {
        var runs = await GetScopeAsync(actor, SereinFlowPermission.RunRead, cancellationToken);
        var debug = await GetScopeAsync(actor, SereinFlowPermission.DebugRead, cancellationToken);
        if (runs.AllProjects || debug.AllProjects)
        {
            return ProjectAccessScope.All;
        }

        var projects = runs.ProjectIds.Concat(debug.ProjectIds).ToHashSet();
        return new ProjectAccessScope(false, projects);
    }
}

public sealed class SereinFlowAccessException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public static class SereinFlowAccessRegistration
{
    public static IServiceCollection AddSereinFlowAccess(this IServiceCollection services)
    {
        services.TryAddSingleton<ISereinFlowActorAccessor, SereinFlowActorAccessor>();
        services.TryAddScoped<SereinFlowAccess>();
        return services;
    }
}
