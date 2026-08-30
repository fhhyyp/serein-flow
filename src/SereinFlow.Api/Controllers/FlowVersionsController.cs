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

    public FlowVersionsController(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        IFlowVersionRepository versions,
        ProjectLibraryService projectLibraries)
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
    public async Task<ActionResult> List(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromQuery] string? track,
        CancellationToken cancellationToken)
    {
        if (!TryParseFlowVersionTrack(track, out var parsedTrack))
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The flow version track is invalid. 流程版本轨道无效。",
                extensions: new Dictionary<string, object?> { ["code"] = "flow.version_track_invalid" });
        }

        if (await _projects.FindAsync(projectId, cancellationToken) is null
            || await _flows.FindAsync(projectId, flowId, cancellationToken) is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
        }

        return Ok(await _versions.ListVersionsAsync(projectId, flowId, parsedTrack, cancellationToken));
    }

    [HttpGet("{version:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromRoute] long version,
        CancellationToken cancellationToken)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null
            || await _flows.FindAsync(projectId, flowId, cancellationToken) is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
        }

        var item = await _versions.FindVersionAsync(projectId, flowId, version, cancellationToken);
        return item is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Flow version not found. 未找到流程版本。")
            : Ok(item);
    }

    [HttpPost("/api/projects/{projectId:guid}/flows/{flowId:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publish(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromBody] PublishFlowVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedDevelopmentVersion < 1)
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "A positive development version is required. 需要有效的开发版本号。");
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        var development = await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (project is null || development is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
        if (project.Status == ProjectStatus.Archived)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "Archived projects cannot publish flow versions. 已归档项目不能发布流程版本。");
        }
        if (development.Version != request.ExpectedDevelopmentVersion)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "The development flow version changed before publishing. 开发流程版本已变更，无法发布。",
                extensions: new Dictionary<string, object?> { ["currentVersion"] = development.Version });
        }

        var validation = FlowDefinitionContractValidator.ValidateForExecution(development);
        if (!validation.IsValid)
            return BadRequest(validation);
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, development, cancellationToken);
        if (!libraryValidation.IsValid)
            return BadRequest(libraryValidation);

        var published = await _versions.PublishAsync(
            projectId,
            flowId,
            request.ExpectedDevelopmentVersion,
            request.Remark,
            await _versions.FindProductionVersionAsync(projectId, flowId, cancellationToken),
            cancellationToken);
        return published.IsCommitted
            ? Ok(published.Version)
            : ApiProblem(
                StatusCodes.Status409Conflict,
                "The development flow version changed before publishing. 开发流程版本已变更，无法发布。",
                extensions: new Dictionary<string, object?> { ["currentVersion"] = published.CurrentHeadVersion });
    }

    [HttpPost("{version:long}/rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Rollback(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromRoute] long version,
        [FromBody] RollbackFlowVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (version < 1 || request.ExpectedHeadVersion < 1 || !Enum.IsDefined(request.Track))
        {
            return ApiProblem(StatusCodes.Status400BadRequest, "The rollback request is invalid. 回滚请求无效。");
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null || await _flows.FindAsync(projectId, flowId, cancellationToken) is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
        if (project.Status == ProjectStatus.Archived)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "Archived projects cannot roll back flow versions. 已归档项目不能回滚流程版本。");
        }

        var source = await _versions.FindVersionAsync(projectId, flowId, version, cancellationToken);
        if (source is null || source.Version.Track != request.Track)
        {
            return ApiProblem(
                StatusCodes.Status404NotFound,
                "The requested flow version was not found on this track. 当前版本轨道中未找到要回滚的流程版本。");
        }

        var validation = FlowDefinitionContractValidator.ValidateForExecution(source.Definition);
        if (!validation.IsValid)
            return BadRequest(validation);
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, source.Definition, cancellationToken);
        if (!libraryValidation.IsValid)
            return BadRequest(libraryValidation);

        var rolledBack = await _versions.RollbackAsync(
            projectId,
            flowId,
            version,
            request.Track,
            request.ExpectedHeadVersion,
            cancellationToken);
        return rolledBack.IsCommitted
            ? Ok(rolledBack.Version)
            : ApiProblem(
                StatusCodes.Status409Conflict,
                "The flow version head changed before rollback. 流程版本头已变更，无法回滚。",
                extensions: new Dictionary<string, object?> { ["currentVersion"] = rolledBack.CurrentHeadVersion });
    }
}
