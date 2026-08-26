namespace SereinFlow.Application.Persistence;

public sealed record ProjectLibraryReference(
    Guid ProjectId,
    string LibraryId,
    DateTimeOffset ReferencedAt);

public interface IProjectLibraryReferenceRepository
{
    Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> IsReferencedAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default);

    Task<ProjectLibraryReference> AddAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default);
}
