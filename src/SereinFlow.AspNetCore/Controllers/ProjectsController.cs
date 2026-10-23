using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;

[Route("api/projects")]
public sealed class ProjectsController : ApiControllerBase
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowVersionRepository _versions;
    private readonly ProjectCreationService _projectCreation;
    private readonly ProjectArchiveService _projectArchive;
    public ProjectsController(IProjectRepository projects, IFlowDefinitionRepository flows, IFlowVersionRepository versions, ProjectCreationService projectCreation, ProjectArchiveService projectArchive)
    {
        _projects = projects;
        _flows = flows;
        _versions = versions;
        _projectCreation = projectCreation;
        _projectArchive = projectArchive;
    }

    [HttpGet]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(ProjectWorkspaceDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] bool? includeArchived, CancellationToken cancellationToken)
    {
        var actor = SereinFlow.AspNetCore.SereinFlowHttpActor.Get(HttpContext);
        var sereinFlowAccess = HttpContext.RequestServices.GetRequiredService<SereinFlowAccess>();
        await sereinFlowAccess.RequireAsync(actor, SereinFlowPermission.ProjectRead, cancellationToken: cancellationToken);
        var scope = await sereinFlowAccess.GetScopeAsync(actor!, SereinFlowPermission.ProjectRead, cancellationToken);
        var workspaces = new List<ProjectWorkspaceDto>();
        foreach (var project in await _projects.ListAsync(cancellationToken))
        {
            if (!scope.Contains(project.Id) || includeArchived != true && project.Status == ProjectStatus.Archived)
            {
                continue;
            }

            var flows = await _flows.ListByProjectAsync(project.Id, cancellationToken);
            var summaries = new List<FlowDefinitionSummaryDto>(flows.Count);
            foreach (var flow in flows)
            {
                var flowSummaryDto = ToFlowSummaryDto(flow, (await _versions.FindProductionVersionAsync(project.Id, flow.Id, cancellationToken)));
                summaries.Add(flowSummaryDto);
            }
            var projectWorkspaceDto = new ProjectWorkspaceDto(ToProjectDto(project), summaries);
            workspaces.Add(projectWorkspaceDto);
        }

        var mappedWorkspaces = workspaces.ToArray();
        return Ok(mappedWorkspaces);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectWorkspaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create([FromBody] CreateProjectRequestDto? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Definition is null)
        {
            return BadRequest(new { message = "A project name and initial flow definition are required. 项目名称和初始流程定义不能为空。", });
        }

        ProjectCreationCandidate candidate;
        try
        {
            candidate = _projectCreation.Prepare(request.Name, request.Definition);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        if (!candidate.Validation.IsValid)
        {
            return BadRequest(candidate.Validation);
        }

        var result = await _projectCreation.CreateAsync(candidate, cancellationToken);
        if (result.Status == ProjectCreationStatus.Conflict)
        {
            return Conflict(new { code = result.ErrorCode, message = result.ErrorMessage });
        }

        if (result.Status != ProjectCreationStatus.Created)
        {
            return BadRequest(candidate.Validation);
        }

        var project = ToProjectDto(candidate.Project);
        IReadOnlyList<FlowDefinitionSummaryDto> flows = [ToFlowSummaryDto(candidate.Definition)];
        var workspace = new ProjectWorkspaceDto(project, flows);
        return Created($"/api/projects/{candidate.Project.Id:D}/flows/{candidate.Definition.Id:D}", workspace);
    }

    [HttpPut("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectWorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Rename([FromRoute] Guid projectId, [FromBody] RenameProjectRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblem(StatusCodes.Status400BadRequest, "A non-empty project name and a positive expected version are required. 项目名称不能为空，期望版本必须为正数。");
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Project not found. 未找到项目。");
        }

        if (project.Status == ProjectStatus.Archived)
        {
            return ApiProblem(StatusCodes.Status409Conflict, "Archived projects cannot be renamed. 已归档项目不能重命名。");
        }

        if (project.Version != request.ExpectedVersion)
        {
            return ApiVersionProblem(StatusCodes.Status409Conflict, "The project was changed by another editor. 项目已被其他编辑器修改。", currentVersion: project.Version);
        }

        project.Rename(request.Name);
        if (!await _projects.TryUpdateAsync(project, request.ExpectedVersion, cancellationToken))
        {
            var currentVersion = (await _projects.FindAsync(projectId, cancellationToken))?.Version;
            return ApiVersionProblem(StatusCodes.Status409Conflict, "The project was changed by another editor. 项目已被其他编辑器修改。", currentVersion: currentVersion);
        }

        var flows = await _flows.ListByProjectAsync(project.Id, cancellationToken);
        var summaries = new List<FlowDefinitionSummaryDto>(flows.Count);
        foreach (var flow in flows)
        {
            var flowSummaryDto = ToFlowSummaryDto(flow, (await _versions.FindProductionVersionAsync(project.Id, flow.Id, cancellationToken)));
            summaries.Add(flowSummaryDto);
        }
        var projectWorkspaceDto = new ProjectWorkspaceDto(ToProjectDto(project), summaries);
        return Ok(projectWorkspaceDto);
    }

    [HttpPost("{projectId:guid}/archive")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectArchive.ArchiveAsync(projectId, cancellationToken);
        return result.IsSuccess ? CreateProjectResponse() : CreateFailureResponse();
        ObjectResult CreateProjectResponse()
        {
            var projectDto = ToProjectDto(result.Project!);
            return Ok(projectDto);
        }

        ObjectResult CreateFailureResponse()
        {
            return ApiProblemWithCode(result.StatusCode, result.Message, code: result.Code);
        }
    }
}
