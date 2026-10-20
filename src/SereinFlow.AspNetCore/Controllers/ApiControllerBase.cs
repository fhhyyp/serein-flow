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

        var problem = string.IsNullOrWhiteSpace(result.Code)
            ? ApiProblem(result.StatusCode, result.Message)
            : ApiProblemWithCode(result.StatusCode, result.Message, result.Code);
        return problem;
    }

    protected ObjectResult ApiProblem(int statusCode, string? title, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = ApiProblemDetailsFactory.Create(statusCode, title, detail, extensions);
        var response = ToProblemResponse(problem);
        return response;
    }

    protected ObjectResult ApiProblemWithCode(int statusCode, string? title, string? code, long? currentVersion = null)
    {
        var problem = ApiProblemDetailsFactory.WithCode(statusCode, title, code, currentVersion);
        var response = ToProblemResponse(problem);
        return response;
    }

    protected ObjectResult ApiVersionProblem(int statusCode, string? title, long? currentVersion)
    {
        var problem = ApiProblemDetailsFactory.WithCurrentVersion(statusCode, title, currentVersion);
        var response = ToProblemResponse(problem);
        return response;
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

        var response = submission.CurrentVersion is null
            ? ApiProblem(submission.StatusCode, submission.ErrorTitle)
            : ApiVersionProblem(submission.StatusCode, submission.ErrorTitle, submission.CurrentVersion);
        return response;
    }

    protected ActionResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
    {
        ActionResult evaluatedValue4 = result.IsAccepted ? Accepted() : ApiProblem(result.StatusCode, result.ErrorTitle);
        return evaluatedValue4;
    }

    protected ActionResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
    {
        if (result.IsSuccess)
        {
            var response = Ok(result.References ?? []);
            return response;
        }

        var problem = string.IsNullOrWhiteSpace(result.Code)
            ? ApiProblem(result.StatusCode, result.Message)
            : ApiProblemWithCode(result.StatusCode, result.Message, result.Code);
        return problem;
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

        var response = ApiProblemWithCode(result.StatusCode, result.Message ?? "Library upgrade request failed. 类库升级请求失败。", result.Code, result.CurrentVersion);
        return response;
    }
}
