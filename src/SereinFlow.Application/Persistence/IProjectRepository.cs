using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default);

    Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Project project, CancellationToken cancellationToken = default);

    Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default);

    // Synchronous members are retained for non-HTTP tooling and existing
    // migration tests. New API/Application code must use the async members.
    // 保留同步成员供迁移工具和既有测试使用；新的 API/Application 代码必须使用异步成员。
    IReadOnlyList<Project> List();

    Project? Find(Guid id);

    void Add(Project project);

    bool TryUpdate(Project project, long expectedVersion);
}
