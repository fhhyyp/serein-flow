using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SereinFlow.Application;

namespace SereinFlow.Api;
/// <summary>
/// Marks actions that perform their own API-key authorization.
/// 标记自行执行 API 密钥授权的操作。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SereinFlowScopedAuthorizationAttribute : Attribute;
/// <summary>
/// Marks the health endpoint and the loopback-only initial key setup.
/// 标记健康检查端点和仅限回环访问的初始密钥设置端点。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SereinFlowAnonymousAttribute : Attribute;
public sealed class SereinFlowRestAuthorizationFilter(IConfiguration configuration, McpSecurityService security) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (resolveAnyResult() || !configuration.GetValue("SereinFlow:Api:RequireAuthentication", true))
        {
            return;
        }

        var ofTypeResult1 = context.ActionDescriptor.EndpointMetadata.OfType<SereinFlowScopedAuthorizationAttribute>();
        if (ofTypeResult1.Any())
        {
            return;
        }

        var raw = context.HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (!AuthenticationHeaderValue.TryParse(raw, out var header) || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
        {
            Deny(context, StatusCodes.Status401Unauthorized, "SereinFlow API authentication is required. 需要进行 SereinFlow API 身份验证。");
            return;
        }

        var principal = await security.AuthenticateAsync(header.Parameter, context.HttpContext.RequestAborted);
        if (principal is null)
        {
            Deny(context, StatusCodes.Status401Unauthorized, "Invalid SereinFlow API key. SereinFlow API 密钥无效。");
            return;
        }

        try
        {
            security.RequireAdministrator(principal);
        }
        catch (McpSecurityException exception)
        {
            Deny(context, exception.StatusCode, exception.Message);
        }

        bool resolveAnyResult()
        {
            var ofTypeResult = context.ActionDescriptor.EndpointMetadata.OfType<SereinFlowAnonymousAttribute>();
            var anyResult = ofTypeResult.Any();
            return anyResult;
        }
    }

    private static void Deny(AuthorizationFilterContext context, int status, string title)
    {
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title
        };
        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = status,
            ContentTypes =
            {
                "application/problem+json"
            }
        };
    }
}
