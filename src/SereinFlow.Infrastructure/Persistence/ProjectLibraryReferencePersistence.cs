using System.Globalization;
using SereinFlow.Application.Persistence;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
{
    private readonly IRepository<ProjectLibraryReferenceRecord> _references;

    public SqlSugarProjectLibraryReferenceRepository(IRepository<ProjectLibraryReferenceRecord> references)
    {
        _references = references;
    }

    // Maintains the same direct-construction path as the other persistence
    // adapters for infrastructure tests and offline maintenance tools.
    // 与其他持久化适配器保持一致，供基础设施测试和离线维护工具直接构造。
    public SqlSugarProjectLibraryReferenceRepository(SqliteDatabase database)
        : this(new SqlSugarRepository<ProjectLibraryReferenceRecord>(
            database?.Client ?? throw new ArgumentNullException(nameof(database), "The database cannot be null. 数据库不能为空。")))
    {
    }

    public async Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var key = projectId.ToString("D");
        return (await _references.ListAsync(item => item.ProjectId == key, cancellationToken))
            .Select(Map)
            .OrderBy(static item => item.ReferencedAt)
            .ThenBy(static item => item.LibraryId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<bool> IsReferencedAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default)
        => await _references.GetByIdAsync(CreateId(projectId, libraryId), cancellationToken) is not null;

    public async Task<ProjectLibraryReference> AddAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeLibraryId(libraryId);
        var id = CreateId(projectId, normalizedId);
        var existing = await _references.GetByIdAsync(id, cancellationToken);
        if (existing is not null)
            return Map(existing);

        var reference = new ProjectLibraryReference(projectId, normalizedId, DateTimeOffset.UtcNow);
        await _references.AddAsync(new ProjectLibraryReferenceRecord
        {
            Id = id,
            ProjectId = projectId.ToString("D"),
            LibraryId = normalizedId,
            ReferencedAt = reference.ReferencedAt.ToString("O", CultureInfo.InvariantCulture),
        }, cancellationToken);
        return reference;
    }

    public async Task<bool> RemoveAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default)
        => await _references.DeleteAsync(CreateId(projectId, libraryId), cancellationToken);

    private static ProjectLibraryReference Map(ProjectLibraryReferenceRecord record)
        => new(
            Guid.Parse(record.ProjectId),
            record.LibraryId,
            DateTimeOffset.Parse(record.ReferencedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static string CreateId(Guid projectId, string libraryId)
        => $"{projectId:N}:{NormalizeLibraryId(libraryId)}";

    private static string NormalizeLibraryId(string libraryId)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
            throw new ArgumentException("Library artifact ID cannot be empty. 类库制品 ID 不能为空。", nameof(libraryId));
        return libraryId.Trim().ToLowerInvariant();
    }
}
