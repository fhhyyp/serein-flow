using SereinFlow.Application.Persistence;
using SereinFlow.Domain;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Captures the outcome of project archive operations.
/// 记录项目归档操作的结果。
/// </summary>
public sealed record ProjectArchiveResult(bool IsSuccess, int StatusCode, string? Code = null, string? Message = null, Project? Project = null);
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
    public ProjectArchiveService(IProjectRepository projects, IFlowInterfaceRepository interfaces)
    {
        _projects = projects;
        _interfaces = interfaces;
    }

    public async Task<ProjectArchiveResult> ArchiveAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            var projectArchiveResult = new ProjectArchiveResult(false, 404, ProjectErrorCodes.NotFound, "Project was not found. 未找到项目。");
            return projectArchiveResult;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            var projectArchiveResult1 = new ProjectArchiveResult(true, 200, Project: project);
            return projectArchiveResult1;
        }

        var hasEnvironmentInterface = (await _interfaces.ListAsync(cancellationToken))
            .Any(flowInterface => flowInterface.ProjectId == projectId);
        if (hasEnvironmentInterface)
        {
            var projectArchiveResult2 = new ProjectArchiveResult(false, 409, ProjectErrorCodes.ArchiveEnvironmentInterfaceExists, "The project is referenced by one or more environment interfaces and cannot be archived. 项目已被一个或多个环境接口引用，不能归档。");
            return projectArchiveResult2;
        }

        var expectedVersion = project.Version;
        project.Archive();
        if (!await _projects.TryUpdateAsync(project, expectedVersion, cancellationToken))
        {
            var projectArchiveResult3 = new ProjectArchiveResult(false, 409, ProjectErrorCodes.VersionConflict, "The project was changed by another editor. 项目已被其他编辑器修改。");
            return projectArchiveResult3;
        }

        var projectArchiveResult4 = new ProjectArchiveResult(true, 200, Project: project);
        return projectArchiveResult4;
    }
}
