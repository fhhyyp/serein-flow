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
    {
        var databaseClient = database?.Client ?? throw new ArgumentNullException(nameof(database), "The database cannot be null. 数据库不能为空。");
        _references = new SqlSugarRepository<ProjectLibraryReferenceRecord>(databaseClient);
    }

    public async Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var key = projectId.ToString("D");
        var evaluatedValue = (await _references.ListAsync(item => item.ProjectId == key, cancellationToken));
        var selectResult = evaluatedValue.Select(Map);
        var orderByResult = selectResult.OrderBy(static item => item.ReferencedAt);
        var thenByResult = orderByResult.ThenBy(static item => item.LibraryId, StringComparer.Ordinal);
        var toArrayResult = thenByResult.ToArray();
        return toArrayResult;
    }

    public async Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
    {
        var evaluatedValue1 = await resolveGetByIdAsyncResult()is not null;
        return evaluatedValue1;
        global::System.Threading.Tasks.Task<global::SereinFlow.Infrastructure.Persistence.ProjectLibraryReferenceRecord?> resolveGetByIdAsyncResult()
        {
            var createIdResult = CreateId(projectId, libraryId);
            var getByIdAsyncResult = _references.GetByIdAsync(createIdResult, cancellationToken);
            return getByIdAsyncResult;
        }
    }

    public async Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeLibraryId(libraryId);
        var id = CreateId(projectId, normalizedId);
        var existing = await _references.GetByIdAsync(id, cancellationToken);
        if (existing is not null)
        {
            var mapResult = Map(existing);
            return mapResult;
        }

        var reference = new ProjectLibraryReference(projectId, normalizedId, DateTimeOffset.UtcNow);
        var projectLibraryReferenceRecord = new ProjectLibraryReferenceRecord
        {
            Id = id,
            ProjectId = projectId.ToString("D"),
            LibraryId = normalizedId,
            ReferencedAt = reference.ReferencedAt.ToString("O", CultureInfo.InvariantCulture),
        };
        await _references.AddAsync(projectLibraryReferenceRecord, cancellationToken);
        return reference;
    }

    public async Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
    {
        var createIdResult2 = CreateId(projectId, libraryId);
        var deleteAsyncResult = await _references.DeleteAsync(createIdResult2, cancellationToken);
        return deleteAsyncResult;
    }

    private static ProjectLibraryReference Map(ProjectLibraryReferenceRecord record)
    {
        var parseResult = Guid.Parse(record.ProjectId);
        var parseResult3 = DateTimeOffset.Parse(record.ReferencedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        global::SereinFlow.Application.Persistence.ProjectLibraryReference evaluatedValue4 = new(parseResult, record.LibraryId, parseResult3);
        return evaluatedValue4;
    }

    private static string CreateId(Guid projectId, string libraryId)
    {
        var evaluatedValue5 = $"{projectId:N}:{NormalizeLibraryId(libraryId)}";
        return evaluatedValue5;
    }

    private static string NormalizeLibraryId(string libraryId)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            throw new ArgumentException("Library artifact ID cannot be empty. 类库制品 ID 不能为空。", nameof(libraryId));
        }

        var trimResult = libraryId.Trim();
        var toLowerInvariantResult = trimResult.ToLowerInvariant();
        return toLowerInvariantResult;
    }
}
