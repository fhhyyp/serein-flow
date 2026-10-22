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
        var access = HttpContext.RequestServices.GetRequiredService<SereinFlowAccess>();
        await access.RequireAsync(actor, SereinFlowPermission.ProjectRead, cancellationToken: cancellationToken);
        var scope = await access.GetScopeAsync(actor!, SereinFlowPermission.ProjectRead, cancellationToken);
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
                var findProductionVersionAsyncResult = await _versions.FindProductionVersionAsync(project.Id, flow.Id, cancellationToken);
                var toFlowSummaryDtoResult = ToFlowSummaryDto(flow, findProductionVersionAsyncResult);
                summaries.Add(toFlowSummaryDtoResult);
            }

            var toProjectDtoResult = ToProjectDto(project);
            var projectWorkspaceDto = new ProjectWorkspaceDto(toProjectDtoResult, summaries);
            workspaces.Add(projectWorkspaceDto);
        }

        var toArrayResult = workspaces.ToArray();
        var okResult = Ok(toArrayResult);
        return okResult;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectWorkspaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create([FromBody] CreateProjectRequestDto? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Definition is null)
        {
            var badRequestResult = BadRequest(new { message = "A project name and initial flow definition are required. 项目名称和初始流程定义不能为空。", });
            return badRequestResult;
        }

        ProjectCreationCandidate candidate;
        try
        {
            candidate = _projectCreation.Prepare(request.Name, request.Definition);
        }
        catch (ArgumentException exception)
        {
            var badRequestResult1 = BadRequest(new { message = exception.Message });
            return badRequestResult1;
        }

        if (!candidate.Validation.IsValid)
        {
            var badRequestResult2 = BadRequest(candidate.Validation);
            return badRequestResult2;
        }

        var result = await _projectCreation.CreateAsync(candidate, cancellationToken);
        if (result.Status == ProjectCreationStatus.Conflict)
        {
            var conflictResult = Conflict(new { code = result.ErrorCode, message = result.ErrorMessage });
            return conflictResult;
        }

        if (result.Status != ProjectCreationStatus.Created)
        {
            var badRequestResult3 = BadRequest(candidate.Validation);
            return badRequestResult3;
        }

        var toProjectDtoResult4 = ToProjectDto(candidate.Project);
        IReadOnlyList<FlowDefinitionSummaryDto> evaluatedValue = [ToFlowSummaryDto(candidate.Definition)];
        var workspace = new ProjectWorkspaceDto(toProjectDtoResult4, evaluatedValue);
        var createdResult = Created($"/api/projects/{candidate.Project.Id:D}/flows/{candidate.Definition.Id:D}", workspace);
        return createdResult;
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
            var apiProblemResult = ApiProblem(StatusCodes.Status400BadRequest, "A non-empty project name and a positive expected version are required. 项目名称不能为空，期望版本必须为正数。");
            return apiProblemResult;
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            var apiProblemResult5 = ApiProblem(StatusCodes.Status404NotFound, "Project not found. 未找到项目。");
            return apiProblemResult5;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            var apiProblemResult6 = ApiProblem(StatusCodes.Status409Conflict, "Archived projects cannot be renamed. 已归档项目不能重命名。");
            return apiProblemResult6;
        }

        if (project.Version != request.ExpectedVersion)
        {
            var apiProblemResult7 = ApiVersionProblem(StatusCodes.Status409Conflict, "The project was changed by another editor. 项目已被其他编辑器修改。", currentVersion: project.Version);
            return apiProblemResult7;
        }

        project.Rename(request.Name);
        if (!await _projects.TryUpdateAsync(project, request.ExpectedVersion, cancellationToken))
        {
            var currentVersion = (await _projects.FindAsync(projectId, cancellationToken))?.Version;
            var apiProblemResult9 = ApiVersionProblem(StatusCodes.Status409Conflict, "The project was changed by another editor. 项目已被其他编辑器修改。", currentVersion: currentVersion);
            return apiProblemResult9;
        }

        var flows = await _flows.ListByProjectAsync(project.Id, cancellationToken);
        var summaries = new List<FlowDefinitionSummaryDto>(flows.Count);
        foreach (var flow in flows)
        {
            var findProductionVersionAsyncResult10 = await _versions.FindProductionVersionAsync(project.Id, flow.Id, cancellationToken);
            var toFlowSummaryDtoResult11 = ToFlowSummaryDto(flow, findProductionVersionAsyncResult10);
            summaries.Add(toFlowSummaryDtoResult11);
        }

        var toProjectDtoResult12 = ToProjectDto(project);
        var projectWorkspaceDto13 = new ProjectWorkspaceDto(toProjectDtoResult12, summaries);
        var okResult14 = Ok(projectWorkspaceDto13);
        return okResult14;
    }

    [HttpPost("{projectId:guid}/archive")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectArchive.ArchiveAsync(projectId, cancellationToken);
        ActionResult evaluatedValue19 = result.IsSuccess ? resolveOkResult() : resolveApiProblemResult();
        return evaluatedValue19;
        ObjectResult resolveOkResult()
        {
            var toProjectDtoResult15 = ToProjectDto(result.Project!);
            var okResult16 = Ok(toProjectDtoResult15);
            return okResult16;
        }

        ObjectResult resolveApiProblemResult()
        {
            var apiProblemResult18 = ApiProblemWithCode(result.StatusCode, result.Message, code: result.Code);
            return apiProblemResult18;
        }
    }
}
