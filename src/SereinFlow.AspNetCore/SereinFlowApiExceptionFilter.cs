using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SereinFlow.Api;
/// <summary>
/// Handles failures from SereinFlow controllers without changing the host's middleware.
/// 处理 SereinFlow 控制器的失败，不改变宿主中间件。
/// </summary>
public sealed class SereinFlowApiExceptionFilter(ILogger<SereinFlowApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is SereinFlow.Application.SereinFlowAccessException denied)
        {
            if (denied.StatusCode == 401)
            {
                context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
            }

            var mappedProblemDetails = new ProblemDetails
            {
                Status = denied.StatusCode,
                Title = denied.Message,
                Extensions =
                {
                    ["code"] = denied.Code
                }
            };
            context.Result = new ObjectResult(mappedProblemDetails)
            {
                StatusCode = denied.StatusCode
            };
            context.ExceptionHandled = true;
            return;
        }

        HostingLog.ApiRequestFailed(logger, context.Exception);
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected server error occurred. 服务器发生未预期错误。",
            Detail = "Please retry the request or contact support. 请重试请求或联系支持人员。",
        };
        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            ContentTypes =
            {
                "application/problem+json"
            }
        };
        context.ExceptionHandled = true;
    }
}
