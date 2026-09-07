using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Api;

public sealed record SereinFlowApiAuthorizationResult(
    bool IsAllowed,
    int StatusCode = StatusCodes.Status200OK,
    string? Code = null,
    string? Message = null)
{
    public static SereinFlowApiAuthorizationResult Allowed { get; } = new(true);
}

/// <summary>
/// Reuses the MCP API-key verifier for the client-facing REST execution
/// surface without coupling the SDK or REST controllers to MCP transport.
/// </summary>
public sealed class SereinFlowApiAuthorizationService
{
    private readonly IConfiguration _configuration;
    private readonly McpSecurityService _security;

    public SereinFlowApiAuthorizationService(
        IConfiguration configuration,
        McpSecurityService security)
    {
        _configuration = configuration;
        _security = security;
    }

    public async Task<SereinFlowApiAuthorizationResult> AuthorizeAsync(
        HttpContext context,
        McpPermissionDto permission,
        Guid? projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue<bool>("SereinFlow:Api:RequireAuthentication"))
            return SereinFlowApiAuthorizationResult.Allowed;

        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            return Unauthorized();
        }

        var principal = await _security.AuthenticateAsync(header.Parameter, cancellationToken);
        if (principal is null)
        {
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            return Unauthorized();
        }

        try
        {
            _security.Require(principal, permission, projectId);
            return SereinFlowApiAuthorizationResult.Allowed;
        }
        catch (McpSecurityException exception)
        {
            return new SereinFlowApiAuthorizationResult(
                false,
                exception.StatusCode,
                exception.Code,
                exception.Message);
        }
    }

    private static SereinFlowApiAuthorizationResult Unauthorized()
        => new(
            false,
            StatusCodes.Status401Unauthorized,
            McpErrorCodes.Unauthenticated,
            "SereinFlow API authentication is required. SereinFlow API 需要鉴权。");
}
