using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public sealed record ProjectArchiveResult(
    bool IsSuccess,
    int StatusCode,
    string? Code = null,
    string? Message = null,
    Project? Project = null);

/// <summary>
/// Owns the project archival invariant. A published environment interface is
/// an external contract, so its target project cannot disappear from normal
/// operations until that interface is removed or repointed.
/// 项目归档规则：环境接口属于对外契约；在接口被删除或改指向前，目标项目不能从常规运行环境中归档。
/// </summary>
public sealed class ProjectArchiveService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowInterfaceRepository _interfaces;

    public ProjectArchiveService(
        IProjectRepository projects,
        IFlowInterfaceRepository interfaces)
    {
        _projects = projects;
        _interfaces = interfaces;
    }

    public async Task<ProjectArchiveResult> ArchiveAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return new ProjectArchiveResult(
                false,
                404,
                "project.not_found",
                "Project was not found. 未找到项目。");
        }

        if (project.Status == ProjectStatus.Archived)
            return new ProjectArchiveResult(true, 200, Project: project);

        var hasEnvironmentInterface = (await _interfaces.ListAsync(cancellationToken))
            .Any(flowInterface => flowInterface.ProjectId == projectId);
        if (hasEnvironmentInterface)
        {
            return new ProjectArchiveResult(
                false,
                409,
                "project.archive_environment_interface_exists",
                "The project is referenced by one or more environment interfaces and cannot be archived. 项目已被一个或多个环境接口引用，不能归档。");
        }

        var expectedVersion = project.Version;
        project.Archive();
        if (!await _projects.TryUpdateAsync(project, expectedVersion, cancellationToken))
        {
            return new ProjectArchiveResult(
                false,
                409,
                "project.version_conflict",
                "The project was changed by another editor. 项目已被其他编辑器修改。");
        }

        return new ProjectArchiveResult(true, 200, Project: project);
    }
}
