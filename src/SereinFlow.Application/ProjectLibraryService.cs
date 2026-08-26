using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed record ProjectLibraryOperationResult(
    bool IsSuccess,
    int StatusCode,
    string? Code = null,
    string? Message = null,
    IReadOnlyList<ProjectLibraryReferenceDto>? References = null);

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
    private readonly IProjectLibraryReferenceRepository _references;
    private readonly ILibraryCatalogService _catalog;

    public ProjectLibraryService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        IProjectLibraryReferenceRepository references,
        ILibraryCatalogService catalog)
    {
        _projects = projects;
        _flows = flows;
        _references = references;
        _catalog = catalog;
    }

    public async Task<ProjectLibraryOperationResult> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return ProjectNotFound();

        var references = await _references.ListByProjectAsync(projectId, cancellationToken);
        var resolved = new List<ProjectLibraryReferenceDto>(references.Count);
        foreach (var reference in references)
        {
            var library = await _catalog.FindAsync(reference.LibraryId, cancellationToken);
            if (library is not null)
                resolved.Add(new ProjectLibraryReferenceDto(reference.ProjectId, reference.LibraryId, reference.ReferencedAt, library));
        }

        return new ProjectLibraryOperationResult(true, 200, References: resolved);
    }

    public async Task<ProjectLibraryOperationResult> AddAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return ProjectNotFound();

        var library = await _catalog.FindAsync(libraryId, cancellationToken);
        if (library is null)
        {
            return new ProjectLibraryOperationResult(
                false,
                404,
                "library.not_found",
                "Library artifact was not found. 未找到类库制品。");
        }
        if (library.Lifecycle != LibraryLifecycleDto.Available)
        {
            return new ProjectLibraryOperationResult(
                false,
                409,
                "library.archived",
                "Archived library artifacts cannot be referenced by a new project. 已归档类库制品不能被新项目引用。");
        }

        await _references.AddAsync(projectId, library.Id, cancellationToken);
        return await ListAsync(projectId, cancellationToken);
    }

    public async Task<ProjectLibraryOperationResult> RemoveAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return ProjectNotFound();
        if (!await _references.IsReferencedAsync(projectId, libraryId, cancellationToken))
        {
            return new ProjectLibraryOperationResult(
                false,
                404,
                "project_library.not_referenced",
                "The project does not reference this library artifact. 项目未引用该类库制品。");
        }

        var flows = await _flows.ListByProjectAsync(projectId, cancellationToken);
        if (flows.Any(flow => GetLibraryIds(flow).Contains(libraryId, StringComparer.OrdinalIgnoreCase)))
        {
            return new ProjectLibraryOperationResult(
                false,
                409,
                "project_library.in_use",
                "The library is used by a current project flow and cannot be removed. 该类库仍被当前项目流程使用，不能取消引用。");
        }

        await _references.RemoveAsync(projectId, libraryId, cancellationToken);
        return await ListAsync(projectId, cancellationToken);
    }

    public async Task<FlowValidationResultDto> ValidateFlowLibrariesAsync(
        Guid projectId,
        FlowDefinitionDto definition,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnosticDto>();
        var referenceIds = (await _references.ListByProjectAsync(projectId, cancellationToken))
            .Select(static item => item.LibraryId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var canvas in definition.Canvases)
        {
            foreach (var node in canvas.Nodes)
            {
                var runtime = node.Ui;
                if (node.Type is not (NodeTypeDto.Action or NodeTypeDto.Flipflop)
                    || string.IsNullOrWhiteSpace(runtime?.LibraryId))
                {
                    continue;
                }

                var path = $"canvases.{canvas.Id}.nodes.{node.Id}.ui.libraryId";
                if (!referenceIds.Contains(runtime.LibraryId))
                {
                    diagnostics.Add(new ValidationDiagnosticDto(
                        "project_library.not_referenced",
                        "The project does not reference the library required by this node. 项目未引用该节点所需的类库。",
                        path));
                    continue;
                }

                var library = await _catalog.FindAsync(runtime.LibraryId, cancellationToken);
                if (library is null)
                {
                    diagnostics.Add(new ValidationDiagnosticDto(
                        "project_library.artifact_missing",
                        "The library artifact required by this node is unavailable. 该节点所需的类库制品不可用。",
                        path));
                    continue;
                }

                var catalogNode = library.Nodes.SingleOrDefault(candidate =>
                    candidate.Type == node.Type
                    && string.Equals(candidate.ClassName, runtime.ClassName, StringComparison.Ordinal)
                    && string.Equals(candidate.MethodName, runtime.MethodName, StringComparison.Ordinal)
                    && string.Equals(candidate.DllName, runtime.DllName, StringComparison.Ordinal)
                    && string.Equals(candidate.DllVersion, runtime.DllVersion, StringComparison.Ordinal));
                if (catalogNode is null)
                {
                    diagnostics.Add(new ValidationDiagnosticDto(
                        "project_library.node_metadata_invalid",
                        "The node metadata does not match the referenced library artifact. 节点元数据与已引用的类库制品不匹配。",
                        path));
                    continue;
                }

                if (node.Type == NodeTypeDto.Flipflop && !catalogNode.IsAwaitable)
                {
                    diagnostics.Add(new ValidationDiagnosticDto(
                        "library.flipflop_return_type_invalid",
                        "Flipflop methods must return Task or Task<T>. Flipflop 方法必须返回 Task 或 Task<T>。",
                        path));
                }
            }
        }

        return new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
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
        var diagnostics = definition.Canvases
            .SelectMany(static canvas => canvas.Nodes.Select(node => new { canvas.Id, Node = node }))
            .Where(static item => item.Node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop)
            .Where(static item => !string.IsNullOrWhiteSpace(item.Node.Ui?.LibraryId))
            .Select(static item => new ValidationDiagnosticDto(
                "project_library.not_referenced",
                "A new project cannot contain external library nodes before the library is explicitly referenced. 新项目尚未显式引用类库，不能包含外部类库节点。",
                $"canvases.{item.Id}.nodes.{item.Node.Id}.ui.libraryId"))
            .ToArray();

        return new FlowValidationResultDto(diagnostics.Length == 0, diagnostics);
    }

    public static IReadOnlyList<string> GetLibraryIds(FlowDefinitionDto definition)
        => definition.Canvases
            .SelectMany(static canvas => canvas.Nodes)
            .Where(static node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop)
            .Select(static node => node.Ui?.LibraryId)
            .Where(static libraryId => !string.IsNullOrWhiteSpace(libraryId))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static ProjectLibraryOperationResult ProjectNotFound()
        => new(false, 404, "project.not_found", "Project was not found. 未找到项目。");
}
