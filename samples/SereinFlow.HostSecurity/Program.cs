using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using SereinFlow.Application;
using SereinFlow.AspNetCore;
using SereinFlow.Contracts;

var builder = WebApplication.CreateBuilder(args);
// Replace the cookie registration with the existing host's identity provider.
builder.Services.AddAuthentication("ExistingHost").AddCookie("ExistingHost", options =>
{
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
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Enum.GetValues<SereinFlowPermission>())
    {
        var operation = SereinFlowOperationNames.ToName(permission);
        options.AddPolicy(operation, policy =>
        {
            var requirement = new HostOperation(operation);
            policy.AddRequirements(requirement);
        });
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, HostOperationHandler>();
builder.Services.AddSingleton<ISereinFlowProjectScopeProvider, HostProjectScopes>();
builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi();
    options.UseMcp();
    options.UseHostSecurity();
});
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseSereinFlow();
app.MapSereinFlow();
app.Run();
sealed record HostOperation(string Name) : IAuthorizationRequirement;
sealed class HostOperationHandler : AuthorizationHandler<HostOperation, SereinFlowResource>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HostOperation requirement, SereinFlowResource resource)
    {
        var project = resource.ProjectId?.ToString("D");
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim("operation", requirement.Name) && (project is null || context.User.HasClaim("project", project)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

sealed class HostProjectScopes : ISereinFlowProjectScopeProvider
{
    public ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!actor.Principal.HasClaim("operation", operation))
        {
            var result1 = ValueTask.FromResult(ProjectAccessScope.None);
            return result1;
        }

        var projects = actor.Principal.FindAll("project").Select(claim =>
        {
            var value1 = Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
            return value1;
        }).Where(id => id != Guid.Empty).ToHashSet();
        {
            var argument1 = new ProjectAccessScope(false, projects);
            var result2 = ValueTask.FromResult(argument1);
            return result2;
        }
    }
}
