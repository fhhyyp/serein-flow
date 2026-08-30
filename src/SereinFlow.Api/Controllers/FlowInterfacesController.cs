using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Api.Controllers;

[Route("api/environment/interfaces")]
public sealed class FlowInterfacesController : ApiControllerBase
{
    private readonly IFlowInterfaceRepository _interfaces;
    private readonly IFlowVersionRepository _versions;
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;

    public FlowInterfacesController(
        IFlowInterfaceRepository interfaces,
        IFlowVersionRepository versions,
        IProjectRepository projects,
        IFlowDefinitionRepository flows)
    {
        _interfaces = interfaces;
        _versions = versions;
        _projects = projects;
        _flows = flows;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FlowInterfaceDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var items = new List<FlowInterfaceDto>();
        foreach (var flowInterface in await _interfaces.ListAsync(cancellationToken))
        {
            items.Add(flowInterface with
            {
                ProductionVersion = await _versions.FindProductionVersionAsync(
                    flowInterface.ProjectId,
                    flowInterface.FlowId,
                    cancellationToken),
            });
        }

        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FlowInterfaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(
        [FromBody] CreateFlowInterfaceRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80)
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The interface name must contain 1 to 80 characters. 接口名称长度必须为 1 到 80 个字符。");
        }
        if (!Enum.IsDefined(request.InvocationMode))
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The interface invocation mode is invalid. 接口调用模式无效。");
        }

        var project = await _projects.FindAsync(request.ProjectId, cancellationToken);
        if (project is null || await _flows.FindAsync(request.ProjectId, request.FlowId, cancellationToken) is null)
        {
            return ApiProblem(
                StatusCodes.Status404NotFound,
                "The selected project or flow was not found. 所选项目或流程不存在。");
        }
        if (project.Status == ProjectStatus.Archived)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "Archived projects cannot be published through environment interfaces. 已归档项目不能发布为环境接口。");
        }

        var productionVersion = await _versions.FindProductionVersionAsync(
            request.ProjectId,
            request.FlowId,
            cancellationToken);
        if (productionVersion is null)
        {
            return ApiProblem(
                StatusCodes.Status409Conflict,
                "A production flow version is required before creating an environment interface. 创建环境接口前必须先发布生产流程版本。",
                extensions: new Dictionary<string, object?> { ["code"] = "flow.production_version_required" });
        }

        var now = DateTimeOffset.UtcNow;
        var flowInterface = new FlowInterfaceDto(
            Guid.NewGuid(),
            request.ProjectId,
            request.FlowId,
            request.Name.Trim(),
            request.InvocationMode,
            request.IsEnabled,
            now,
            now,
            productionVersion);
        await _interfaces.AddAsync(flowInterface, cancellationToken);
        return Created($"/api/environment/interfaces/{flowInterface.Id:D}", flowInterface);
    }

    [HttpPut("{interfaceId:guid}")]
    [ProducesResponseType(typeof(FlowInterfaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(
        [FromRoute] Guid interfaceId,
        [FromBody] UpdateFlowInterfaceRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80 || !Enum.IsDefined(request.InvocationMode))
            return ApiProblem(StatusCodes.Status400BadRequest, "The interface configuration is invalid. 接口配置无效。");

        var existing = await _interfaces.FindAsync(interfaceId, cancellationToken);
        if (existing is null)
            return ApiProblem(StatusCodes.Status404NotFound, "Flow interface not found. 流程接口不存在。");

        var updated = existing with
        {
            Name = request.Name.Trim(),
            InvocationMode = request.InvocationMode,
            IsEnabled = request.IsEnabled,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _interfaces.UpdateAsync(updated, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{interfaceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete([FromRoute] Guid interfaceId, CancellationToken cancellationToken)
        => await _interfaces.DeleteAsync(interfaceId, cancellationToken)
            ? NoContent()
            : ApiProblem(StatusCodes.Status404NotFound, "Flow interface not found. 流程接口不存在。");
}
