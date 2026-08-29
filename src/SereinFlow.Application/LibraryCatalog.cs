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

    Task<IReadOnlyList<LibraryDto>> ListAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<LibraryDto?> FindAsync(
        string libraryId,
        CancellationToken cancellationToken = default);

    Task<LibraryUploadResultDto> UploadAsync(
        Stream package,
        string fileName,
        long? declaredLength = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects a package without persisting it. MCP may retain the original
    /// bytes only in a bounded, controlled staging area until preview expiry.
    /// 仅扫描而不持久化类库包；MCP 仅可在预览过期前将原始字节保存在受限暂存区。
    /// </summary>
    Task<LibraryPackageInspectionDto?> InspectAsync(
        Stream package,
        string fileName,
        long? declaredLength = null,
        string? familyId = null,
        string? baselineArtifactId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<LibraryPackageInspectionDto?>(null);

    bool Delete(string libraryId);

    Task<bool> ArchiveAsync(
        string libraryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the safe metadata catalog from the immutable ZIP package.
    /// 从不可变 ZIP 包重新构建安全元数据目录。
    /// </summary>
    Task<LibraryDto?> ReindexAsync(
        string libraryId,
        CancellationToken cancellationToken = default);

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
    Task<IReadOnlyList<LibraryFamilyDto>> ListFamiliesAsync(
        bool includeArchivedArtifacts = true,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LibraryFamilyDto>>([]);

    Task<LibraryFamilyDto?> AssignFamilyAsync(
        string libraryId,
        AssignLibraryFamilyRequestDto request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<LibraryFamilyDto?>(null);

    /// <summary>
    /// Changes catalog visibility only. Package bytes and existing flow/run
    /// bindings remain available for deterministic execution and audit.
    /// 只改变目录可见性；包文件以及既有流程/运行绑定仍可用于确定性执行和审计。
    /// </summary>
    Task<bool> SetLifecycleAsync(
        string libraryId,
        LibraryLifecycleDto lifecycle,
        CancellationToken cancellationToken = default)
        => lifecycle == LibraryLifecycleDto.Archived
            ? ArchiveAsync(libraryId, cancellationToken)
            : Task.FromResult(false);
}

public sealed class LibraryUploadException : Exception
{
    public LibraryUploadException(string message, int statusCode = 400)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public LibraryUploadException(string message, Exception innerException, int statusCode = 400)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed record LibraryPackageInspectionDto(
    LibraryDto Library,
    bool AlreadyExists,
    LibraryArtifactCompatibilityDto? Compatibility = null,
    LibraryPackageProjectImpactDto? ProjectImpact = null);
