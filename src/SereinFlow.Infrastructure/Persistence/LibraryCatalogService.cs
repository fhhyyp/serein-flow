using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Application.Persistence;

namespace SereinFlow.Infrastructure.Persistence;

/// <summary>
/// Configures library catalog behavior.
/// 配置库Catalog行为。
/// </summary>
public sealed record LibraryCatalogOptions
{
    public LibraryCatalogOptions(string rootPath, long maxPackageBytes = 100 * 1024 * 1024, long maxUncompressedBytes = 512 * 1024 * 1024, int maxEntries = 512)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Library root path cannot be empty. 类库根目录不能为空。", nameof(rootPath));
        }

        if (maxPackageBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPackageBytes), "Maximum package size must be positive. 最大包大小必须为正数。");
        }

        if (maxUncompressedBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUncompressedBytes), "Maximum uncompressed size must be positive. 最大解压大小必须为正数。");
        }

        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Maximum entry count must be positive. 最大条目数必须为正数。");
        }

        RootPath = Path.GetFullPath(rootPath);
        MaxPackageBytes = maxPackageBytes;
        MaxUncompressedBytes = maxUncompressedBytes;
        MaxEntries = maxEntries;
    }

    public string RootPath { get; }
    public long MaxPackageBytes { get; }
    public long MaxUncompressedBytes { get; }
    public int MaxEntries { get; }
}

/// <summary>
/// Persists uploaded class-library packages and their safe metadata catalog.
/// 保存上传的类库包及其安全元数据目录。
/// The API only reads ZIP/PE metadata; it never calls Assembly.Load or executes
/// code from the uploaded package. The Worker boundary owns runtime loading.
/// API 只读取 ZIP/PE 元数据，不调用 Assembly.Load，也不执行上传包中的代码；运行时加载由 Worker 边界负责。
/// </summary>
public sealed class SqliteLibraryCatalogService : ILibraryCatalogService, IDisposable
{
    internal const int CurrentCatalogSchemaVersion = 6;
    private static readonly Action<ILogger, string, Exception?> ReindexSkippedLog = CreateReindexSkippedLogInitialValue();
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        var jsonStringEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(jsonStringEnumConverter);
    });
    private static readonly string[] DisallowedPackageExtensions = [".cs", ".csx", ".fs", ".fsx", ".vb", ".vbs", ".py", ".ps1", ".psm1", ".psd1", ".cmd", ".bat", ".sh", ".bash", ".zsh", ".fish", ".js", ".mjs", ".cjs", ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj", ".props", ".targets", ".user", ".zip", ".nupkg", ".exe", ".com"];
    private static readonly string[] DisallowedPackageFileNames = ["Dockerfile", "Makefile", "NuGet.Config", "global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "packages.lock.json"];
    private readonly IRepository<LibraryRecord> _libraries;
    private readonly IRepository<LibraryFamilyRecord> _families;
    private readonly IUnitOfWork _unitOfWork;
    private readonly LibraryCatalogOptions _options;
    private readonly ILogger<SqliteLibraryCatalogService>? _logger;
    private readonly IFileUploadSettings? _fileUploadSettings;
    private readonly IWorkspaceChangePublisher? _changePublisher;
    // The catalog is scoped because it consumes scoped repository services,
    // while uploads must still be serialized across concurrent HTTP requests.
    // Keep the gate process-wide instead of tying it to one request scope.
    // 目录服务使用有作用域的仓储，但并发 HTTP 上传仍须串行处理；因此使用进程级门控，而非请求级门控。
    private static readonly SemaphoreSlim UploadGate = new(1, 1);
    public SqliteLibraryCatalogService(SqliteDatabase database, LibraryCatalogOptions options, IFileUploadSettings? fileUploadSettings = null)
    {
        var libraryClient = database?.Client ?? throw new ArgumentNullException(nameof(database), "The database cannot be null. 数据库不能为空。");
        _libraries = new SqlSugarRepository<LibraryRecord>(libraryClient);
        _families = new SqlSugarRepository<LibraryFamilyRecord>(database.Client);
        _unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _options = options ?? throw new ArgumentNullException(nameof(options), "Library catalog options cannot be null. 类库目录选项不能为空。");
        _logger = null;
        _fileUploadSettings = fileUploadSettings;
        _changePublisher = null;
        Directory.CreateDirectory(_options.RootPath);
        Directory.CreateDirectory(PackagesPath);
    }

    public SqliteLibraryCatalogService(IRepository<LibraryRecord> libraries, IRepository<LibraryFamilyRecord> families, IUnitOfWork unitOfWork, LibraryCatalogOptions options, ILogger<SqliteLibraryCatalogService>? logger = null, IFileUploadSettings? fileUploadSettings = null, IWorkspaceChangePublisher? changePublisher = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries), "The library repository cannot be null. 类库仓储不能为空。");
        _families = families ?? throw new ArgumentNullException(nameof(families), "The library family repository cannot be null. 类库族仓储不能为空。");
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork), "The unit of work cannot be null. 工作单元不能为空。");
        _options = options ?? throw new ArgumentNullException(nameof(options), "Library catalog options cannot be null. 类库目录选项不能为空。");
        _logger = logger;
        _fileUploadSettings = fileUploadSettings;
        _changePublisher = changePublisher;
        Directory.CreateDirectory(_options.RootPath);
        Directory.CreateDirectory(PackagesPath);
    }

    private string PackagesPath
    {
        get
        {
            var combineResult = Path.Combine(_options.RootPath, "packages");
            return combineResult;
        }
    }

    private long MaxPackageBytes => _fileUploadSettings?.MaxLibraryUploadBytes ?? _options.MaxPackageBytes;

    public IReadOnlyList<LibraryDto> List()
    {
        var listAsyncResult = ListAsync(includeArchived: true, cancellationToken: CancellationToken.None);
        var getAwaiterResult = listAsyncResult.GetAwaiter();
        var getResultResult = getAwaiterResult.GetResult();
        return getResultResult;
    }

    public LibraryDto? Find(string libraryId)
    {
        var findAsyncResult = FindAsync(libraryId, CancellationToken.None);
        var getAwaiterResult1 = findAsyncResult.GetAwaiter();
        var getResultResult2 = getAwaiterResult1.GetResult();
        return getResultResult2;
    }

    public async Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var rows = await _libraries.ListAsync(cancellationToken: cancellationToken);
        var familyNames = await GetFamilyNamesAsync(rows, cancellationToken);
        var toArrayResult = rows
            .Select(row =>
            {
                var getFamilyNameResult = GetFamilyName(row, familyNames);
                var mapResult = Map(row, getFamilyNameResult);
                return mapResult;
            })
            .Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available)
            .OrderBy(static library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static library => library.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static library => library.Id, StringComparer.Ordinal)
            .ToArray();
        return toArrayResult;
    }

    public async Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return null;
        }

        var key = libraryId.Trim();
        var row = await _libraries.GetByIdAsync(key, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var family = string.IsNullOrWhiteSpace(row.FamilyId) ? null : await _families.GetByIdAsync(row.FamilyId, cancellationToken);
        var mapResult4 = Map(row, family?.Name);
        return mapResult4;
    }

    public async Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
    {
        var uploadAsyncResult = await UploadAsync(package, fileName, declaredLength, cancellationToken, "web");
        return uploadAsyncResult;
    }

    public async Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength, CancellationToken cancellationToken, string origin)
    {
        var uploadCoreAsyncResult = await UploadCoreAsync(package, fileName, declaredLength, origin, expectedSha256: null, cancellationToken);
        return uploadCoreAsyncResult;
    }

    public Task<LibraryUploadResultDto> UploadVerifiedAsync(Stream package, string fileName, long expectedSizeBytes, string expectedSha256, string origin = "mcp", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        var uploadCoreAsyncResult5 = UploadCoreAsync(package, fileName, expectedSizeBytes, origin, expectedSha256, cancellationToken);
        return uploadCoreAsyncResult5;
    }

    private async Task<LibraryUploadResultDto> UploadCoreAsync(Stream package, string fileName, long? declaredLength, string origin, string? expectedSha256, CancellationToken cancellationToken)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package), "The library package stream cannot be null. 类库包流不能为空。");
        }

        ValidateFileName(fileName);
        if (declaredLength is > 0 && declaredLength > MaxPackageBytes)
        {
            throw new LibraryUploadException($"The library package cannot exceed {MaxPackageBytes / (1024 * 1024)} MB. 类库压缩包不能超过 {MaxPackageBytes / (1024 * 1024)} MB。", 413);
        }

        await UploadGate.WaitAsync(cancellationToken);
        var evaluatedValue = $".upload-{Guid.NewGuid():N}.tmp";
        var temporaryPath = Path.Combine(_options.RootPath, evaluatedValue);
        try
        {
            (long size, string sha256) = await CopyToTemporaryFileAsync(package, temporaryPath, cancellationToken);
            if (expectedSha256 is not null && (size != declaredLength || !string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase)))
            {
                throw new LibraryUploadException("The staged library package changed after preview. 暂存的类库包在预览后发生了变化。", 409);
            }

            var existing = await FindAsyncCore(sha256, cancellationToken);
            if (existing is not null)
            {
                var libraryUploadResultDto = new LibraryUploadResultDto(existing, true);
                return libraryUploadResultDto;
            }

            var packageInfo = await InspectPackageAsync(temporaryPath, fileName, sha256, size, cancellationToken);
            var finalPath = Path.Combine(PackagesPath, $"{sha256}.zip");
            File.Move(temporaryPath, finalPath, overwrite: false);
            var library = packageInfo with
            {
            };
            var nodesJson = JsonSerializer.Serialize(library.Nodes, JsonOptions);
            try
            {
                await _unitOfWork.ExecuteAsync(async token =>
                {
                    var libraryRecord = new LibraryRecord
                    {
                        Id = library.Id,
                        Name = library.Name,
                        Version = library.Version,
                        FileName = library.FileName,
                        SizeBytes = library.SizeBytes,
                        Sha256 = library.Sha256,
                        DllSha256 = library.DllSha256,
                        UploadedAt = library.UploadedAt.ToString("O"),
                        PackagePath = finalPath,
                        NodeCatalogJson = nodesJson,
                        SemanticVersion = library.Version,
                        CompatibilityManifestJson = JsonSerializer.Serialize(library.CompatibilityManifest, JsonOptions),
                        CatalogSchemaVersion = CurrentCatalogSchemaVersion,
                        Status = LibraryLifecycleDto.Available.ToString(),
                        ArchivedAt = null,
                    };
                    await _libraries.AddAsync(libraryRecord, token);
                    return true;
                }, cancellationToken);
            }
            catch
            {
                // The database row is the source of truth. If it cannot be
                // committed, remove the moved package so an orphan cannot be
                // loaded by a future Worker run.
                // 数据库提交失败时删除已移动的包，避免 Worker 读取孤儿文件。
                TryDelete(finalPath);
                throw;
            }

            var result = new LibraryUploadResultDto(library, false);
            await PublishCatalogChangeAsync(library.Id, "library.catalog.changed", "library.package.import", origin);
            return result;
        }
        catch (LibraryUploadException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new LibraryUploadException("The library package cannot be read or is corrupted. 类库压缩包无法读取或已损坏。", exception);
        }
        catch (JsonException exception)
        {
            throw new LibraryUploadException("The library node catalog format is invalid. 类库节点清单格式无效。", exception);
        }
        finally
        {
            TryDelete(temporaryPath);
            UploadGate.Release();
        }
    }

    public async Task<LibraryPackageInspectionDto?> InspectAsync(Stream package, string fileName, long? declaredLength = null, string? familyId = null, string? baselineArtifactId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidateFileName(fileName);
        if (declaredLength is > 0 && declaredLength > MaxPackageBytes)
        {
            throw new LibraryUploadException($"The library package cannot exceed {MaxPackageBytes / (1024 * 1024)} MB. 类库压缩包不能超过 {MaxPackageBytes / (1024 * 1024)} MB。", 413);
        }

        await UploadGate.WaitAsync(cancellationToken);
        var evaluatedValue8 = $".inspect-{Guid.NewGuid():N}.tmp";
        var temporaryPath = Path.Combine(_options.RootPath, evaluatedValue8);
        try
        {
            var (size, sha256) = await CopyToTemporaryFileAsync(package, temporaryPath, cancellationToken);
            var existing = await FindAsyncCore(sha256, cancellationToken);
            if (existing is not null)
            {
                var existingBaseline = await ResolveCompatibilityBaselineAsync(familyId, baselineArtifactId, cancellationToken);
                var existingCompatibility = existingBaseline is null ? null : LibraryArtifactCompatibilityAnalyzer.Analyze(existingBaseline, existing);
                var libraryPackageInspectionDto = new LibraryPackageInspectionDto(existing, true, existingCompatibility);
                return libraryPackageInspectionDto;
            }

            var inspected = await InspectPackageAsync(temporaryPath, fileName, sha256, size, cancellationToken);
            var baseline = await ResolveCompatibilityBaselineAsync(familyId, baselineArtifactId, cancellationToken);
            var compatibility = baseline is null ? null : LibraryArtifactCompatibilityAnalyzer.Analyze(baseline, inspected);
            var libraryPackageInspectionDto7 = new LibraryPackageInspectionDto(inspected, false, compatibility);
            return libraryPackageInspectionDto7;
        }
        finally
        {
            TryDelete(temporaryPath);
            UploadGate.Release();
        }
    }

    private async Task<LibraryDto?> ResolveCompatibilityBaselineAsync(string? familyId, string? baselineArtifactId, CancellationToken cancellationToken)
    {
        var normalizedBaseline = baselineArtifactId?.Trim();
        var normalizedFamily = familyId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBaseline) && string.IsNullOrWhiteSpace(normalizedFamily))
        {
            return null;
        }

        LibraryDto? baseline;
        if (!string.IsNullOrWhiteSpace(normalizedBaseline))
        {
            baseline = await FindAsync(normalizedBaseline, cancellationToken);
            if (baseline is null)
            {
                throw new LibraryUploadException("The requested baseline library artifact was not found. 指定的基线类库工件不存在。", 422);
            }
        }
        else
        {
            var family = (await ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken))
                .SingleOrDefault(item =>
                {
                    var equalsResult = string.Equals(item.Id, normalizedFamily, StringComparison.OrdinalIgnoreCase);
                    return equalsResult;
                });
            if (family is null)
            {
                throw new LibraryUploadException("The requested library family was not found. 指定的类库族不存在。", 422);
            }

            baseline = string.IsNullOrWhiteSpace(family.LatestArtifactId) ? null : await FindAsync(family.LatestArtifactId, cancellationToken);
            if (baseline is null)
            {
                throw new LibraryUploadException("The requested library family has no baseline artifact. 指定的类库族没有可用基线工件。", 422);
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedFamily) && !string.Equals(baseline!.FamilyId, normalizedFamily, StringComparison.OrdinalIgnoreCase))
        {
            throw new LibraryUploadException("The baseline artifact does not belong to the requested library family. 基线工件不属于指定的类库族。", 422);
        }

        return baseline;
    }

    public bool Delete(string libraryId)
    {
        var archiveAsyncResult = ArchiveAsync(libraryId, CancellationToken.None);
        var getAwaiterResult10 = archiveAsyncResult.GetAwaiter();
        var getResultResult11 = getAwaiterResult10.GetResult();
        return getResultResult11;
    }

    public async Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        var archiveAsyncResult12 = await ArchiveAsync(libraryId, cancellationToken, "web");
        return archiveAsyncResult12;
    }

    public async Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken, string origin)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return false;
        }

        var key = libraryId.Trim();
        var row = await _libraries.GetByIdAsync(key, cancellationToken);
        if (row is null)
        {
            return false;
        }

        var setLifecycleAsyncResult = await SetLifecycleAsync(libraryId, LibraryLifecycleDto.Archived, cancellationToken, origin);
        return setLifecycleAsyncResult;
    }

    public async Task<bool> SetLifecycleAsync(string libraryId, LibraryLifecycleDto lifecycle, CancellationToken cancellationToken = default)
    {
        var setLifecycleAsyncResult13 = await SetLifecycleAsync(libraryId, lifecycle, cancellationToken, "web");
        return setLifecycleAsyncResult13;
    }

    public async Task<bool> SetLifecycleAsync(string libraryId, LibraryLifecycleDto lifecycle, CancellationToken cancellationToken, string origin)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return false;
        }

        var updated = await _unitOfWork.ExecuteAsync(async token =>
        {
            var trimResult = libraryId.Trim();
            var row = await _libraries.GetByIdAsync(trimResult, token);
            if (row is null)
            {
                return false;
            }

            if (ParseLifecycle(row.Status) != lifecycle)
            {
                row.Status = lifecycle.ToString();
                row.ArchivedAt = lifecycle == LibraryLifecycleDto.Archived ? DateTimeOffset.UtcNow.ToString("O") : null;
                if (!await _libraries.UpdateAsync(row, token))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(row.FamilyId))
            {
                var family = await _families.GetByIdAsync(row.FamilyId, token);
                if (family is not null)
                {
                    await RefreshLatestArtifactAsync(family, token);
                }
            }

            return true;
        }, cancellationToken);
        if (updated)
        {
            await PublishCatalogChangeAsync(libraryId, "library.catalog.changed", "library.lifecycle.change", origin);
        }

        return updated;
    }

    public async Task<IReadOnlyList<LibraryFamilyDto>> ListFamiliesAsync(bool includeArchivedArtifacts = true, CancellationToken cancellationToken = default)
    {
        var families = await _families.ListAsync(cancellationToken: cancellationToken);
        var artifactRows = await _libraries.ListAsync(cancellationToken: cancellationToken);
        var familyNames = families.ToDictionary(family => family.Id, family => family.Name, StringComparer.OrdinalIgnoreCase);
        var artifacts = artifactRows
            .Select(row =>
            {
                var getFamilyNameResult14 = GetFamilyName(row, familyNames);
                var mapResult15 = Map(row, getFamilyNameResult14);
                return mapResult15;
            })
            .Where(library => includeArchivedArtifacts || library.Lifecycle == LibraryLifecycleDto.Available)
            .ToArray();
        var toArrayResult21 = families
            .Select(family =>
            {
                var mapFamilyResult = MapFamily(family, artifacts);
                return mapFamilyResult;
            })
            .OrderBy(static family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static family => family.Id, StringComparer.Ordinal)
            .ToArray();
        return toArrayResult21;
    }

    public async Task<LibraryFamilyDto?> AssignFamilyAsync(string libraryId, AssignLibraryFamilyRequestDto request, CancellationToken cancellationToken = default)
    {
        var assignFamilyAsyncResult = await AssignFamilyAsync(libraryId, request, cancellationToken, "web");
        return assignFamilyAsyncResult;
    }

    public async Task<LibraryFamilyDto?> AssignFamilyAsync(string libraryId, AssignLibraryFamilyRequestDto request, CancellationToken cancellationToken, string origin)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return null;
        }

        var result = await _unitOfWork.ExecuteAsync(async token =>
        {
            var trimResult26 = libraryId.Trim();
            var library = await _libraries.GetByIdAsync(trimResult26, token);
            if (library is null)
            {
                return null;
            }

            var previousFamilyId = library.FamilyId;
            LibraryFamilyRecord? family;
            if (!string.IsNullOrWhiteSpace(request.FamilyId))
            {
                var trimResult22 = request.FamilyId.Trim();
                family = await _families.GetByIdAsync(trimResult22, token);
                if (family is null)
                {
                    throw new ArgumentException("The requested library family does not exist. 指定的类库族不存在。", nameof(request));
                }
            }
            else
            {
                var name = NormalizeFamilyName(request.Name);
                var evaluatedValue24 = (await _families.ListAsync(cancellationToken: token));
                family = evaluatedValue24.SingleOrDefault(item =>
                {
                    var equalsResult23 = string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase);
                    return equalsResult23;
                });
                if (family is null)
                {
                    var now = DateTimeOffset.UtcNow;
                    family = new LibraryFamilyRecord
                    {
                        Id = resolveToStringResult(),
                        Name = name,
                        Description = NormalizeOptional(request.Description),
                        CreatedAt = now.ToString("O"),
                        UpdatedAt = now.ToString("O")
                    };
                    await _families.AddAsync(family, token);
                    string resolveToStringResult()
                    {
                        var newGuidResult = Guid.NewGuid();
                        var toStringResult = newGuidResult.ToString("N");
                        return toStringResult;
                    }
                }
            }

            library.FamilyId = family.Id;
            library.SemanticVersion = string.IsNullOrWhiteSpace(library.SemanticVersion) ? library.Version : library.SemanticVersion;
            await _libraries.UpdateAsync(library, token);
            var members = await RefreshLatestArtifactAsync(family, token);
            // A family assignment moves the artifact instead of copying it.
            // Recompute the old family's recommendation in the same transaction
            // so it cannot retain an artifact that no longer belongs to it.
            // 类库工件归类是迁移而不是复制；在同一事务内重算原类库族的推荐工件，
            // 避免它继续指向已迁出的工件。
            if (!string.IsNullOrWhiteSpace(previousFamilyId) && !string.Equals(previousFamilyId, family.Id, StringComparison.OrdinalIgnoreCase))
            {
                var previousFamily = await _families.GetByIdAsync(previousFamilyId, token);
                if (previousFamily is not null)
                {
                    await RefreshLatestArtifactAsync(previousFamily, token);
                }
            }

            var toArrayResult28 = members
                .Select(member =>
                {
                    var mapResult25 = Map(member, family.Name);
                    return mapResult25;
                })
                .ToArray();
            var mapFamilyResult29 = MapFamily(family, toArrayResult28);
            return mapFamilyResult29;
        }, cancellationToken);
        if (result is not null)
        {
            await PublishCatalogChangeAsync(libraryId, "library.catalog.changed", "library.family.assign", origin);
        }

        return result;
    }

    public async Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        var reindexAsyncResult = await ReindexAsync(libraryId, cancellationToken, "web");
        return reindexAsyncResult;
    }

    public async Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken, string origin)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return null;
        }

        await UploadGate.WaitAsync(cancellationToken);
        try
        {
            var trimResult30 = libraryId.Trim();
            var row = await _libraries.GetByIdAsync(trimResult30, cancellationToken);
            if (row is null)
            {
                return null;
            }

            var packagePath = Path.GetFullPath(row.PackagePath);
            if (!IsPathWithinRoot(packagePath, PackagesPath) || !File.Exists(packagePath))
            {
                throw new LibraryUploadException("The stored library package cannot be found in the allowed package root. 已存储类库包不在允许的包目录中或不存在。", 422);
            }

            var rescanned = await InspectPackageAsync(packagePath, row.FileName, row.Sha256, row.SizeBytes, cancellationToken);
            row.NodeCatalogJson = JsonSerializer.Serialize(rescanned.Nodes, JsonOptions);
            row.CompatibilityManifestJson = JsonSerializer.Serialize(rescanned.CompatibilityManifest, JsonOptions);
            row.CatalogSchemaVersion = CurrentCatalogSchemaVersion;
            await _unitOfWork.ExecuteAsync(token =>
            {
                var updateAsyncResult = _libraries.UpdateAsync(row, token);
                return updateAsyncResult;
            }, cancellationToken);
            var family = string.IsNullOrWhiteSpace(row.FamilyId) ? null : await _families.GetByIdAsync(row.FamilyId, cancellationToken);
            var result = Map(row, family?.Name);
            await PublishCatalogChangeAsync(libraryId, "library.catalog.changed", "library.catalog.reindex", origin);
            return result;
        }
        finally
        {
            UploadGate.Release();
        }
    }

    public async Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _libraries.ListAsync(cancellationToken: cancellationToken);
        var outdatedIds = rows
            .Where(static row => row.CatalogSchemaVersion < CurrentCatalogSchemaVersion)
            .Select(static row => row.Id)
            .ToArray();
        var reindexed = 0;
        foreach (var libraryId in outdatedIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ReindexAsync(libraryId, cancellationToken) is not null)
                {
                    reindexed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsRecoverableReindexFailure(exception))
            {
                if (_logger is not null)
                {
                    ReindexSkippedLog(_logger, libraryId, exception);
                }
            }
        }

        return reindexed;
    }

    private async Task PublishCatalogChangeAsync(string libraryId, string changeType, string operation, string origin)
    {
        if (_changePublisher is null)
        {
            return;
        }

        var newGuidResult33 = Guid.NewGuid();
        var workspaceChangeEventDto = new WorkspaceChangeEventDto(newGuidResult33, DateTimeOffset.UtcNow, changeType, null, null, null, null, origin, operation, LibraryIds: [libraryId]);
        await _changePublisher.PublishAsync(workspaceChangeEventDto, CancellationToken.None);
    }

    private static LibraryDto Map(LibraryRecord row, string? familyName = null)
    {
        var nodes = JsonSerializer.Deserialize<IReadOnlyList<LibraryNodeDto>>(row.NodeCatalogJson, JsonOptions) ?? [];
        var manifest = string.IsNullOrWhiteSpace(row.CompatibilityManifestJson) ? null : JsonSerializer.Deserialize<LibraryArtifactManifestDto>(row.CompatibilityManifestJson, JsonOptions);
        var uploadedAt = DateTimeOffset.TryParse(row.UploadedAt, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;
        var parseLifecycleResult = ParseLifecycle(row.Status);
        string evaluatedValue34 = string.IsNullOrWhiteSpace(row.SemanticVersion) ? row.Version : row.SemanticVersion;
        var libraryDto = new LibraryDto(row.Id, row.Name, row.Version, row.FileName, row.SizeBytes, row.Sha256, uploadedAt, nodes, parseLifecycleResult, row.FamilyId, evaluatedValue34, manifest, familyName, row.DllSha256);
        return libraryDto;
    }

    private async Task<IReadOnlyDictionary<string, string>> GetFamilyNamesAsync(IReadOnlyList<LibraryRecord> libraries, CancellationToken cancellationToken)
    {
        var familyIds = libraries
            .Select(static library => library.FamilyId)
            .Where(static familyId =>
            {
                var evaluatedValue35 = !string.IsNullOrWhiteSpace(familyId);
                return evaluatedValue35;
            })
            .Select(static familyId => familyId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (familyIds.Count == 0)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return dictionary;
        }

        var toDictionaryResult = (await _families.ListAsync(cancellationToken: cancellationToken))
            .Where(family =>
            {
                var containsResult = familyIds.Contains(family.Id);
                return containsResult;
            })
            .ToDictionary(family => family.Id, family => family.Name, StringComparer.OrdinalIgnoreCase);
        return toDictionaryResult;
    }

    private static string? GetFamilyName(LibraryRecord library, IReadOnlyDictionary<string, string> familyNames)
    {
        string? evaluatedValue41 = !string.IsNullOrWhiteSpace(library.FamilyId) && familyNames.TryGetValue(library.FamilyId, out var familyName) ? familyName : null;
        return evaluatedValue41;
    }

    private static LibraryFamilyDto MapFamily(LibraryFamilyRecord family, IEnumerable<LibraryDto> artifacts)
    {
        var members = artifacts
            .Where(item =>
            {
                var equalsResult42 = string.Equals(item.FamilyId, family.Id, StringComparison.OrdinalIgnoreCase);
                return equalsResult42;
            })
            .OrderByDescending(item =>
            {
                var parseSemanticVersionResult = ParseSemanticVersion(item.SemanticVersion ?? item.Version);
                return parseSemanticVersionResult;
            })
            .ThenByDescending(static item => item.UploadedAt)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var parseDateResult = ParseDate(family.CreatedAt);
        var parseDateResult45 = ParseDate(family.UpdatedAt);
        var libraryFamilyDto = new LibraryFamilyDto(family.Id, family.Name, family.Description, family.LatestArtifactId, parseDateResult, parseDateResult45, members);
        return libraryFamilyDto;
    }

    /// <summary>
    /// Rebuilds a family's recommendation from immutable members. Archived
    /// artifacts remain visible for audit but are never advertised as the
    /// preferred target for a new reference or upgrade.
    /// 从不可变工件成员重新计算类库族推荐项。已归档工件仍可用于审计展示，
    /// 但不会再被作为新引用或升级的推荐目标。
    /// </summary>
    private async Task<LibraryRecord[]> RefreshLatestArtifactAsync(LibraryFamilyRecord family, CancellationToken cancellationToken)
    {
        var members = (await _libraries.ListAsync(cancellationToken: cancellationToken))
            .Where(item =>
            {
                var equalsResult46 = string.Equals(item.FamilyId, family.Id, StringComparison.OrdinalIgnoreCase);
                return equalsResult46;
            })
            .ToArray();
        var latest = members
            .Where(item =>
            {
                var evaluatedValue47 = ParseLifecycle(item.Status) == LibraryLifecycleDto.Available;
                return evaluatedValue47;
            })
            .OrderByDescending(item =>
            {
                var parseSemanticVersionResult48 = ParseSemanticVersion(item.SemanticVersion ?? item.Version);
                return parseSemanticVersionResult48;
            })
            .ThenByDescending(static item => item.UploadedAt, StringComparer.Ordinal)
            .ThenByDescending(static item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        family.LatestArtifactId = latest?.Id;
        family.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        await _families.UpdateAsync(family, cancellationToken);
        return members;
    }

    private static string NormalizeFamilyName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            throw new ArgumentException("The library family name must contain 1 to 120 characters. 类库族名称长度必须为 1 到 120 个字符。", nameof(value));
        }

        return name;
    }

    private static string? NormalizeOptional(string? value)
    {
        string? evaluatedValue55 = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return evaluatedValue55;
    }

    private static Version ParseSemanticVersion(string value)
    {
        var optionalReceiver = value;
        string? optionalResult = default(string);
        if (optionalReceiver is not null)
        {
            var trimResult1 = optionalReceiver.Trim();
            optionalResult = trimResult1.TrimStart('v', 'V');
        }

        var evaluatedValue2 = optionalResult;
        Version conditionalValue;
        if (Version.TryParse(evaluatedValue2, out var version))
        {
            conditionalValue = version;
        }
        else
        {
            conditionalValue = new Version(0, 0);
        }

        Version evaluatedValue56 = conditionalValue;
        return evaluatedValue56;
    }

    private static DateTimeOffset ParseDate(string? value)
    {
        DateTimeOffset evaluatedValue57 = DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;
        return evaluatedValue57;
    }

    private async Task<LibraryDto?> FindAsyncCore(string libraryId, CancellationToken cancellationToken)
    {
        var findAsyncResult58 = await FindAsync(libraryId, cancellationToken);
        return findAsyncResult58;
    }

    private static LibraryLifecycleDto ParseLifecycle(string? status)
    {
        LibraryLifecycleDto evaluatedValue59 = Enum.TryParse<LibraryLifecycleDto>(status, ignoreCase: true, out var lifecycle) ? lifecycle : LibraryLifecycleDto.Available;
        return evaluatedValue59;
    }

    private async Task<(long Size, string Sha256)> CopyToTemporaryFileAsync(Stream source, string destinationPath, CancellationToken cancellationToken)
    {
        long total = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var asMemoryResult = buffer.AsMemory();
            var read = await source.ReadAsync(asMemoryResult, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaxPackageBytes)
            {
                throw new LibraryUploadException($"The library package cannot exceed {MaxPackageBytes / (1024 * 1024)} MB. 类库压缩包不能超过 {MaxPackageBytes / (1024 * 1024)} MB。", 413);
            }

            hash.AppendData(buffer, 0, read);
            var asMemoryResult60 = buffer.AsMemory(0, read);
            await destination.WriteAsync(asMemoryResult60, cancellationToken);
        }

        await destination.FlushAsync(cancellationToken);
        var evaluatedValue61 = (total, resolveToLowerInvariantResult());
        return evaluatedValue61;
        string resolveToLowerInvariantResult()
        {
            var getHashAndResetResult = hash.GetHashAndReset();
            var toHexStringResult = Convert.ToHexString(getHashAndResetResult);
            var toLowerInvariantResult = toHexStringResult.ToLowerInvariant();
            return toLowerInvariantResult;
        }
    }

    private async Task<LibraryDto> InspectPackageAsync(string packagePath, string fileName, string sha256, long size, CancellationToken cancellationToken)
    {
        var (libraryName, version) = ParsePackageName(fileName);
        await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count == 0 || archive.Entries.Count > _options.MaxEntries)
        {
            throw new LibraryUploadException($"The library package must contain between 1 and {_options.MaxEntries} files. 类库压缩包必须包含 1 到 {_options.MaxEntries} 个文件。", 422);
        }

        long uncompressedBytes = 0;
        ZipArchiveEntry? dllEntry = null;
        ZipArchiveEntry? symbolsEntry = null;
        var dllEntries = new List<ZipArchiveEntry>();
        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageDirectoryName = $"{libraryName}-{version}";
        var expectedDllPath = $"{packageDirectoryName}/{libraryName}.dll";
        foreach (var entry in archive.Entries)
        {
            var normalizedName = entry.FullName.Replace('\\', '/');
            var isDirectory = string.IsNullOrEmpty(entry.Name);
            var isLegacyRootSymbols = !isDirectory && string.Equals(normalizedName, $"{libraryName}.pdb", StringComparison.OrdinalIgnoreCase);
            if (!isLegacyRootSymbols)
            {
                ValidatePackageEntryPath(normalizedName, packageDirectoryName, isDirectory);
            }

            if (isDirectory)
            {
                continue;
            }

            if (!seenEntries.Add(normalizedName))
            {
                throw new LibraryUploadException($"The library package contains a duplicate file entry '{normalizedName}'. 类库压缩包包含重复的文件条目“{normalizedName}”。", 422);
            }

            if (entry.Length > _options.MaxUncompressedBytes || (uncompressedBytes += entry.Length) > _options.MaxUncompressedBytes)
            {
                throw new LibraryUploadException("The uncompressed library content exceeds the safety limit. 类库解压后的内容超过安全大小限制。", 422);
            }

            var entryFileName = Path.GetFileName(normalizedName);
            if (IsDisallowedPackageFile(normalizedName))
            {
                throw new LibraryUploadException($"The library package contains a source, build, script, archive, or executable file '{entryFileName}'. 类库包不能包含源代码、构建文件、脚本、压缩包或可执行文件“{entryFileName}”。", 422);
            }

            var isExpectedDll = string.Equals(normalizedName, expectedDllPath, StringComparison.OrdinalIgnoreCase);
            var isLibraryNamedDll = string.Equals(entryFileName, $"{libraryName}.dll", StringComparison.OrdinalIgnoreCase);
            if (isLibraryNamedDll && !isExpectedDll)
            {
                if (dllEntry is not null)
                {
                    throw new LibraryUploadException($"The package contains more than one {libraryName}.dll. 压缩包中包含多个 {libraryName}.dll。", 422);
                }

                throw new LibraryUploadException($"The library-matching file must be exactly '{expectedDllPath}'. 与类库匹配的程序集必须位于“{expectedDllPath}”。", 422);
            }

            if (isExpectedDll)
            {
                if (dllEntry is not null)
                {
                    throw new LibraryUploadException($"The package contains more than one {libraryName}.dll. 压缩包中包含多个 {libraryName}.dll。", 422);
                }

                dllEntry = entry;
            }

            var isLibraryNamedSymbols = string.Equals(entryFileName, $"{libraryName}.pdb", StringComparison.OrdinalIgnoreCase);
            if (isLibraryNamedSymbols)
            {
                if (symbolsEntry is not null)
                {
                    throw new LibraryUploadException($"The package contains more than one {libraryName}.pdb. 压缩包中包含多个 {libraryName}.pdb。", 422);
                }

                symbolsEntry = entry;
            }

            if (normalizedName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                dllEntries.Add(entry);
            }
        }

        if (dllEntry is null)
        {
            throw new LibraryUploadException($"The package does not contain the library-matching file {libraryName}.dll. 压缩包中未找到与类库同名的 {libraryName}.dll。", 422);
        }

        var enumMetadataIndex = new EnumMetadataIndex();
        foreach (var candidate in dllEntries)
        {
            await using var candidateMemory = await ReadEntryAsync(candidate, cancellationToken);
            LibraryMetadataScanner.AddEnumDefinitions(candidateMemory, enumMetadataIndex);
        }

        await using var dllMemory = await ReadEntryAsync(dllEntry, cancellationToken);
        var dllBytes = dllMemory.ToArray();
        var hashDataResult = SHA256.HashData(dllBytes);
        var toHexStringResult63 = Convert.ToHexString(hashDataResult);
        var dllSha256 = toHexStringResult63.ToLowerInvariant();
        dllMemory.Position = 0;
        var metadata = LibraryMetadataScanner.Scan(dllMemory, libraryName, version, sha256, enumMetadataIndex);
        if (!string.Equals(metadata.AssemblyName, libraryName, StringComparison.OrdinalIgnoreCase))
        {
            throw new LibraryUploadException($"The DLL assembly name '{metadata.AssemblyName}' does not match the package library name '{libraryName}'. DLL 程序集名称“{metadata.AssemblyName}”与类库包名称“{libraryName}”不匹配。", 422);
        }

        var invalidFlipflop = metadata.Nodes.FirstOrDefault(node => node.Type == NodeTypeDto.Flipflop && !node.IsAwaitable);
        if (invalidFlipflop is not null)
        {
            throw new LibraryUploadException($"Flipflop method '{invalidFlipflop.MethodName}' must return Task or Task<T>. Flipflop 方法“{invalidFlipflop.MethodName}”必须返回 Task 或 Task<T>。", 422);
        }

        var getFileNameResult = Path.GetFileName(fileName);
        var libraryArtifactManifestDto = new LibraryArtifactManifestDto(sha256, metadata.AssemblyName, metadata.AssemblyVersion, version, metadata.ManifestNodes);
        var libraryDto64 = new LibraryDto(sha256, libraryName, version, getFileNameResult, size, sha256, DateTimeOffset.UtcNow, metadata.Nodes, CompatibilityManifest: libraryArtifactManifestDto, DllSha256: dllSha256);
        return libraryDto64;
    }

    private static void ValidateFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(['/', '\\']) >= 0 || !string.Equals(safeName, fileName, StringComparison.Ordinal) || !safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new LibraryUploadException("Only ZIP library packages can be uploaded. 只能上传 ZIP 类库压缩包。", 400);
        }
    }

    private static void ValidatePackageEntryPath(string normalizedName, string packageDirectoryName, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.StartsWith('/') || Regex.IsMatch(normalizedName, "^[A-Za-z]:", RegexOptions.CultureInvariant))
        {
            throw new LibraryUploadException("The library package contains an unsafe path. 类库压缩包包含不安全的路径。", 422);
        }

        var segments = normalizedName.Split('/');
        if (isDirectory && segments[^1].Length == 0)
        {
            segments = segments[..^1];
        }

        if (segments.Length == 0 || !string.Equals(segments[0], packageDirectoryName, StringComparison.OrdinalIgnoreCase) || (!isDirectory && segments.Length < 2) || segments.Any(static segment =>
        {
            var evaluatedValue65 = segment.Length == 0 || segment is "." or ".." || segment.Contains(':') || resolveIndexOfAnyResult() >= 0;
            return evaluatedValue65;
            int resolveIndexOfAnyResult()
            {
                var getInvalidFileNameCharsResult = Path.GetInvalidFileNameChars();
                var indexOfAnyResult = segment.IndexOfAny(getInvalidFileNameCharsResult);
                return indexOfAnyResult;
            }
        }))
        {
            throw new LibraryUploadException($"The library package contains an unsafe or misplaced path '{normalizedName}'. 类库压缩包包含不安全或位置错误的路径“{normalizedName}”。", 422);
        }
    }

    private static bool IsDisallowedPackageFile(string normalizedName)
    {
        var fileName = Path.GetFileName(normalizedName);
        var extension = Path.GetExtension(fileName);
        var evaluatedValue66 = DisallowedPackageFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) || DisallowedPackageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        return evaluatedValue66;
    }

    private static (string Name, string Version) ParsePackageName(string fileName)
    {
        var getFileNameWithoutExtensionResult = Path.GetFileNameWithoutExtension(fileName);
        var stem = getFileNameWithoutExtensionResult.Trim();
        var match = Regex.Match(stem, "^(?<name>.+)-(?<version>\\d+\\.\\d+\\.\\d+(?:[-+][0-9A-Za-z.-]+)?)$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new LibraryUploadException("The filename must use the format [library-name]-[version].zip. 文件名格式不正确，应为：[类库名称]-[版本号].zip。", 422);
        }

        var name = match.Groups["name"].Value.Trim();
        var version = match.Groups["version"].Value.Trim();
        if (name.Length == 0 || version.Length == 0 || resolveIndexOfAnyResult67() >= 0 || resolveIndexOfAnyResult70() >= 0)
        {
            throw new LibraryUploadException("The library name or version is invalid. 类库名称或版本号无效。", 422);
        }

        return (name, version);
        int resolveIndexOfAnyResult67()
        {
            var getInvalidFileNameCharsResult68 = Path.GetInvalidFileNameChars();
            var indexOfAnyResult69 = name.IndexOfAny(getInvalidFileNameCharsResult68);
            return indexOfAnyResult69;
        }

        int resolveIndexOfAnyResult70()
        {
            var getInvalidFileNameCharsResult71 = Path.GetInvalidFileNameChars();
            var indexOfAnyResult72 = version.IndexOfAny(getInvalidFileNameCharsResult71);
            return indexOfAnyResult72;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static async Task<MemoryStream> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream(entry.Length is <= int.MaxValue ? (int)entry.Length : 0);
        await using var source = entry.Open();
        await source.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;
        return stream;
    }

    private static bool IsPathWithinRoot(string candidatePath, string rootPath)
    {
        var normalizedRoot = resolveTrimEndingDirectorySeparatorResult() + Path.DirectorySeparatorChar;
        var startsWithResult = candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        return startsWithResult;
        string resolveTrimEndingDirectorySeparatorResult()
        {
            var getFullPathResult = Path.GetFullPath(rootPath);
            var trimEndingDirectorySeparatorResult = Path.TrimEndingDirectorySeparator(getFullPathResult);
            return trimEndingDirectorySeparatorResult;
        }
    }

    private static bool IsRecoverableReindexFailure(Exception exception) => exception is LibraryUploadException or InvalidDataException or BadImageFormatException or IOException or UnauthorizedAccessException;
    public void Dispose()
    {
        // UploadGate is process-wide and intentionally lives for the host
        // lifetime; scoped instances must not dispose it underneath another
        // request.
        // UploadGate 贯穿宿主生命周期；作用域实例不得在其他请求使用期间将其释放。
    }

    private static Action<ILogger, string, Exception?> CreateReindexSkippedLogInitialValue()
    {
        var eventId = new EventId(1001, nameof(ReindexSkippedLog));
        Action<ILogger, string, Exception?> reindexSkippedLogInitialValue = LoggerMessage.Define<string>(LogLevel.Warning, eventId, "Library catalog reindex skipped library {LibraryId}; the saved catalog remains unchanged. 类库目录重建已跳过该类库；已保存目录保持不变。");
        return reindexSkippedLogInitialValue;
    }
}

internal sealed class EnumMetadataIndex
{
    private readonly Dictionary<string, EnumParameterMetadataDto> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ambiguousNames = new(StringComparer.Ordinal);
    public void Add(EnumParameterMetadataDto metadata)
    {
        if (_ambiguousNames.Contains(metadata.TypeName))
        {
            return;
        }

        if (_entries.ContainsKey(metadata.TypeName))
        {
            _entries.Remove(metadata.TypeName);
            _ambiguousNames.Add(metadata.TypeName);
            return;
        }

        _entries.Add(metadata.TypeName, metadata);
    }

    public EnumParameterMetadataDto? Resolve(string typeName)
    {
        var normalized = UnwrapNullable(typeName);
        EnumParameterMetadataDto? evaluatedValue73 = _ambiguousNames.Contains(normalized) ? null : _entries.GetValueOrDefault(normalized);
        return evaluatedValue73;
    }

    private static string UnwrapNullable(string typeName)
    {
        var replaceResult = typeName.Replace(" ", string.Empty, StringComparison.Ordinal);
        var normalized = replaceResult.Trim();
        const string nullablePrefix = "System.Nullable";
        if (!normalized.StartsWith(nullablePrefix, StringComparison.Ordinal) || !normalized.EndsWith('>'))
        {
            return normalized;
        }

        var start = normalized.IndexOf('<');
        return start < 0 ? normalized : normalized[(start + 1)..^1];
    }
}

internal static class LibraryMetadataScanner
{
    public static LibraryScanResult Scan(Stream assemblyStream, string fallbackName, string fallbackVersion, string libraryId, EnumMetadataIndex? enumMetadataIndex = null)
    {
        try
        {
            using var peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                throw new LibraryUploadException("The library DLL is not a managed PE assembly. 类库 DLL 不是有效的托管 PE 程序集。", 422);
            }

            var reader = peReader.GetMetadataReader();
            var assembly = reader.GetAssemblyDefinition();
            var assemblyName = reader.GetString(assembly.Name);
            var assemblyVersion = assembly.Version.ToString();
            var provider = new MetadataTypeNameProvider();
            var nodes = new List<LibraryNodeDto>();
            var manifestNodes = new List<LibraryManifestNodeDto>();
            var nodeContractIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                var getCustomAttributesResult86 = type.GetCustomAttributes();
                var libraryAttribute = FindAttribute(reader, getCustomAttributesResult86, LibraryAttributeContract.FlowLibraryAttributeFullName);
                if (!libraryAttribute.HasValue)
                {
                    continue;
                }

                var className = GetTypeName(reader, typeHandle);
                var libraryName = ReadLibraryName(reader, type, libraryAttribute.Value, provider);
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    var getCustomAttributesResult75 = method.GetCustomAttributes();
                    var nodeAttribute = FindAttribute(reader, getCustomAttributesResult75, LibraryAttributeContract.FlowNodeAttributeFullName);
                    if (!nodeAttribute.HasValue)
                    {
                        continue;
                    }

                    var signature = method.DecodeSignature(provider, null);
                    var nodeMetadata = ReadNodeMetadata(reader, nodeAttribute.Value, provider);
                    var hasResultConverter = resolveFindAttributeResult().HasValue || resolveFindAttributeResult77().HasValue;
                    var parameterDefinitions = method.GetParameters()
                        .Select(reader.GetParameter)
                        .Where(static parameter => parameter.SequenceNumber > 0)
                        .OrderBy(static parameter => parameter.SequenceNumber)
                        .Select((parameter, index) => new { Parameter = parameter, Index = index })
                        .Where(item =>
                        {
                            var evaluatedValue74 = signature.ParameterTypes.Length <= item.Index || !string.Equals(signature.ParameterTypes[item.Index], FlowContextContract.FullName, StringComparison.Ordinal);
                            return evaluatedValue74;
                        })
                        .ToArray();
                    var methodName = reader.GetString(method.Name);
                    var overloadSignature = $"{className}.{methodName}({string.Join(",", signature.ParameterTypes)})";
                    var nodeContractId = nodeMetadata.ContractId ?? $"{libraryName}.{methodName}";
                    var nodeIdentityConfidence = LibraryContractIdentityConfidenceDto.Explicit;
                    if (!nodeContractIds.Add(nodeContractId))
                    {
                        throw new LibraryUploadException($"Node contract ID '{nodeContractId}' is duplicated in the library package. 节点契约 ID“{nodeContractId}”在类库包中重复。", 422);
                    }

                    var parameters = new List<LibraryParameterDto>();
                    var manifestParameters = new List<LibraryManifestParameterDto>();
                    var parameterIds = new HashSet<string>(StringComparer.Ordinal);
                    var parameterAliases = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in parameterDefinitions)
                    {
                        var parameter = item.Parameter;
                        var index = item.Index;
                        var parameterMetadata = ReadParameterMetadata(reader, parameter, provider);
                        var clrParameterName = parameter.Name.IsNil ? null : reader.GetString(parameter.Name);
                        var parameterId = parameterMetadata.ContractId ?? clrParameterName;
                        if (string.IsNullOrWhiteSpace(parameterId))
                        {
                            throw new LibraryUploadException($"Parameter {index + 1} on node '{nodeContractId}' has no CLR name; specify NodeParamAttribute.Id explicitly. 节点“{nodeContractId}”的第 {index + 1} 个参数没有 CLR 参数名，必须显式指定 NodeParamAttribute.Id。", 422);
                        }

                        var parameterName = parameterMetadata.Name ?? clrParameterName ?? parameterId;
                        var getCustomAttributesResult = parameter.GetCustomAttributes();
                        var isVariadic = FindAttribute(reader, getCustomAttributesResult, LibraryAttributeContract.ParamArrayAttributeFullName).HasValue;
                        var isRequired = !isVariadic && (parameter.Attributes & ParameterAttributes.Optional) == 0 && parameterMetadata.IsExplicit;
                        var parameterType = signature.ParameterTypes.Length > index ? signature.ParameterTypes[index] : "System.Object";
                        var identityConfidence = LibraryContractIdentityConfidenceDto.Explicit;
                        if (!parameterIds.Add(parameterId) || parameterAliases.Contains(parameterId))
                        {
                            throw new LibraryUploadException($"Parameter contract ID '{parameterId}' is duplicated on node '{nodeContractId}'. 节点“{nodeContractId}”中的参数契约 ID“{parameterId}”重复。", 422);
                        }

                        foreach (var alias in parameterMetadata.Aliases)
                        {
                            if (string.Equals(alias, parameterId, StringComparison.Ordinal) || parameterIds.Contains(alias) || !parameterAliases.Add(alias))
                            {
                                throw new LibraryUploadException($"Parameter alias '{alias}' is duplicated or conflicts with an active parameter ID on node '{nodeContractId}'. 参数别名“{alias}”重复，或与节点“{nodeContractId}”的活动参数 ID 冲突。", 422);
                            }
                        }

                        var enumMetadata = enumMetadataIndex?.Resolve(parameterType);
                        var defaultValue = ReadParameterDefaultValue(reader, parameter, provider, enumMetadata);
                        var elementType = isVariadic ? GetArrayElementType(parameterType) : null;
                        var libraryParameterDto = new LibraryParameterDto(parameterId, parameterName, parameterType, null, isRequired, isVariadic, isVariadic ? parameterId : null, elementType, enumMetadata, defaultValue, parameterMetadata.Aliases, identityConfidence);
                        parameters.Add(libraryParameterDto);
                        var libraryManifestParameterDto = new LibraryManifestParameterDto(parameterId, identityConfidence, parameterMetadata.Aliases, clrParameterName ?? parameterId, parameterName, parameterType, isRequired, defaultValue, isVariadic, elementType, enumMetadata);
                        manifestParameters.Add(libraryManifestParameterDto);
                    }

                    var isAwaitableReturnTypeResult = IsAwaitableReturnType(signature.ReturnType);
                    var libraryNodeDto = new LibraryNodeDto($"{libraryId}:{nodeContractId}", nodeMetadata.Type, nodeMetadata.DisplayName ?? methodName, nodeMetadata.Description, libraryId, className, methodName, $"{assemblyName}.dll", assemblyVersion, signature.ReturnType, parameters, isAwaitableReturnTypeResult, nodeContractId, overloadSignature, nodeIdentityConfidence, libraryName, hasResultConverter);
                    nodes.Add(libraryNodeDto);
                    var isAwaitableReturnTypeResult85 = IsAwaitableReturnType(signature.ReturnType);
                    var libraryManifestNodeDto = new LibraryManifestNodeDto(nodeContractId, nodeIdentityConfidence, nodeMetadata.Type, className, methodName, overloadSignature, signature.ReturnType, isAwaitableReturnTypeResult85, manifestParameters, hasResultConverter);
                    manifestNodes.Add(libraryManifestNodeDto);
                    CustomAttributeHandle? resolveFindAttributeResult()
                    {
                        var getCustomAttributesResult76 = method.GetCustomAttributes();
                        var findAttributeResult = FindAttribute(reader, getCustomAttributesResult76, LibraryAttributeContract.NodeResultAttributeFullName);
                        return findAttributeResult;
                    }

                    CustomAttributeHandle? resolveFindAttributeResult77()
                    {
                        var getCustomAttributesResult78 = method.GetCustomAttributes();
                        var findAttributeResult79 = FindAttribute(reader, getCustomAttributesResult78, LibraryAttributeContract.NodeResultGenericAttributeFullName);
                        return findAttributeResult79;
                    }
                }
            }

            string evaluatedValue87 = string.IsNullOrWhiteSpace(assemblyName) ? fallbackName : assemblyName;
            string evaluatedValue88 = string.IsNullOrWhiteSpace(assemblyVersion) ? fallbackVersion : assemblyVersion;
            var libraryScanResult = new LibraryScanResult(evaluatedValue87, evaluatedValue88, nodes, manifestNodes);
            return libraryScanResult;
        }
        catch (BadImageFormatException exception)
        {
            throw new LibraryUploadException("The library DLL is not a valid PE assembly. 类库 DLL 不是有效的 PE 程序集。", exception, 422);
        }
        catch (ArgumentException exception)
        {
            throw new LibraryUploadException("The library DLL metadata cannot be decoded. 类库 DLL 元数据无法解码。", exception, 422);
        }
    }

    public static void AddEnumDefinitions(Stream assemblyStream, EnumMetadataIndex index)
    {
        try
        {
            using var peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return;
            }

            var reader = peReader.GetMetadataReader();
            var provider = new MetadataTypeNameProvider();
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                if (!IsEnumType(reader, type))
                {
                    continue;
                }

                var options = new List<EnumValueOptionDto>();
                string? underlyingType = null;
                foreach (var fieldHandle in type.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    var fieldName = reader.GetString(field.Name);
                    if (string.Equals(fieldName, "value__", StringComparison.Ordinal))
                    {
                        underlyingType = field.DecodeSignature(provider, genericContext: null);
                        continue;
                    }

                    if ((field.Attributes & (FieldAttributes.Static | FieldAttributes.Literal)) != (FieldAttributes.Static | FieldAttributes.Literal) || field.GetDefaultValue().IsNil)
                    {
                        continue;
                    }

                    var getDefaultValueResult = field.GetDefaultValue();
                    var getConstantResult = reader.GetConstant(getDefaultValueResult);
                    var value = ReadEnumConstant(reader, getConstantResult);
                    if (value is not null)
                    {
                        var enumValueOptionDto = new EnumValueOptionDto(fieldName, value);
                        options.Add(enumValueOptionDto);
                    }
                }

                if (options.Count == 0 || string.IsNullOrWhiteSpace(underlyingType))
                {
                    continue;
                }

                var getTypeNameResult = GetTypeName(reader, typeHandle);
                var getCustomAttributesResult89 = type.GetCustomAttributes();
                var evaluatedValue90 = FindAttribute(reader, getCustomAttributesResult89, "System.FlagsAttribute").HasValue;
                var enumParameterMetadataDto = new EnumParameterMetadataDto(getTypeNameResult, evaluatedValue90, underlyingType, options);
                index.Add(enumParameterMetadataDto);
            }
        }
        catch (BadImageFormatException)
        {
            // A non-managed dependency cannot contribute enum options.
            // 非托管依赖无法提供枚举选项，安全跳过。
        }
        catch (ArgumentException)
        {
            // Metadata that cannot be decoded must not make an upload execute
            // or load code; the affected parameter falls back to text input.
            // 无法解码的元数据不会触发代码加载，受影响参数回退为文本输入。
        }
    }

    private static CustomAttributeHandle? FindAttribute(MetadataReader reader, CustomAttributeHandleCollection attributes, string fullName)
    {
        foreach (var attribute in attributes)
        {
            var getAttributeNameResult = GetAttributeName(reader, attribute);
            if (string.Equals(getAttributeNameResult, fullName, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    private static bool IsEnumType(MetadataReader reader, TypeDefinition type)
    {
        var evaluatedValue93 = !type.BaseType.IsNil && resolveEqualsResult();
        return evaluatedValue93;
        bool resolveEqualsResult()
        {
            var getTypeNameResult91 = GetTypeName(reader, type.BaseType);
            var equalsResult92 = string.Equals(getTypeNameResult91, "System.Enum", StringComparison.Ordinal);
            return equalsResult92;
        }
    }

    private static string GetTypeName(MetadataReader reader, EntityHandle handle)
    {
        string evaluatedValue94 = handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeName(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => GetTypeName(reader, (TypeReferenceHandle)handle),
            _ => string.Empty,
        };
        return evaluatedValue94;
    }

    private static string? ReadEnumConstant(MetadataReader reader, Constant constant)
    {
        var value = reader.GetBlobReader(constant.Value);
        string? evaluatedValue111 = constant.TypeCode switch
        {
            ConstantTypeCode.SByte => resolveToStringResult95(),
            ConstantTypeCode.Byte => resolveToStringResult97(),
            ConstantTypeCode.Int16 => resolveToStringResult99(),
            ConstantTypeCode.UInt16 => resolveToStringResult101(),
            ConstantTypeCode.Int32 => resolveToStringResult103(),
            ConstantTypeCode.UInt32 => resolveToStringResult105(),
            ConstantTypeCode.Int64 => resolveToStringResult107(),
            ConstantTypeCode.UInt64 => resolveToStringResult109(),
            _ => null,
        };
        return evaluatedValue111;
        string resolveToStringResult95()
        {
            var readSByteResult = value.ReadSByte();
            var toStringResult96 = readSByteResult.ToString(CultureInfo.InvariantCulture);
            return toStringResult96;
        }

        string resolveToStringResult97()
        {
            var readByteResult = value.ReadByte();
            var toStringResult98 = readByteResult.ToString(CultureInfo.InvariantCulture);
            return toStringResult98;
        }

        string resolveToStringResult99()
        {
            var readInt16Result = value.ReadInt16();
            var toStringResult100 = readInt16Result.ToString(CultureInfo.InvariantCulture);
            return toStringResult100;
        }

        string resolveToStringResult101()
        {
            var readUInt16Result = value.ReadUInt16();
            var toStringResult102 = readUInt16Result.ToString(CultureInfo.InvariantCulture);
            return toStringResult102;
        }

        string resolveToStringResult103()
        {
            var readInt32Result = value.ReadInt32();
            var toStringResult104 = readInt32Result.ToString(CultureInfo.InvariantCulture);
            return toStringResult104;
        }

        string resolveToStringResult105()
        {
            var readUInt32Result = value.ReadUInt32();
            var toStringResult106 = readUInt32Result.ToString(CultureInfo.InvariantCulture);
            return toStringResult106;
        }

        string resolveToStringResult107()
        {
            var readInt64Result = value.ReadInt64();
            var toStringResult108 = readInt64Result.ToString(CultureInfo.InvariantCulture);
            return toStringResult108;
        }

        string resolveToStringResult109()
        {
            var readUInt64Result = value.ReadUInt64();
            var toStringResult110 = readUInt64Result.ToString(CultureInfo.InvariantCulture);
            return toStringResult110;
        }
    }

    private static string? ReadParameterDefaultValue(MetadataReader reader, Parameter parameter, MetadataTypeNameProvider provider, EnumParameterMetadataDto? enumMetadata)
    {
        var constantHandle = parameter.GetDefaultValue();
        if (!constantHandle.IsNil)
        {
            var getConstantResult112 = reader.GetConstant(constantHandle);
            var literal = ReadParameterConstant(reader, getConstantResult112);
            string? evaluatedValue113 = enumMetadata is null ? literal : FormatEnumDefaultValue(literal, enumMetadata);
            return evaluatedValue113;
        }

        var evaluatedValue114 = ReadDecimalDefaultValue(reader, parameter, provider) ?? ReadDateTimeDefaultValue(reader, parameter, provider);
        return evaluatedValue114;
    }

    private static string? ReadParameterConstant(MetadataReader reader, Constant constant)
    {
        var value = reader.GetBlobReader(constant.Value);
        string? evaluatedValue146 = constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => value.ReadBoolean() ? "true" : "false",
            ConstantTypeCode.Char => resolveToStringResult115(),
            ConstantTypeCode.SByte => resolveToStringResult118(),
            ConstantTypeCode.Byte => resolveToStringResult121(),
            ConstantTypeCode.Int16 => resolveToStringResult124(),
            ConstantTypeCode.UInt16 => resolveToStringResult127(),
            ConstantTypeCode.Int32 => resolveToStringResult130(),
            ConstantTypeCode.UInt32 => resolveToStringResult133(),
            ConstantTypeCode.Int64 => resolveToStringResult136(),
            ConstantTypeCode.UInt64 => resolveToStringResult139(),
            ConstantTypeCode.Single => resolveToStringResult142(),
            ConstantTypeCode.Double => resolveToStringResult144(),
            ConstantTypeCode.String => resolveGetStringResult(),
            ConstantTypeCode.NullReference => null,
            _ => null,
        };
        return evaluatedValue146;
        string resolveToStringResult115()
        {
            var evaluatedValue116 = ((char)value.ReadUInt16());
            var toStringResult117 = evaluatedValue116.ToString();
            return toStringResult117;
        }

        string resolveToStringResult118()
        {
            var readSByteResult119 = value.ReadSByte();
            var toStringResult120 = readSByteResult119.ToString(CultureInfo.InvariantCulture);
            return toStringResult120;
        }

        string resolveToStringResult121()
        {
            var readByteResult122 = value.ReadByte();
            var toStringResult123 = readByteResult122.ToString(CultureInfo.InvariantCulture);
            return toStringResult123;
        }

        string resolveToStringResult124()
        {
            var readInt16Result125 = value.ReadInt16();
            var toStringResult126 = readInt16Result125.ToString(CultureInfo.InvariantCulture);
            return toStringResult126;
        }

        string resolveToStringResult127()
        {
            var readUInt16Result128 = value.ReadUInt16();
            var toStringResult129 = readUInt16Result128.ToString(CultureInfo.InvariantCulture);
            return toStringResult129;
        }

        string resolveToStringResult130()
        {
            var readInt32Result131 = value.ReadInt32();
            var toStringResult132 = readInt32Result131.ToString(CultureInfo.InvariantCulture);
            return toStringResult132;
        }

        string resolveToStringResult133()
        {
            var readUInt32Result134 = value.ReadUInt32();
            var toStringResult135 = readUInt32Result134.ToString(CultureInfo.InvariantCulture);
            return toStringResult135;
        }

        string resolveToStringResult136()
        {
            var readInt64Result137 = value.ReadInt64();
            var toStringResult138 = readInt64Result137.ToString(CultureInfo.InvariantCulture);
            return toStringResult138;
        }

        string resolveToStringResult139()
        {
            var readUInt64Result140 = value.ReadUInt64();
            var toStringResult141 = readUInt64Result140.ToString(CultureInfo.InvariantCulture);
            return toStringResult141;
        }

        string resolveToStringResult142()
        {
            var readSingleResult = value.ReadSingle();
            var toStringResult143 = readSingleResult.ToString(CultureInfo.InvariantCulture);
            return toStringResult143;
        }

        string resolveToStringResult144()
        {
            var readDoubleResult = value.ReadDouble();
            var toStringResult145 = readDoubleResult.ToString(CultureInfo.InvariantCulture);
            return toStringResult145;
        }

        string resolveGetStringResult()
        {
            var getBlobBytesResult = reader.GetBlobBytes(constant.Value);
            var getStringResult = System.Text.Encoding.Unicode.GetString(getBlobBytesResult);
            return getStringResult;
        }
    }

    private static string? ReadDecimalDefaultValue(MetadataReader reader, Parameter parameter, MetadataTypeNameProvider provider)
    {
        var getCustomAttributesResult148 = parameter.GetCustomAttributes();
        var attribute = FindAttribute(reader, getCustomAttributesResult148, "System.Runtime.CompilerServices.DecimalConstantAttribute");
        if (!attribute.HasValue)
        {
            return null;
        }

        try
        {
            var getCustomAttributeResult = reader.GetCustomAttribute(attribute.Value);
            var arguments = getCustomAttributeResult.DecodeValue(provider).FixedArguments;
            if (arguments.Length != 5)
            {
                return null;
            }

            var scale = Convert.ToByte(arguments[0].Value, CultureInfo.InvariantCulture);
            var isNegative = Convert.ToByte(arguments[1].Value, CultureInfo.InvariantCulture) != 0;
            var high = unchecked((int)Convert.ToUInt32(arguments[2].Value, CultureInfo.InvariantCulture));
            var middle = unchecked((int)Convert.ToUInt32(arguments[3].Value, CultureInfo.InvariantCulture));
            var low = unchecked((int)Convert.ToUInt32(arguments[4].Value, CultureInfo.InvariantCulture));
            var decimalValue = new decimal(low, middle, high, isNegative, scale);
            var toStringResult147 = decimalValue.ToString(CultureInfo.InvariantCulture);
            return toStringResult147;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static string? ReadDateTimeDefaultValue(MetadataReader reader, Parameter parameter, MetadataTypeNameProvider provider)
    {
        var getCustomAttributesResult151 = parameter.GetCustomAttributes();
        var attribute = FindAttribute(reader, getCustomAttributesResult151, "System.Runtime.CompilerServices.DateTimeConstantAttribute");
        if (!attribute.HasValue)
        {
            return null;
        }

        try
        {
            var getCustomAttributeResult149 = reader.GetCustomAttribute(attribute.Value);
            var arguments = getCustomAttributeResult149.DecodeValue(provider).FixedArguments;
            if (arguments.Length != 1)
            {
                return null;
            }

            var ticks = Convert.ToInt64(arguments[0].Value, CultureInfo.InvariantCulture);
            var dateTime = new DateTime(ticks, DateTimeKind.Unspecified);
            var toStringResult150 = dateTime.ToString("O", CultureInfo.InvariantCulture);
            return toStringResult150;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static string? FormatEnumDefaultValue(string? numericValue, EnumParameterMetadataDto metadata)
    {
        if (string.IsNullOrWhiteSpace(numericValue))
        {
            return null;
        }

        var exactMatch = metadata.Options.FirstOrDefault(option =>
        {
            var equalsResult152 = string.Equals(option.NumericValue, numericValue, StringComparison.Ordinal);
            return equalsResult152;
        });
        if (exactMatch is not null)
        {
            return exactMatch.Name;
        }

        if (!metadata.IsFlags || !ulong.TryParse(numericValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remaining))
        {
            return null;
        }

        var selected = new List<string>();
        foreach (var option in metadata.Options)
        {
            if (!ulong.TryParse(option.NumericValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optionValue) || optionValue == 0 || (remaining & optionValue) != optionValue)
            {
                continue;
            }

            selected.Add(option.Name);
            remaining &= ~optionValue;
        }

        string? evaluatedValue153 = remaining == 0 && selected.Count > 0 ? string.Join(", ", selected) : null;
        return evaluatedValue153;
    }

    private static NodeMetadata ReadNodeMetadata(MetadataReader reader, CustomAttributeHandle handle, MetadataTypeNameProvider provider)
    {
        try
        {
            var getCustomAttributeResult154 = reader.GetCustomAttribute(handle);
            var value = getCustomAttributeResult154.DecodeValue(provider);
            var readNamedValueResult = ReadNamedValue(value, LibraryAttributeContract.NodeTypePropertyName);
            var nodeType = ReadNodeType(readNamedValueResult);
            var displayName = ReadNamedValue(value, LibraryAttributeContract.DisplayNamePropertyName) as string;
            var description = ReadNamedValue(value, LibraryAttributeContract.DescriptionPropertyName) as string;
            var evaluatedValue155 = ReadNamedValue(value, LibraryAttributeContract.NodeContractIdPropertyName) as string;
            var contractId = NormalizeContractId(evaluatedValue155, "node");
            string? evaluatedValue156 = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
            string? evaluatedValue157 = string.IsNullOrWhiteSpace(description) ? null : description;
            var nodeMetadata158 = new NodeMetadata(nodeType, evaluatedValue156, evaluatedValue157, contractId);
            return nodeMetadata158;
        }
        catch (BadImageFormatException exception)
        {
            throw new LibraryUploadException("The FlowNodeAttribute metadata is invalid. FlowNodeAttribute 元数据无效。", exception, 422);
        }
        catch (ArgumentException exception)
        {
            throw new LibraryUploadException("The FlowNodeAttribute metadata is invalid. FlowNodeAttribute 元数据无效。", exception, 422);
        }
    }

    private static string ReadLibraryName(MetadataReader reader, TypeDefinition type, CustomAttributeHandle handle, MetadataTypeNameProvider provider)
    {
        var className = reader.GetString(type.Name);
        try
        {
            var getCustomAttributeResult159 = reader.GetCustomAttribute(handle);
            var value = getCustomAttributeResult159.DecodeValue(provider);
            var name = value.FixedArguments.Length > 0 ? value.FixedArguments[0].Value as string : null;
            string evaluatedValue160 = string.IsNullOrWhiteSpace(name) ? className : name.Trim();
            return evaluatedValue160;
        }
        catch (BadImageFormatException exception)
        {
            throw new LibraryUploadException("The FlowLibraryAttribute metadata is invalid. FlowLibraryAttribute 元数据无效。", exception, 422);
        }
        catch (ArgumentException exception)
        {
            throw new LibraryUploadException("The FlowLibraryAttribute metadata is invalid. FlowLibraryAttribute 元数据无效。", exception, 422);
        }
    }

    private static ParameterMetadata ReadParameterMetadata(MetadataReader reader, Parameter parameter, MetadataTypeNameProvider provider)
    {
        var getCustomAttributesResult168 = parameter.GetCustomAttributes();
        var handle = FindAttribute(reader, getCustomAttributesResult168, LibraryAttributeContract.NodeParamAttributeFullName);
        if (!handle.HasValue)
        {
            var parameterMetadata161 = new ParameterMetadata(null, null, [], true);
            return parameterMetadata161;
        }

        try
        {
            var getCustomAttributeResult163 = reader.GetCustomAttribute(handle.Value);
            var value = getCustomAttributeResult163.DecodeValue(provider);
            var name = ReadNamedValue(value, LibraryAttributeContract.ParameterNamePropertyName) as string;
            var evaluatedValue164 = ReadNamedValue(value, LibraryAttributeContract.ParameterContractIdPropertyName) as string;
            var contractId = NormalizeContractId(evaluatedValue164, "parameter");
            var aliases = ReadNamedStringArray(value, LibraryAttributeContract.ParameterAliasesPropertyName)
                .Select(alias =>
                {
                    var evaluatedValue162 = NormalizeContractId(alias, "parameter alias")!;
                    return evaluatedValue162;
                })
                .ToArray();
            var explicitValue = ReadNamedValue(value, LibraryAttributeContract.IsExplicitPropertyName);
            var isExplicit = explicitValue is bool boolValue ? boolValue : true;
            string? evaluatedValue166 = string.IsNullOrWhiteSpace(name) ? null : name;
            var parameterMetadata167 = new ParameterMetadata(evaluatedValue166, contractId, aliases, isExplicit);
            return parameterMetadata167;
        }
        catch (BadImageFormatException exception)
        {
            throw new LibraryUploadException("The NodeParamAttribute metadata is invalid. NodeParamAttribute 元数据无效。", exception, 422);
        }
        catch (ArgumentException exception)
        {
            throw new LibraryUploadException("The NodeParamAttribute metadata is invalid. NodeParamAttribute 元数据无效。", exception, 422);
        }
    }

    private static object? ReadNamedValue(CustomAttributeValue<string> value, string name)
    {
        var evaluatedValue170 = value.NamedArguments.FirstOrDefault(argument =>
        {
            var equalsResult169 = string.Equals(argument.Name, name, StringComparison.Ordinal);
            return equalsResult169;
        }).Value;
        return evaluatedValue170;
    }

    private static string[] ReadNamedStringArray(CustomAttributeValue<string> value, string name)
    {
        var raw = ReadNamedValue(value, name);
        string[] evaluatedValue177 = raw is ImmutableArray<CustomAttributeTypedArgument<string>> values ? resolveToArrayResult() : [];
        return evaluatedValue177;
        string[] resolveToArrayResult()
        {
            var toArrayResult176 = values.Select(static item => item.Value as string)
                .Where(static item =>
                {
                    var evaluatedValue171 = !string.IsNullOrWhiteSpace(item);
                    return evaluatedValue171;
                })
                .Select(static item =>
                {
                    var trimResult172 = item!.Trim();
                    return trimResult172;
                })
                .ToArray();
            return toArrayResult176;
        }
    }

    private static string? NormalizeContractId(string? value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        var isValid = normalized.All(static character => (character is >= 'a' and <= 'z') || (character is >= '0' and <= '9') || character is '.' or '-' or '_');
        if (!isValid)
        {
            throw new LibraryUploadException($"The {kind} contract ID '{normalized}' is invalid; use lowercase letters, digits, '.', '-' or '_'. {kind} 契约 ID“{normalized}”无效；只能使用小写字母、数字、“.”、“-”或“_”。", 422);
        }

        return normalized;
    }

    private static NodeTypeDto ReadNodeType(object? value)
    {
        if (value is null)
        {
            return NodeTypeDto.Action;
        }

        if (value is not IConvertible convertible)
        {
            throw new LibraryUploadException("The FlowNodeAttribute NodeType value is invalid. FlowNodeAttribute 的 NodeType 值无效。", 422);
        }

        try
        {
            NodeTypeDto evaluatedValue178 = Convert.ToInt32(convertible, CultureInfo.InvariantCulture) switch
            {
                (int)NodeType.Action => NodeTypeDto.Action,
                (int)NodeType.Flipflop => NodeTypeDto.Flipflop,
                _ => throw new LibraryUploadException("The FlowNodeAttribute NodeType value is unsupported. FlowNodeAttribute 的 NodeType 值不受支持。", 422),
            };
            return evaluatedValue178;
        }
        catch (FormatException)
        {
            throw new LibraryUploadException("The FlowNodeAttribute NodeType value is invalid. FlowNodeAttribute 的 NodeType 值无效。", 422);
        }
        catch (InvalidCastException)
        {
            throw new LibraryUploadException("The FlowNodeAttribute NodeType value is invalid. FlowNodeAttribute 的 NodeType 值无效。", 422);
        }
        catch (OverflowException)
        {
            throw new LibraryUploadException("The FlowNodeAttribute NodeType value is invalid. FlowNodeAttribute 的 NodeType 值无效。", 422);
        }
    }

    private static bool IsAwaitableReturnType(string returnType)
    {
        var evaluatedValue179 = string.Equals(returnType, "System.Threading.Tasks.Task", StringComparison.Ordinal) || returnType.StartsWith("System.Threading.Tasks.Task<", StringComparison.Ordinal) // Metadata signatures preserve the CLR arity suffix (Task`1<T>)
                                                                                                                                                                                                      // while reflection exposes the same type as Task<T>.
        // 元数据签名保留 CLR 泛型元数后缀（Task`1<T>），而反射将同一类型表示为 Task<T>。
        || returnType.StartsWith("System.Threading.Tasks.Task`1<", StringComparison.Ordinal);
        return evaluatedValue179;
    }

    private static string GetArrayElementType(string type)
    {
        string evaluatedValue180 = type.EndsWith("[]", StringComparison.Ordinal) ? type[..^2] : "System.Object";
        return evaluatedValue180;
    }

    private static string GetAttributeName(MetadataReader reader, CustomAttributeHandle handle)
    {
        var attribute = reader.GetCustomAttribute(handle);
        var typeHandle = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => resolveGetDeclaringTypeResult(),
            _ => default(EntityHandle),
        };
        string evaluatedValue181 = typeHandle.Kind switch
        {
            HandleKind.TypeReference => GetTypeName(reader, (TypeReferenceHandle)typeHandle),
            HandleKind.TypeDefinition => GetTypeName(reader, (TypeDefinitionHandle)typeHandle),
            _ => string.Empty,
        };
        return evaluatedValue181;
        EntityHandle resolveGetDeclaringTypeResult()
        {
            var getMethodDefinitionResult = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
            var getDeclaringTypeResult = getMethodDefinitionResult.GetDeclaringType();
            return getDeclaringTypeResult;
        }
    }

    private static string GetTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        string evaluatedValue183 = declaring.IsNil ? resolveCombineNamespaceResult() : $"{GetTypeName(reader, declaring)}+{name}";
        return evaluatedValue183;
        string resolveCombineNamespaceResult()
        {
            var getStringResult182 = reader.GetString(type.Namespace);
            var combineNamespaceResult = CombineNamespace(getStringResult182, name);
            return combineNamespaceResult;
        }
    }

    private static string GetTypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        var name = reader.GetString(type.Name);
        string evaluatedValue187 = type.ResolutionScope.Kind == HandleKind.TypeReference ? $"{GetTypeName(reader, (TypeReferenceHandle)type.ResolutionScope)}+{name}" : resolveCombineNamespaceResult184();
        return evaluatedValue187;
        string resolveCombineNamespaceResult184()
        {
            var getStringResult185 = reader.GetString(type.Namespace);
            var combineNamespaceResult186 = CombineNamespace(getStringResult185, name);
            return combineNamespaceResult186;
        }
    }

    private static string CombineNamespace(string @namespace, string name)
    {
        string evaluatedValue188 = string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";
        return evaluatedValue188;
    }
}

/// <summary>
/// Represents node metadata data used by SereinFlow.
/// 表示 SereinFlow 使用的节点Metadata数据。
/// </summary>
internal sealed record NodeMetadata(NodeTypeDto Type, string? DisplayName, string? Description, string? ContractId);
/// <summary>
/// Represents parameter metadata data used by SereinFlow.
/// 表示 SereinFlow 使用的参数Metadata数据。
/// </summary>
internal sealed record ParameterMetadata(string? Name, string? ContractId, IReadOnlyList<string> Aliases, bool IsExplicit);
/// <summary>
/// Captures the outcome of library scan operations.
/// 记录库Scan操作的结果。
/// </summary>
internal sealed record LibraryScanResult(string AssemblyName, string AssemblyVersion, IReadOnlyList<LibraryNodeDto> Nodes, IReadOnlyList<LibraryManifestNodeDto> ManifestNodes);
internal sealed class MetadataTypeNameProvider : ISignatureTypeProvider<string, object?>, ICustomAttributeTypeProvider<string>
{
    public string GetArrayType(string elementType, ArrayShape shape)
    {
        var evaluatedValue189 = $"{elementType}[{resolvestring()}]";
        return evaluatedValue189;
        string resolvestring()
        {
            var maxResult = Math.Max(shape.Rank - 1, 0);
            var stringValue = new string(',', maxResult);
            return stringValue;
        }
    }

    public string GetByReferenceType(string elementType) => $"ref {elementType}";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var evaluatedValue190 = $"{genericType}<{string.Join(", ", typeArguments)}>";
        return evaluatedValue190;
    }

    public string GetGenericMethodParameter(object? genericContext, int index) => $"TMethod{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"T{index}";
    public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Void => "System.Void",
        _ => "System.Object",
    };
    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var getTypeNameResult191 = GetTypeName(reader, handle);
        return getTypeNameResult191;
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var getTypeNameResult192 = GetTypeName(reader, handle);
        return getTypeNameResult192;
    }

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var getTypeSpecificationResult = reader.GetTypeSpecification(handle);
        var decodeSignatureResult = getTypeSpecificationResult.DecodeSignature(this, genericContext);
        return decodeSignatureResult;
    }

    public string GetSystemType() => "System.Type";
    public string GetTypeFromSerializedName(string name) => name;
    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
    public bool IsSystemType(string type)
    {
        var equalsResult193 = string.Equals(type, "System.Type", StringComparison.Ordinal);
        return equalsResult193;
    }

    private static string GetTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        string evaluatedValue195 = declaring.IsNil ? resolveIsNullOrWhiteSpaceResult() ? name : $"{reader.GetString(type.Namespace)}.{name}" : $"{GetTypeName(reader, declaring)}+{name}";
        return evaluatedValue195;
        bool resolveIsNullOrWhiteSpaceResult()
        {
            var getStringResult194 = reader.GetString(type.Namespace);
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(getStringResult194);
            return isNullOrWhiteSpaceResult;
        }
    }

    private static string GetTypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        var name = reader.GetString(type.Name);
        string evaluatedValue199 = type.ResolutionScope.Kind == HandleKind.TypeReference ? $"{GetTypeName(reader, (TypeReferenceHandle)type.ResolutionScope)}+{name}" : resolveIsNullOrWhiteSpaceResult196() ? name : $"{reader.GetString(type.Namespace)}.{name}";
        return evaluatedValue199;
        bool resolveIsNullOrWhiteSpaceResult196()
        {
            var getStringResult197 = reader.GetString(type.Namespace);
            var isNullOrWhiteSpaceResult198 = string.IsNullOrWhiteSpace(getStringResult197);
            return isNullOrWhiteSpaceResult198;
        }
    }
}
