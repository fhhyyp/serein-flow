using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SereinFlow.Application;

namespace SereinFlow.Api;
// Selected only by a local process host; request arguments never establish trust.
public sealed class LocalTrustedAuthorization : ISereinFlowAuthorizationService, ISereinFlowProjectScopeProvider
{
    public static SereinFlowActor CreateActor(string dataRoot)
    {
        var input = $"{Environment.UserDomainName}\\{Environment.UserName}\n{Path.GetFullPath(dataRoot)}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var id = "local:" + Convert.ToHexString(hashBytes).ToLowerInvariant();
        var subjectClaim = new Claim(ClaimTypes.NameIdentifier, id);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([subjectClaim], "SereinFlow.Local"));
        return new(id, principal);
    }

    public ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(actor.Principal.Identity?.AuthenticationType == "SereinFlow.Local" ? AccessDecision.Allowed : AccessDecision.Denied);
    }

    public ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default) => ValueTask.FromResult(actor.Principal.Identity?.AuthenticationType == "SereinFlow.Local" ? ProjectAccessScope.All : ProjectAccessScope.None);
}

internal static class LocalAccessRegistration
{
    internal static IServiceCollection AddSereinFlowLocalAccess(this IServiceCollection services)
    {
        services.AddSereinFlowAccess();
        services.AddSingleton<ISereinFlowAuthorizationService, LocalTrustedAuthorization>();
        services.AddSingleton<ISereinFlowProjectScopeProvider, LocalTrustedAuthorization>();
        return services;
    }
}
