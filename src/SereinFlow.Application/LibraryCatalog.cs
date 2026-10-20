using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Trusted API boundary for uploaded class libraries.
/// Implementations may persist packages and metadata, but must never load an
/// uploaded assembly in the API process. Assembly inspection/execution belongs
/// to a separately supervised Worker process.
/// 上传类库的可信 API 边界。实现可以持久化包和元数据，但绝不能在 API 进程加载上传程序集；程序集检查和执行属于独立监管的 Worker 进程。
/// </summary>
public interface ILibraryCatalogService
{
    IReadOnlyList<LibraryDto> List();
    LibraryDto? Find(string libraryId);
    Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default);
    Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default);
    Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength, CancellationToken cancellationToken, string origin)
    {
        var uploadAsyncResult = UploadAsync(package, fileName, declaredLength, cancellationToken);
        return uploadAsyncResult;
    }

    /// <summary>
    /// Imports a previewed package only when its copied bytes match the preview.
    /// The size and hash must be checked before deduplication or persistence.
    /// 复制包时校验预览的大小和哈希，校验通过后才可去重或持久化。
    /// </summary>
    Task<LibraryUploadResultDto> UploadVerifiedAsync(Stream package, string fileName, long expectedSizeBytes, string expectedSha256, string origin = "mcp", CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This library catalog does not support importing previewed packages.");
    }

    /// <summary>
    /// Inspects a package without persisting it. MCP may retain the original
    /// bytes only in a bounded, controlled staging area until preview expiry.
    /// 仅扫描而不持久化类库包；MCP 仅可在预览过期前将原始字节保存在受限暂存区。
    /// </summary>
    Task<LibraryPackageInspectionDto?> InspectAsync(Stream package, string fileName, long? declaredLength = null, string? familyId = null, string? baselineArtifactId = null, CancellationToken cancellationToken = default)
    {
        var fromResultResult = Task.FromResult<LibraryPackageInspectionDto?>(null);
        return fromResultResult;
    }

    bool Delete(string libraryId);
    Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken, string origin)
    {
        var archiveAsyncResult = ArchiveAsync(libraryId, cancellationToken);
        return archiveAsyncResult;
    }

    /// <summary>
    /// Rebuilds the safe metadata catalog from the immutable ZIP package.
    /// 从不可变 ZIP 包重新构建安全元数据目录。
    /// </summary>
    Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default);
    Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken, string origin)
    {
        var reindexAsyncResult = ReindexAsync(libraryId, cancellationToken);
        return reindexAsyncResult;
    }

    /// <summary>
    /// Rebuilds packages created by an older catalog scanner version.
    /// 重新扫描由旧版目录扫描器创建的类库包。
    /// </summary>
    Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Lists administrator-defined library families and their immutable
    /// artifacts. Legacy artifacts remain unassigned until an explicit action.
    /// 列出管理员定义的类库族及其不可变工件。历史工件在显式归类前保持未归属。
    /// </summary>
    Task<IReadOnlyList<LibraryFamilyDto>> ListFamiliesAsync(bool includeArchivedArtifacts = true, CancellationToken cancellationToken = default)
    {
        var fromResultResult1 = Task.FromResult<IReadOnlyList<LibraryFamilyDto>>([]);
        return fromResultResult1;
    }

    Task<LibraryFamilyDto?> AssignFamilyAsync(string libraryId, AssignLibraryFamilyRequestDto request, CancellationToken cancellationToken = default)
    {
        var fromResultResult2 = Task.FromResult<LibraryFamilyDto?>(null);
        return fromResultResult2;
    }

    Task<LibraryFamilyDto?> AssignFamilyAsync(string libraryId, AssignLibraryFamilyRequestDto request, CancellationToken cancellationToken, string origin)
    {
        var assignFamilyAsyncResult = AssignFamilyAsync(libraryId, request, cancellationToken);
        return assignFamilyAsyncResult;
    }

    /// <summary>
    /// Changes catalog visibility only. Package bytes and existing flow/run
    /// bindings remain available for deterministic execution and audit.
    /// 只改变目录可见性；包文件以及既有流程/运行绑定仍可用于确定性执行和审计。
    /// </summary>
    Task<bool> SetLifecycleAsync(string libraryId, LibraryLifecycleDto lifecycle, CancellationToken cancellationToken = default)
    {
        global::System.Threading.Tasks.Task<bool> evaluatedValue = lifecycle == LibraryLifecycleDto.Archived ? ArchiveAsync(libraryId, cancellationToken) : Task.FromResult(false);
        return evaluatedValue;
    }

    Task<bool> SetLifecycleAsync(string libraryId, LibraryLifecycleDto lifecycle, CancellationToken cancellationToken, string origin)
    {
        global::System.Threading.Tasks.Task<bool> evaluatedValue3 = lifecycle == LibraryLifecycleDto.Archived ? ArchiveAsync(libraryId, cancellationToken, origin) : Task.FromResult(false);
        return evaluatedValue3;
    }
}

public sealed class LibraryUploadException : Exception
{
    public LibraryUploadException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }

    public LibraryUploadException(string message, Exception innerException, int statusCode = 400) : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed record LibraryPackageInspectionDto(LibraryDto Library, bool AlreadyExists, LibraryArtifactCompatibilityDto? Compatibility = null, LibraryPackageProjectImpactDto? ProjectImpact = null);
