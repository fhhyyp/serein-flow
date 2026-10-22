using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Api;
/// <summary>
/// Captures the outcome of serein flow API authorization operations.
/// 记录Serein流程APIAuthorization操作的结果。
/// </summary>
public sealed record SereinFlowApiAuthorizationResult(bool IsAllowed, int StatusCode = StatusCodes.Status200OK, string? Code = null, string? Message = null)
{
    public static SereinFlowApiAuthorizationResult Allowed { get; } = new(true);
}

/// <summary>
/// Applies the shared resource authorization contract to REST execution requests.
/// 使用统一的资源授权契约检查 REST 执行请求。
/// </summary>
public sealed class SereinFlowApiAuthorizationService
{
    private readonly SereinFlowAccess _security;
    public SereinFlowApiAuthorizationService(SereinFlowAccess security)
    {
        _security = security;
    }

    public async Task<SereinFlowApiAuthorizationResult> AuthorizeAsync(HttpContext context, SereinFlowPermission permission, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var principal = SereinFlow.AspNetCore.SereinFlowHttpActor.Get(context);
        if (principal is null)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            {
                var result1 = Unauthorized();
                return result1;
            }
        }

        try
        {
            var routeRun = context.Request.RouteValues["runId"]?.ToString();
            if (projectId is null && Guid.TryParse(routeRun, out var requestedRun))
            {
                var runs = context.RequestServices.GetRequiredService<IFlowRunStore>();
                var run = await runs.FindAsync(requestedRun, cancellationToken);
                if (run is null)
                {
                    var missing = new SereinFlowApiAuthorizationResult(false, 404, RunErrorCodes.NotFound, "Run not found. 未找到运行实例。");
                    return missing;
                }
                projectId = run.ProjectId;
            }
            var resource = projectId is { } id ? SereinFlowResource.Project(id) : SereinFlowResource.Workspace;
            // Controllers resolve the stored resource before passing its project ownership here.
            if (projectId is { } owner)
            {
                var runValue = context.Request.RouteValues["runId"]?.ToString();
                var flowValue = context.Request.RouteValues["flowId"]?.ToString();
                if (Guid.TryParse(runValue, out var runId))
                {
                    resource = SereinFlowResource.Run(runId, owner);
                }
                else if (Guid.TryParse(flowValue, out var flowId))
                {
                    resource = SereinFlowResource.Flow(flowId, owner);
                }
            }

            var operation = SereinFlowOperationNames.ToName(permission);
            await _security.RequireResourceAsync(principal, operation, resource, cancellationToken);
            return SereinFlowApiAuthorizationResult.Allowed;
        }
        catch (SereinFlowAccessException exception)
        {
            var sereinFlowApiAuthorizationResult = new SereinFlowApiAuthorizationResult(false, exception.StatusCode, exception.Code, exception.Message);
            return sereinFlowApiAuthorizationResult;
        }
    }

    private static SereinFlowApiAuthorizationResult Unauthorized()
    {
        SereinFlowApiAuthorizationResult evaluatedValue = new(false, StatusCodes.Status401Unauthorized, McpErrorCodes.Unauthenticated, "SereinFlow API authentication is required. SereinFlow API 需要鉴权。");
        return evaluatedValue;
    }
}
