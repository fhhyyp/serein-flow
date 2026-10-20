using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;
[Route("api/projects/{projectId:guid}/flows/{flowId:guid}/versions")]
public sealed class FlowVersionsController : ApiControllerBase
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowVersionRepository _versions;
    private readonly ProjectLibraryService _projectLibraries;
    public FlowVersionsController(IProjectRepository projects, IFlowDefinitionRepository flows, IFlowVersionRepository versions, ProjectLibraryService projectLibraries)
    {
        _projects = projects;
        _flows = flows;
        _versions = versions;
        _projectLibraries = projectLibraries;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> List([FromRoute] Guid projectId, [FromRoute] Guid flowId, [FromQuery] string? track, CancellationToken cancellationToken)
    {
        if (!TryParseFlowVersionTrack(track, out var parsedTrack))
        {
            var dictionary = new Dictionary<string, object?>
            {
                ["code"] = FlowErrorCodes.VersionTrackInvalid
            };
            var apiProblemResult = ApiProblem(StatusCodes.Status400BadRequest, "The flow version track is invalid. 流程版本轨道无效。", extensions: dictionary);
            return apiProblemResult;
        }

        if (await _projects.FindAsync(projectId, cancellationToken)is null || await _flows.FindAsync(projectId, flowId, cancellationToken)is null)
        {
            var apiProblemResult1 = ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
            return apiProblemResult1;
        }

        var listVersionsAsyncResult = await _versions.ListVersionsAsync(projectId, flowId, parsedTrack, cancellationToken);
        var okResult = Ok(listVersionsAsyncResult);
        return okResult;
    }

    [HttpGet("{version:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] Guid projectId, [FromRoute] Guid flowId, [FromRoute] long version, CancellationToken cancellationToken)
    {
        if (await _projects.FindAsync(projectId, cancellationToken)is null || await _flows.FindAsync(projectId, flowId, cancellationToken)is null)
        {
            var apiProblemResult2 = ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
            return apiProblemResult2;
        }

        var item = await _versions.FindVersionAsync(projectId, flowId, version, cancellationToken);
        ActionResult evaluatedValue = item is null ? ApiProblem(StatusCodes.Status404NotFound, "Flow version not found. 未找到流程版本。") : Ok(item);
        return evaluatedValue;
    }

    [HttpPost("/api/projects/{projectId:guid}/flows/{flowId:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publish([FromRoute] Guid projectId, [FromRoute] Guid flowId, [FromBody] PublishFlowVersionRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ExpectedDevelopmentVersion < 1)
        {
            var apiProblemResult3 = ApiProblem(StatusCodes.Status400BadRequest, "A positive development version is required. 需要有效的开发版本号。");
            return apiProblemResult3;
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        var development = await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (project is null || development is null)
        {
            var apiProblemResult4 = ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
            return apiProblemResult4;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            var apiProblemResult5 = ApiProblem(StatusCodes.Status409Conflict, "Archived projects cannot publish flow versions. 已归档项目不能发布流程版本。");
            return apiProblemResult5;
        }

        if (development.Version != request.ExpectedDevelopmentVersion)
        {
            var dictionary6 = new Dictionary<string, object?>
            {
                ["currentVersion"] = development.Version
            };
            var apiProblemResult7 = ApiProblem(StatusCodes.Status409Conflict, "The development flow version changed before publishing. 开发流程版本已变更，无法发布。", extensions: dictionary6);
            return apiProblemResult7;
        }

        var validation = FlowDefinitionContractValidator.ValidateForExecution(development);
        if (!validation.IsValid)
        {
            var badRequestResult = BadRequest(validation);
            return badRequestResult;
        }

        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, development, cancellationToken);
        if (!libraryValidation.IsValid)
        {
            var badRequestResult8 = BadRequest(libraryValidation);
            return badRequestResult8;
        }

        var findProductionVersionAsyncResult = await _versions.FindProductionVersionAsync(projectId, flowId, cancellationToken);
        var published = await _versions.PublishAsync(projectId, flowId, request.ExpectedDevelopmentVersion, request.Remark, findProductionVersionAsyncResult, cancellationToken);
        ActionResult evaluatedValue11 = published.IsCommitted ? Ok(published.Version) : resolveApiProblemResult();
        return evaluatedValue11;
        ObjectResult resolveApiProblemResult()
        {
            var dictionary9 = new Dictionary<string, object?>
            {
                ["currentVersion"] = published.CurrentHeadVersion
            };
            var apiProblemResult10 = ApiProblem(StatusCodes.Status409Conflict, "The development flow version changed before publishing. 开发流程版本已变更，无法发布。", extensions: dictionary9);
            return apiProblemResult10;
        }
    }

    [HttpPost("{version:long}/rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Rollback([FromRoute] Guid projectId, [FromRoute] Guid flowId, [FromRoute] long version, [FromBody] RollbackFlowVersionRequestDto request, CancellationToken cancellationToken)
    {
        if (version < 1 || request.ExpectedHeadVersion < 1 || !Enum.IsDefined(request.Track))
        {
            var apiProblemResult12 = ApiProblem(StatusCodes.Status400BadRequest, "The rollback request is invalid. 回滚请求无效。");
            return apiProblemResult12;
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null || await _flows.FindAsync(projectId, flowId, cancellationToken)is null)
        {
            var apiProblemResult13 = ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
            return apiProblemResult13;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            var apiProblemResult14 = ApiProblem(StatusCodes.Status409Conflict, "Archived projects cannot roll back flow versions. 已归档项目不能回滚流程版本。");
            return apiProblemResult14;
        }

        var source = await _versions.FindVersionAsync(projectId, flowId, version, cancellationToken);
        if (source is null || source.Version.Track != request.Track)
        {
            var apiProblemResult15 = ApiProblem(StatusCodes.Status404NotFound, "The requested flow version was not found on this track. 当前版本轨道中未找到要回滚的流程版本。");
            return apiProblemResult15;
        }

        var validation = FlowDefinitionContractValidator.ValidateForExecution(source.Definition);
        if (!validation.IsValid)
        {
            var badRequestResult16 = BadRequest(validation);
            return badRequestResult16;
        }

        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, source.Definition, cancellationToken);
        if (!libraryValidation.IsValid)
        {
            var badRequestResult17 = BadRequest(libraryValidation);
            return badRequestResult17;
        }

        var rolledBack = await _versions.RollbackAsync(projectId, flowId, version, request.Track, request.ExpectedHeadVersion, cancellationToken);
        ActionResult evaluatedValue21 = rolledBack.IsCommitted ? Ok(rolledBack.Version) : resolveApiProblemResult18();
        return evaluatedValue21;
        ObjectResult resolveApiProblemResult18()
        {
            var dictionary19 = new Dictionary<string, object?>
            {
                ["currentVersion"] = rolledBack.CurrentHeadVersion
            };
            var apiProblemResult20 = ApiProblem(StatusCodes.Status409Conflict, "The flow version head changed before rollback. 流程版本头已变更，无法回滚。", extensions: dictionary19);
            return apiProblemResult20;
        }
    }
}
