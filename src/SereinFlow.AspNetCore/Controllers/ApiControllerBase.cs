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
        if (result.IsAllowed)
        {
            return null;
        }
        return string.IsNullOrWhiteSpace(result.Code)
            ? ApiProblem(result.StatusCode, result.Message)
            : ApiProblemWithCode(result.StatusCode, result.Message, result.Code);
    }

    protected ObjectResult ApiProblem(int statusCode, string? title, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = ApiProblemDetailsFactory.Create(statusCode, title, detail, extensions);
        return ToProblemResponse(problem);
    }

    protected ObjectResult ApiProblemWithCode(int statusCode, string? title, string? code, long? currentVersion = null)
    {
        var problem = ApiProblemDetailsFactory.WithCode(statusCode, title, code, currentVersion);
        return ToProblemResponse(problem);
    }

    protected ObjectResult ApiVersionProblem(int statusCode, string? title, long? currentVersion)
    {
        var problem = ApiProblemDetailsFactory.WithCurrentVersion(statusCode, title, currentVersion);
        return ToProblemResponse(problem);
    }

    private static ObjectResult ToProblemResponse(ProblemDetails problem)
    {
        if (string.IsNullOrWhiteSpace(problem.Title))
        {
            problem.Title = "The request could not be completed. 请求无法完成。";
        }

        var response = new ObjectResult(problem) { StatusCode = problem.Status };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }

    protected ActionResult ToRunSubmissionResponse(RunSubmissionResult submission)
    {
        if (submission.IsAccepted)
        {
            var runDto = ApiEndpointHelpers.ToRunDto(submission.Run);
            return Accepted($"/api/runs/{submission.Run!.Id:D}", runDto);
        }

        if (submission.ErrorBody is not null)
        {
            return new JsonResult(submission.ErrorBody)
            {
                StatusCode = submission.StatusCode
            };
        }
        return submission.CurrentVersion is null
            ? ApiProblem(submission.StatusCode, submission.ErrorTitle)
            : ApiVersionProblem(submission.StatusCode, submission.ErrorTitle, submission.CurrentVersion);
    }

    protected ActionResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
    {
        return result.IsAccepted ? Accepted() : ApiProblem(result.StatusCode, result.ErrorTitle);
    }

    protected ActionResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.References ?? []);
        }
        return string.IsNullOrWhiteSpace(result.Code)
            ? ApiProblem(result.StatusCode, result.Message)
            : ApiProblemWithCode(result.StatusCode, result.Message, result.Code);
    }

    protected ActionResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode == StatusCodes.Status201Created ? CreateUpgradeResponse() : Ok(result.Value);
            ActionResult CreateUpgradeResponse()
            {
                var upgradeLocation = $"/api/projects/library-upgrades/{ApiEndpointHelpers.GetUpgradeId(result.Value)}";
                return Created(upgradeLocation, result.Value);
            }
        }
        return ApiProblemWithCode(result.StatusCode, result.Message ?? "Library upgrade request failed. 类库升级请求失败。", result.Code, result.CurrentVersion);
    }
}
