using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;
[ApiController]
[EnableCors("sereinflow-webapi")]
[ServiceFilter(typeof(SereinFlowRestAuthorizationFilter))]
[ServiceFilter(typeof(SereinFlowApiExceptionFilter))]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult? ToAuthorizationFailure(SereinFlowApiAuthorizationResult result)
    {
        ActionResult? evaluatedValue1 = result.IsAllowed ? null : resolveApiProblemResult();
        return evaluatedValue1;
        ObjectResult resolveApiProblemResult()
        {
            IReadOnlyDictionary<string, object>? evaluatedValue = string.IsNullOrWhiteSpace(result.Code) ? null : new Dictionary<string, object?>
            {
                ["code"] = result.Code
            };
            var apiProblemResult = ApiProblem(result.StatusCode, result.Message, extensions: evaluatedValue);
            return apiProblemResult;
        }
    }

    protected ObjectResult ApiProblem(int statusCode, string? title, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = string.IsNullOrWhiteSpace(title) ? "The request could not be completed. 请求无法完成。" : title,
            Detail = detail,
        };
        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                problem.Extensions[extension.Key] = extension.Value;
            }
        }

        var result = new ObjectResult(problem)
        {
            StatusCode = statusCode
        };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    protected ActionResult ToRunSubmissionResponse(RunSubmissionResult submission)
    {
        if (submission.IsAccepted)
        {
            var toRunDtoResult = ApiEndpointHelpers.ToRunDto(submission.Run);
            var acceptedResult = Accepted($"/api/runs/{submission.Run!.Id:D}", toRunDtoResult);
            return acceptedResult;
        }

        if (submission.ErrorBody is not null)
        {
            var jsonResult = new JsonResult(submission.ErrorBody)
            {
                StatusCode = submission.StatusCode
            };
            return jsonResult;
        }

        IReadOnlyDictionary<string, object>? evaluatedValue2 = submission.CurrentVersion is null ? null : new Dictionary<string, object?>
        {
            ["currentVersion"] = submission.CurrentVersion
        };
        var apiProblemResult3 = ApiProblem(submission.StatusCode, submission.ErrorTitle, extensions: evaluatedValue2);
        return apiProblemResult3;
    }

    protected ActionResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
    {
        ActionResult evaluatedValue4 = result.IsAccepted ? Accepted() : ApiProblem(result.StatusCode, result.ErrorTitle);
        return evaluatedValue4;
    }

    protected ActionResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
    {
        ActionResult evaluatedValue8 = result.IsSuccess ? Ok(result.References ?? []) : resolveApiProblemResult5();
        return evaluatedValue8;
        ObjectResult resolveApiProblemResult5()
        {
            IReadOnlyDictionary<string, object>? evaluatedValue6 = string.IsNullOrWhiteSpace(result.Code) ? null : new Dictionary<string, object?>
            {
                ["code"] = result.Code
            };
            var apiProblemResult7 = ApiProblem(result.StatusCode, result.Message, extensions: evaluatedValue6);
            return apiProblemResult7;
        }
    }

    protected ActionResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            ActionResult evaluatedValue10 = result.StatusCode == StatusCodes.Status201Created ? resolveCreatedResult() : Ok(result.Value);
            return evaluatedValue10;
            ActionResult resolveCreatedResult()
            {
                var evaluatedValue9 = $"/api/projects/library-upgrades/{ApiEndpointHelpers.GetUpgradeId(result.Value)}";
                var createdResult = Created(evaluatedValue9, result.Value);
                return createdResult;
            }
        }

        var extensions = new Dictionary<string, object?>
        {
            ["code"] = result.Code
        };
        if (result.CurrentVersion is not null)
        {
            extensions["currentVersion"] = result.CurrentVersion;
        }

        var apiProblemResult11 = ApiProblem(result.StatusCode, result.Message ?? "Library upgrade request failed. 类库升级请求失败。", extensions: extensions);
        return apiProblemResult11;
    }
}
