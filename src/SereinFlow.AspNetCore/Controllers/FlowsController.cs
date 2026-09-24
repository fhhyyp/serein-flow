using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Api.Controllers;

[Route("api/projects/{projectId:guid}/flows")]
public sealed class FlowsController : ApiControllerBase
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly FlowDefinitionWriteService _flowWriter;

    public FlowsController(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        FlowDefinitionWriteService flowWriter)
    {
        _projects = projects;
        _flows = flows;
        _flowWriter = flowWriter;
    }

    [HttpGet("{flowId:guid}")]
    [ProducesResponseType(typeof(FlowDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        CancellationToken cancellationToken)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Project not found. 未找到项目。");

        var flow = await _flows.FindAsync(projectId, flowId, cancellationToken);
        return flow is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。")
            : Ok(flow);
    }

    [HttpPut("{flowId:guid}")]
    [ProducesResponseType(typeof(FlowDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(
        [FromRoute] Guid projectId,
        [FromRoute] Guid flowId,
        [FromBody] UpdateFlowDefinitionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 1 || request.Definition.Id != flowId)
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The flow route and version must match the update request. 流程路由和版本必须与更新请求一致。");
        }

        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null || await _flows.FindAsync(projectId, flowId, cancellationToken) is null)
        {
            return ApiProblem(StatusCodes.Status404NotFound, "Flow definition not found. 未找到流程定义。");
        }

        if (project.Status == ProjectStatus.Archived)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "Archived projects cannot save flow definitions. 已归档项目不能保存流程定义。");
        }

        var result = await _flowWriter.WriteAsync(
            projectId,
            flowId,
            request.Definition,
            request.ExpectedVersion,
            cancellationToken);
        return result.Status switch
        {
            FlowDefinitionWriteStatus.Saved or FlowDefinitionWriteStatus.NoChange => Ok(result.Saved),
            FlowDefinitionWriteStatus.Invalid when result.Preparation is not null => BadRequest(result.Preparation.Validation),
            FlowDefinitionWriteStatus.Archived => ApiProblem(
                StatusCodes.Status409Conflict,
                "Archived projects cannot save flow definitions. 已归档项目不能保存流程定义。"),
            FlowDefinitionWriteStatus.Conflict => ApiProblem(
                StatusCodes.Status409Conflict,
                "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。",
                extensions: new Dictionary<string, object?> { ["currentVersion"] = result.CurrentVersion }),
            _ => ApiProblem(StatusCodes.Status400BadRequest, "The flow definition is invalid. 流程定义无效。"),
        };
    }
}
