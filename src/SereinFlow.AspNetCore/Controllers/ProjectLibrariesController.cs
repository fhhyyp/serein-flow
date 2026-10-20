using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using static SereinFlow.Api.ApiEndpointHelpers;

namespace SereinFlow.Api.Controllers;
[Route("api/projects/{projectId:guid}")]
public sealed class ProjectLibrariesController : ApiControllerBase
{
    private readonly ProjectLibraryService _projectLibraries;
    private readonly IProjectRepository _projects;
    private readonly ILibraryArtifactUsageStore _usage;
    private readonly LibraryUpgradeService _upgrades;
    public ProjectLibrariesController(ProjectLibraryService projectLibraries, IProjectRepository projects, ILibraryArtifactUsageStore usage, LibraryUpgradeService upgrades)
    {
        _projectLibraries = projectLibraries;
        _projects = projects;
        _usage = usage;
        _upgrades = upgrades;
    }

    [HttpGet("libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> List([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        var listAsyncResult = await _projectLibraries.ListAsync(projectId, cancellationToken);
        var toProjectLibraryResponseResult = ToProjectLibraryResponse(listAsyncResult);
        return toProjectLibraryResponseResult;
    }

    [HttpGet("libraries/usage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetUsage([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        if (await _projects.FindAsync(projectId, cancellationToken)is null)
        {
            var apiProblemResult = ApiProblem(StatusCodes.Status404NotFound, "Project was not found. 未找到项目。");
            return apiProblemResult;
        }

        var listByProjectAsyncResult = await _usage.ListByProjectAsync(projectId, cancellationToken);
        var okResult = Ok(listByProjectAsyncResult);
        return okResult;
    }

    [HttpPut("libraries/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Attach([FromRoute] Guid projectId, [FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var addAsyncResult = await _projectLibraries.AddAsync(projectId, libraryId, cancellationToken);
        var toProjectLibraryResponseResult1 = ToProjectLibraryResponse(addAsyncResult);
        return toProjectLibraryResponseResult1;
    }

    [HttpDelete("libraries/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Detach([FromRoute] Guid projectId, [FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var removeAsyncResult = await _projectLibraries.RemoveAsync(projectId, libraryId, cancellationToken);
        var toProjectLibraryResponseResult2 = ToProjectLibraryResponse(removeAsyncResult);
        return toProjectLibraryResponseResult2;
    }

    [HttpPost("library-upgrades/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> PreviewUpgrade([FromRoute] Guid projectId, [FromBody] LibraryUpgradePreviewRequestDto request, CancellationToken cancellationToken)
    {
        var previewAsyncResult = await _upgrades.PreviewAsync(projectId, request, cancellationToken);
        var toLibraryUpgradeResponseResult = ToLibraryUpgradeResponse(previewAsyncResult);
        return toLibraryUpgradeResponseResult;
    }

    [HttpGet("library-upgrades/{upgradeId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetUpgrade([FromRoute] Guid projectId, [FromRoute] Guid upgradeId, CancellationToken cancellationToken)
    {
        var getPlanAsyncResult = await _upgrades.GetPlanAsync(projectId, upgradeId, cancellationToken);
        var toLibraryUpgradeResponseResult3 = ToLibraryUpgradeResponse(getPlanAsyncResult);
        return toLibraryUpgradeResponseResult3;
    }

    [HttpPost("library-upgrades/{upgradeId:guid}/apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ApplyUpgrade([FromRoute] Guid projectId, [FromRoute] Guid upgradeId, [FromBody] ApplyLibraryUpgradeRequestDto request, CancellationToken cancellationToken)
    {
        var applyAsyncResult = await _upgrades.ApplyAsync(projectId, upgradeId, request, cancellationToken);
        var toLibraryUpgradeResponseResult4 = ToLibraryUpgradeResponse(applyAsyncResult);
        return toLibraryUpgradeResponseResult4;
    }

    [HttpPost("library-upgrades/{upgradeId:guid}/apply-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ApplyUpgradeBatch([FromRoute] Guid projectId, [FromRoute] Guid upgradeId, [FromBody] ApplyLibraryUpgradeBatchRequestDto request, CancellationToken cancellationToken)
    {
        var applyBatchAsyncResult = await _upgrades.ApplyBatchAsync(projectId, upgradeId, request, cancellationToken);
        var toLibraryUpgradeResponseResult5 = ToLibraryUpgradeResponse(applyBatchAsyncResult);
        return toLibraryUpgradeResponseResult5;
    }
}
