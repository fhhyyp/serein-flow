using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ApiProblem(
        int statusCode,
        string? title,
        string? detail = null,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = string.IsNullOrWhiteSpace(title)
                ? "The request could not be completed. 请求无法完成。"
                : title,
            Detail = detail,
        };

        if (extensions is not null)
        {
            foreach (var extension in extensions)
                problem.Extensions[extension.Key] = extension.Value;
        }

        var result = new ObjectResult(problem) { StatusCode = statusCode };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    protected ActionResult ToRunSubmissionResponse(RunSubmissionResult submission)
    {
        if (submission.IsAccepted)
            return Accepted($"/api/runs/{submission.Run!.Id:D}", ApiEndpointHelpers.ToRunDto(submission.Run));
        if (submission.ErrorBody is not null)
            return new JsonResult(submission.ErrorBody) { StatusCode = submission.StatusCode };

        return ApiProblem(
            submission.StatusCode,
            submission.ErrorTitle,
            extensions: submission.CurrentVersion is null
                ? null
                : new Dictionary<string, object?> { ["currentVersion"] = submission.CurrentVersion });
    }

    protected ActionResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
        => result.IsAccepted
            ? Accepted()
            : ApiProblem(result.StatusCode, result.ErrorTitle);

    protected ActionResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
        => result.IsSuccess
            ? Ok(result.References ?? [])
            : ApiProblem(
                result.StatusCode,
                result.Message,
                extensions: string.IsNullOrWhiteSpace(result.Code)
                    ? null
                    : new Dictionary<string, object?> { ["code"] = result.Code });

    protected ActionResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode == StatusCodes.Status201Created
                ? Created($"/api/projects/library-upgrades/{ApiEndpointHelpers.GetUpgradeId(result.Value)}", result.Value)
                : Ok(result.Value);
        }

        var extensions = new Dictionary<string, object?> { ["code"] = result.Code };
        if (result.CurrentVersion is not null)
            extensions["currentVersion"] = result.CurrentVersion;

        return ApiProblem(
            result.StatusCode,
            result.Message ?? "Library upgrade request failed. 类库升级请求失败。",
            extensions: extensions);
    }
}
