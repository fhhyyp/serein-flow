using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SereinFlow.Api;

/// <summary>Handles failures from SereinFlow controllers without changing the host's middleware.</summary>
public sealed class SereinFlowApiExceptionFilter(ILogger<SereinFlowApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        logger.LogError(context.Exception, "Unhandled SereinFlow API request failure.");
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected server error occurred. 服务器发生未预期错误。",
            Detail = "Please retry the request or contact support. 请重试请求或联系支持人员。",
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            ContentTypes = { "application/problem+json" },
        };
        context.ExceptionHandled = true;
    }
}
