using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SereinFlow.Application;

namespace SereinFlow.Api;
/// <summary>
/// Marks actions that perform their own resource authorization.
/// 标记自行执行资源授权的操作。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SereinFlowScopedAuthorizationAttribute : Attribute;
/// <summary>
/// Marks the health endpoint and the loopback-only initial key setup.
/// 标记健康检查端点和仅限回环访问的初始密钥设置端点。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SereinFlowAnonymousAttribute : Attribute;
public sealed class SereinFlowRestAuthorizationFilter(SereinFlowAccess security) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (resolveAnyResult())
        {
            return;
        }

        var ofTypeResult1 = context.ActionDescriptor.EndpointMetadata.OfType<SereinFlowScopedAuthorizationAttribute>();
        if (ofTypeResult1.Any())
        {
            return;
        }

        var principal = SereinFlow.AspNetCore.SereinFlowHttpActor.Get(context.HttpContext);
        if (principal is null)
        {
            Deny(context, 401, "Authentication is required.");
            return;
        }

        try
        {
            await security.RequireWorkspaceAsync(principal, context.HttpContext.RequestAborted);
        }
        catch (SereinFlowAccessException exception)
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
