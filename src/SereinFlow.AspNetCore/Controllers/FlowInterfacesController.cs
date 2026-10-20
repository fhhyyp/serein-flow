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
    public FlowInterfacesController(IFlowInterfaceRepository interfaces, IFlowVersionRepository versions, IProjectRepository projects, IFlowDefinitionRepository flows)
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
            var evaluatedValue = flowInterface with
            {
                ProductionVersion = await _versions.FindProductionVersionAsync(flowInterface.ProjectId, flowInterface.FlowId, cancellationToken),
            };
            items.Add(evaluatedValue);
        }

        var okResult = Ok(items);
        return okResult;
    }

    [HttpPost]
    [ProducesResponseType(typeof(FlowInterfaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create([FromBody] CreateFlowInterfaceRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80)
        {
            var apiProblemResult = ApiProblem(StatusCodes.Status400BadRequest, "The interface name must contain 1 to 80 characters. 接口名称长度必须为 1 到 80 个字符。");
            return apiProblemResult;
        }

        if (!Enum.IsDefined(request.InvocationMode))
        {
            var apiProblemResult1 = ApiProblem(StatusCodes.Status400BadRequest, "The interface invocation mode is invalid. 接口调用模式无效。");
            return apiProblemResult1;
        }

        var project = await _projects.FindAsync(request.ProjectId, cancellationToken);
        if (project is null || await _flows.FindAsync(request.ProjectId, request.FlowId, cancellationToken)is null)
        {
            var apiProblemResult2 = ApiProblem(StatusCodes.Status404NotFound, "The selected project or flow was not found. 所选项目或流程不存在。");
            return apiProblemResult2;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            var apiProblemResult3 = ApiProblem(StatusCodes.Status409Conflict, "Archived projects cannot be published through environment interfaces. 已归档项目不能发布为环境接口。");
            return apiProblemResult3;
        }

        var productionVersion = await _versions.FindProductionVersionAsync(request.ProjectId, request.FlowId, cancellationToken);
        if (productionVersion is null)
        {
            var dictionary = new Dictionary<string, object?>
            {
                ["code"] = FlowErrorCodes.ProductionVersionRequired
            };
            var apiProblemResult4 = ApiProblem(StatusCodes.Status409Conflict, "A production flow version is required before creating an environment interface. 创建环境接口前必须先发布生产流程版本。", extensions: dictionary);
            return apiProblemResult4;
        }

        var now = DateTimeOffset.UtcNow;
        var newGuidResult = Guid.NewGuid();
        var trimResult = request.Name.Trim();
        var flowInterface = new FlowInterfaceDto(newGuidResult, request.ProjectId, request.FlowId, trimResult, request.InvocationMode, request.IsEnabled, now, now, productionVersion);
        await _interfaces.AddAsync(flowInterface, cancellationToken);
        var createdResult = Created($"/api/environment/interfaces/{flowInterface.Id:D}", flowInterface);
        return createdResult;
    }

    [HttpPut("{interfaceId:guid}")]
    [ProducesResponseType(typeof(FlowInterfaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update([FromRoute] Guid interfaceId, [FromBody] UpdateFlowInterfaceRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80 || !Enum.IsDefined(request.InvocationMode))
        {
            var apiProblemResult5 = ApiProblem(StatusCodes.Status400BadRequest, "The interface configuration is invalid. 接口配置无效。");
            return apiProblemResult5;
        }

        var existing = await _interfaces.FindAsync(interfaceId, cancellationToken);
        if (existing is null)
        {
            var apiProblemResult6 = ApiProblem(StatusCodes.Status404NotFound, "Flow interface not found. 流程接口不存在。");
            return apiProblemResult6;
        }

        var updated = existing with
        {
            Name = request.Name.Trim(),
            InvocationMode = request.InvocationMode,
            IsEnabled = request.IsEnabled,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _interfaces.UpdateAsync(updated, cancellationToken);
        var okResult7 = Ok(updated);
        return okResult7;
    }

    [HttpDelete("{interfaceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete([FromRoute] Guid interfaceId, CancellationToken cancellationToken)
    {
        ActionResult evaluatedValue8 = await _interfaces.DeleteAsync(interfaceId, cancellationToken) ? NoContent() : ApiProblem(StatusCodes.Status404NotFound, "Flow interface not found. 流程接口不存在。");
        return evaluatedValue8;
    }
}
