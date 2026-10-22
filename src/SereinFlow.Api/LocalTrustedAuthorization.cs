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
        var argument4 = Encoding.UTF8.GetBytes(input);
        var argument3 = SHA256.HashData(argument4);
        var id = "local:" + Convert.ToHexString(argument3).ToLowerInvariant();
        {
            var subjectClaim = new Claim(ClaimTypes.NameIdentifier, id);
            var argument2 = new ClaimsIdentity([subjectClaim], "SereinFlow.Local");
            var argument1 = new ClaimsPrincipal(argument2);
            SereinFlowActor result1 = new(id, argument1);
            return result1;
        }
    }

    public ValueTask<AccessDecision> AuthorizeAsync(SereinFlowActor actor, string operation, SereinFlowResource resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        {
            var result2 = ValueTask.FromResult(actor.Principal.Identity?.AuthenticationType == "SereinFlow.Local" ? AccessDecision.Allowed : AccessDecision.Denied);
            return result2;
        }
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
