using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
public sealed record ProjectLibraryOperationResult(bool IsSuccess, int StatusCode, string? Code = null, string? Message = null, IReadOnlyList<ProjectLibraryReferenceDto>? References = null);
/// <summary>
/// Owns project-to-library ownership rules. Environment packages remain
/// immutable global artifacts, while this service controls which artifacts a
/// project may place in a flow or execute.
/// 项目类库归属规则：环境包仍是不可变的全局制品，本服务控制项目能够在流程中使用和执行哪些制品。
/// </summary>
public sealed class ProjectLibraryService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowVersionRepository? _versions;
    private readonly IProjectLibraryReferenceRepository _references;
    private readonly ILibraryCatalogService _catalog;
    private readonly IWorkspaceChangePublisher? _changePublisher;
    public ProjectLibraryService(IProjectRepository projects, IFlowDefinitionRepository flows, IProjectLibraryReferenceRepository references, ILibraryCatalogService catalog, IFlowVersionRepository? versions = null, IWorkspaceChangePublisher? changePublisher = null)
    {
        _projects = projects;
        _flows = flows;
        _references = references;
        _catalog = catalog;
        _versions = versions;
        _changePublisher = changePublisher;
    }

    public async Task<ProjectLibraryOperationResult> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken)is null)
        {
            var projectNotFoundResult = ProjectNotFound();
            return projectNotFoundResult;
        }

        var references = await _references.ListByProjectAsync(projectId, cancellationToken);
        var resolved = new List<ProjectLibraryReferenceDto>(references.Count);
        foreach (var reference in references)
        {
            var library = await _catalog.FindAsync(reference.LibraryId, cancellationToken);
            if (library is not null)
            {
                var projectLibraryReferenceDto = new ProjectLibraryReferenceDto(reference.ProjectId, reference.LibraryId, reference.ReferencedAt, library);
                resolved.Add(projectLibraryReferenceDto);
            }
        }

        var projectLibraryOperationResult = new ProjectLibraryOperationResult(true, 200, References: resolved);
        return projectLibraryOperationResult;
    }

    public async Task<ProjectLibraryOperationResult> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default, string origin = "web")
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            var projectNotFoundResult1 = ProjectNotFound();
            return projectNotFoundResult1;
        }

        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            var projectArchivedResult = ProjectArchived();
            return projectArchivedResult;
        }

        var library = await _catalog.FindAsync(libraryId, cancellationToken);
        if (library is null)
        {
            var projectLibraryOperationResult2 = new ProjectLibraryOperationResult(false, 404, LibraryErrorCodes.NotFound, "Library artifact was not found. 未找到类库制品。");
            return projectLibraryOperationResult2;
        }

        if (library.Lifecycle != LibraryLifecycleDto.Available)
        {
            var projectLibraryOperationResult3 = new ProjectLibraryOperationResult(false, 409, LibraryErrorCodes.Archived, "Archived library artifacts cannot be referenced by a new project. 已归档类库制品不能被新项目引用。");
            return projectLibraryOperationResult3;
        }

        await _references.AddAsync(projectId, library.Id, cancellationToken);
        var result = await ListAsync(projectId, cancellationToken);
        await PublishLibraryChangeAsync(projectId, library.Id, ProjectErrorCodes.LibraryChanged, ProjectErrorCodes.LibraryAttach, origin);
        return result;
    }

    public async Task<ProjectLibraryOperationResult> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default, string origin = "web")
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            var projectNotFoundResult4 = ProjectNotFound();
            return projectNotFoundResult4;
        }

        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            var projectArchivedResult5 = ProjectArchived();
            return projectArchivedResult5;
        }

        if (!await _references.IsReferencedAsync(projectId, libraryId, cancellationToken))
        {
            var projectLibraryOperationResult6 = new ProjectLibraryOperationResult(false, 404, ProjectLibraryErrorCodes.NotReferenced, "The project does not reference this library artifact. 项目未引用该类库制品。");
            return projectLibraryOperationResult6;
        }

        var flows = await _flows.ListByProjectAsync(projectId, cancellationToken);
        if (flows.Any(flow =>
        {
            var getLibraryIdsResult = GetLibraryIds(flow);
            var containsResult = getLibraryIdsResult.Contains(libraryId, StringComparer.OrdinalIgnoreCase);
            return containsResult;
        }))
        {
            var projectLibraryOperationResult7 = new ProjectLibraryOperationResult(false, 409, ProjectLibraryErrorCodes.InUse, "The library is used by a current project flow and cannot be removed. 该类库仍被当前项目流程使用，不能取消引用。");
            return projectLibraryOperationResult7;
        }

        if (_versions is not null && await _versions.IsLibraryReferencedByProductionHistoryAsync(projectId, libraryId, cancellationToken))
        {
            var projectLibraryOperationResult8 = new ProjectLibraryOperationResult(false, 409, ProjectLibraryErrorCodes.InUseByProductionHistory, "The library is retained by production flow history and cannot be removed. 该类库仍被生产流程历史使用，不能取消引用。");
            return projectLibraryOperationResult8;
        }

        await _references.RemoveAsync(projectId, libraryId, cancellationToken);
        var result = await ListAsync(projectId, cancellationToken);
        await PublishLibraryChangeAsync(projectId, libraryId, ProjectErrorCodes.LibraryChanged, ProjectErrorCodes.LibraryDetach, origin);
        return result;
    }

    public async Task<FlowValidationResultDto> ValidateFlowLibrariesAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnosticDto>();
        var evaluatedValue13 = (await _references.ListByProjectAsync(projectId, cancellationToken));
        var selectResult = evaluatedValue13.Select(static item => item.LibraryId);
        var referenceIds = selectResult.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var canvas in definition.Canvases)
        {
            foreach (var node in canvas.Nodes)
            {
                var runtime = node.Ui;
                if (node.Type is not (NodeTypeDto.Action or NodeTypeDto.Flipflop) || string.IsNullOrWhiteSpace(runtime?.LibraryId))
                {
                    continue;
                }

                var path = $"canvases.{canvas.Id}.nodes.{node.Id}.ui.libraryId";
                if (!referenceIds.Contains(runtime.LibraryId))
                {
                    var validationDiagnosticDto = new ValidationDiagnosticDto(ProjectLibraryErrorCodes.NotReferenced, "The project does not reference the library required by this node. 项目未引用该节点所需的类库。", path);
                    diagnostics.Add(validationDiagnosticDto);
                    continue;
                }

                var library = await _catalog.FindAsync(runtime.LibraryId, cancellationToken);
                if (library is null)
                {
                    var validationDiagnosticDto9 = new ValidationDiagnosticDto(ProjectLibraryErrorCodes.ArtifactMissing, "The library artifact required by this node is unavailable. 该节点所需的类库制品不可用。", path);
                    diagnostics.Add(validationDiagnosticDto9);
                    continue;
                }

                var catalogNode = library.Nodes.SingleOrDefault(candidate =>
                {
                    var evaluatedValue = candidate.Type == node.Type && string.Equals(candidate.ClassName, runtime.ClassName, StringComparison.Ordinal) && string.Equals(candidate.MethodName, runtime.MethodName, StringComparison.Ordinal) && string.Equals(candidate.DllName, runtime.DllName, StringComparison.Ordinal) && string.Equals(candidate.DllVersion, runtime.DllVersion, StringComparison.Ordinal);
                    return evaluatedValue;
                });
                if (catalogNode is null)
                {
                    var validationDiagnosticDto10 = new ValidationDiagnosticDto(ProjectLibraryErrorCodes.NodeMetadataInvalid, "The node metadata does not match the referenced library artifact. 节点元数据与已引用的类库制品不匹配。", path);
                    diagnostics.Add(validationDiagnosticDto10);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(runtime.LibraryNodeContractId) && !string.Equals(runtime.LibraryNodeContractId, catalogNode.ContractId ?? catalogNode.Id, StringComparison.Ordinal))
                {
                    var validationDiagnosticDto11 = new ValidationDiagnosticDto(ProjectLibraryErrorCodes.NodeContractInvalid, "The node contract ID does not match the referenced library artifact. 节点契约 ID 与已引用的类库制品不匹配。", path);
                    diagnostics.Add(validationDiagnosticDto11);
                    continue;
                }

                if (node.Type == NodeTypeDto.Flipflop && !catalogNode.IsAwaitable)
                {
                    var validationDiagnosticDto12 = new ValidationDiagnosticDto(LibraryErrorCodes.FlipflopReturnTypeInvalid, "Flipflop methods must return Task or Task<T>. Flipflop 方法必须返回 Task 或 Task<T>。", path);
                    diagnostics.Add(validationDiagnosticDto12);
                }
            }
        }

        var flowValidationResultDto = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        return flowValidationResultDto;
    }

    /// <summary>
    /// A project has no library-reference scope before it is created. Rejecting
    /// external library nodes here keeps the creation path consistent with the
    /// explicit-reference rule enforced when flows are later saved or run.
    /// 项目创建前不存在类库引用范围。此处拒绝外部类库节点，使创建入口与后续保存、运行时
    /// 强制执行的显式引用规则保持一致。
    /// </summary>
    public static FlowValidationResultDto ValidateNewProjectFlowLibraries(FlowDefinitionDto definition)
    {
        var selectManyResult = definition.Canvases.SelectMany(static canvas =>
        {
            var selectResult14 = canvas.Nodes.Select(node => new { canvas.Id, Node = node });
            return selectResult14;
        });
        var whereResult = selectManyResult.Where(static item => item.Node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop);
        var whereResult17 = whereResult.Where(static item =>
        {
            var evaluatedValue15 = !string.IsNullOrWhiteSpace(item.Node.Ui?.LibraryId);
            return evaluatedValue15;
        });
        var selectResult18 = whereResult17.Select(static item =>
        {
            var validationDiagnosticDto16 = new ValidationDiagnosticDto(ProjectLibraryErrorCodes.NotReferenced, "A new project cannot contain external library nodes before the library is explicitly referenced. 新项目尚未显式引用类库，不能包含外部类库节点。", $"canvases.{item.Id}.nodes.{item.Node.Id}.ui.libraryId");
            return validationDiagnosticDto16;
        });
        var diagnostics = selectResult18.ToArray();
        var flowValidationResultDto19 = new FlowValidationResultDto(diagnostics.Length == 0, diagnostics);
        return flowValidationResultDto19;
    }

    public static IReadOnlyList<string> GetLibraryIds(FlowDefinitionDto definition)
    {
        var selectManyResult21 = definition.Canvases.SelectMany(static canvas => canvas.Nodes);
        var whereResult22 = selectManyResult21.Where(static node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop);
        var selectResult23 = whereResult22.Select(static node => node.Ui?.LibraryId);
        var whereResult24 = selectResult23.Where(static libraryId =>
        {
            var evaluatedValue20 = !string.IsNullOrWhiteSpace(libraryId);
            return evaluatedValue20;
        });
        var castResult = whereResult24.Cast<string>();
        var distinctResult = castResult.Distinct(StringComparer.OrdinalIgnoreCase);
        var orderResult = distinctResult.Order(StringComparer.Ordinal);
        var toArrayResult = orderResult.ToArray();
        return toArrayResult;
    }

    private static ProjectLibraryOperationResult ProjectNotFound()
    {
        global::SereinFlow.Application.ProjectLibraryOperationResult evaluatedValue25 = new(false, 404, ProjectErrorCodes.NotFound, "Project was not found. 未找到项目。");
        return evaluatedValue25;
    }

    private static ProjectLibraryOperationResult ProjectArchived()
    {
        global::SereinFlow.Application.ProjectLibraryOperationResult evaluatedValue26 = new(false, 409, ProjectErrorCodes.Archived, "Archived projects cannot change library references. 已归档项目不能修改类库引用。");
        return evaluatedValue26;
    }

    private async Task PublishLibraryChangeAsync(Guid projectId, string libraryId, string changeType, string operation, string origin)
    {
        if (_changePublisher is null)
        {
            return;
        }

        var newGuidResult = Guid.NewGuid();
        var workspaceChangeEventDto = new WorkspaceChangeEventDto(newGuidResult, DateTimeOffset.UtcNow, changeType, projectId, null, null, null, origin, operation, LibraryIds: [libraryId]);
        await _changePublisher.PublishAsync(workspaceChangeEventDto, CancellationToken.None);
    }
}
