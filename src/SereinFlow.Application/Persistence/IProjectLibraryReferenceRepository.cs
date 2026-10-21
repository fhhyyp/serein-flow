namespace SereinFlow.Application.Persistence;
/// <summary>
/// Represents project library reference data used by SereinFlow.
/// 表示 SereinFlow 使用的项目库Reference数据。
/// </summary>
public sealed record ProjectLibraryReference(Guid ProjectId, string LibraryId, DateTimeOffset ReferencedAt);
public interface IProjectLibraryReferenceRepository
{
    Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default);
    Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default);
}
