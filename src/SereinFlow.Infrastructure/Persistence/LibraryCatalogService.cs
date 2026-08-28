using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Core.Api;
using SereinFlow.Application.Persistence;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Infrastructure.Persistence;

public sealed record LibraryCatalogOptions
{
    public LibraryCatalogOptions(
        string rootPath,
        long maxPackageBytes = 100 * 1024 * 1024,
        long maxUncompressedBytes = 512 * 1024 * 1024,
        int maxEntries = 512)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Library root path cannot be empty. 类库根目录不能为空。", nameof(rootPath));
        }

        if (maxPackageBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPackageBytes), "Maximum package size must be positive. 最大包大小必须为正数。");
        if (maxUncompressedBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxUncompressedBytes), "Maximum uncompressed size must be positive. 最大解压大小必须为正数。");
        if (maxEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Maximum entry count must be positive. 最大条目数必须为正数。");
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
    private static readonly Action<ILogger, string, Exception?> ReindexSkippedLog = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1001, nameof(ReindexSkippedLog)),
        "Library catalog reindex skipped library {LibraryId}; the saved catalog remains unchanged. 类库目录重建已跳过该类库；已保存目录保持不变。");
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

    private readonly IRepository<LibraryRecord> _libraries;
    private readonly IRepository<LibraryFamilyRecord> _families;
    private readonly IUnitOfWork _unitOfWork;
    private readonly LibraryCatalogOptions _options;
    private readonly ILogger<SqliteLibraryCatalogService>? _logger;
    // The catalog is scoped because it consumes scoped repository services,
    // while uploads must still be serialized across concurrent HTTP requests.
    // Keep the gate process-wide instead of tying it to one request scope.
    private static readonly SemaphoreSlim UploadGate = new(1, 1);

    public SqliteLibraryCatalogService(SqliteDatabase database, LibraryCatalogOptions options)
        : this(
            new SqlSugarRepository<LibraryRecord>(database?.Client ?? throw new ArgumentNullException(nameof(database), "The database cannot be null. 数据库不能为空。")),
            new SqlSugarRepository<LibraryFamilyRecord>(database.Client),
            new SqlSugarUnitOfWork(database.Client),
            options)
    {
    }

    public SqliteLibraryCatalogService(
        IRepository<LibraryRecord> libraries,
        IRepository<LibraryFamilyRecord> families,
        IUnitOfWork unitOfWork,
        LibraryCatalogOptions options,
        ILogger<SqliteLibraryCatalogService>? logger = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries), "The library repository cannot be null. 类库仓储不能为空。");
        _families = families ?? throw new ArgumentNullException(nameof(families), "The library family repository cannot be null. 类库族仓储不能为空。");
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork), "The unit of work cannot be null. 工作单元不能为空。");
        _options = options ?? throw new ArgumentNullException(nameof(options), "Library catalog options cannot be null. 类库目录选项不能为空。");
        _logger = logger;
        Directory.CreateDirectory(_options.RootPath);
        Directory.CreateDirectory(PackagesPath);
    }

    private string PackagesPath => Path.Combine(_options.RootPath, "packages");

    public IReadOnlyList<LibraryDto> List()
        => ListAsync(includeArchived: true, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

    public LibraryDto? Find(string libraryId)
        => FindAsync(libraryId, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<LibraryDto>> ListAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await _libraries.ListAsync(cancellationToken: cancellationToken);
        var familyNames = await GetFamilyNamesAsync(rows, cancellationToken);
        return rows
            .Select(row => Map(row, GetFamilyName(row, familyNames)))
            .Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available)
            .OrderBy(static library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static library => library.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static library => library.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<LibraryDto?> FindAsync(
        string libraryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return null;
        }

        var key = libraryId.Trim();
        var row = await _libraries.GetByIdAsync(key, cancellationToken);
        if (row is null)
            return null;

        var family = string.IsNullOrWhiteSpace(row.FamilyId)
            ? null
            : await _families.GetByIdAsync(row.FamilyId, cancellationToken);
        return Map(row, family?.Name);
    }

    public async Task<LibraryUploadResultDto> UploadAsync(
        Stream package,
        string fileName,
        long? declaredLength = null,
        CancellationToken cancellationToken = default)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package), "The library package stream cannot be null. 类库包流不能为空。");
        ValidateFileName(fileName);
        if (declaredLength is > 0 && declaredLength > _options.MaxPackageBytes)
        {
            throw new LibraryUploadException($"The library package cannot exceed {_options.MaxPackageBytes / (1024 * 1024)} MB. 类库压缩包不能超过 {_options.MaxPackageBytes / (1024 * 1024)} MB。", 413);
        }

        await UploadGate.WaitAsync(cancellationToken);
        var temporaryPath = Path.Combine(_options.RootPath, $".upload-{Guid.NewGuid():N}.tmp");
        try
        {
            var (size, sha256) = await CopyToTemporaryFileAsync(package, temporaryPath, cancellationToken);
            var existing = await FindAsyncCore(sha256, cancellationToken);
            if (existing is not null)
            {
                return new LibraryUploadResultDto(existing, true);
            }

            var packageInfo = await InspectPackageAsync(temporaryPath, fileName, sha256, size, cancellationToken);
            var finalPath = Path.Combine(PackagesPath, $"{sha256}.zip");
            File.Move(temporaryPath, finalPath, overwrite: false);

            var library = packageInfo with { };
            var nodesJson = JsonSerializer.Serialize(library.Nodes, JsonOptions);
            try
            {
                await _unitOfWork.ExecuteAsync(async token =>
                {
                    await _libraries.AddAsync(new LibraryRecord
                    {
                        Id = library.Id,
                        Name = library.Name,
                        Version = library.Version,
                        FileName = library.FileName,
                        SizeBytes = library.SizeBytes,
                        Sha256 = library.Sha256,
                        UploadedAt = library.UploadedAt.ToString("O"),
                        PackagePath = finalPath,
                        NodeCatalogJson = nodesJson,
                        SemanticVersion = library.Version,
                        CompatibilityManifestJson = JsonSerializer.Serialize(library.CompatibilityManifest, JsonOptions),
                        CatalogSchemaVersion = CurrentCatalogSchemaVersion,
                        Status = LibraryLifecycleDto.Available.ToString(),
                        ArchivedAt = null,
                    }, token);
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

            return new LibraryUploadResultDto(library, false);
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

    public bool Delete(string libraryId)
        => ArchiveAsync(libraryId, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<bool> ArchiveAsync(
        string libraryId,
        CancellationToken cancellationToken = default)
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

        return await SetLifecycleAsync(libraryId, LibraryLifecycleDto.Archived, cancellationToken);
    }

    public async Task<bool> SetLifecycleAsync(
        string libraryId,
        LibraryLifecycleDto lifecycle,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
            return false;

        return await _unitOfWork.ExecuteAsync(async token =>
        {
            var row = await _libraries.GetByIdAsync(libraryId.Trim(), token);
            if (row is null)
                return false;

            if (ParseLifecycle(row.Status) != lifecycle)
            {
                row.Status = lifecycle.ToString();
                row.ArchivedAt = lifecycle == LibraryLifecycleDto.Archived
                    ? DateTimeOffset.UtcNow.ToString("O")
                    : null;
                if (!await _libraries.UpdateAsync(row, token))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(row.FamilyId))
            {
                var family = await _families.GetByIdAsync(row.FamilyId, token);
                if (family is not null)
                    await RefreshLatestArtifactAsync(family, token);
            }

            return true;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryFamilyDto>> ListFamiliesAsync(
        bool includeArchivedArtifacts = true,
        CancellationToken cancellationToken = default)
    {
        var families = await _families.ListAsync(cancellationToken: cancellationToken);
        var artifactRows = await _libraries.ListAsync(cancellationToken: cancellationToken);
        var familyNames = families.ToDictionary(family => family.Id, family => family.Name, StringComparer.OrdinalIgnoreCase);
        var artifacts = artifactRows
            .Select(row => Map(row, GetFamilyName(row, familyNames)))
            .Where(library => includeArchivedArtifacts || library.Lifecycle == LibraryLifecycleDto.Available)
            .ToArray();
        return families
            .Select(family => MapFamily(family, artifacts))
            .OrderBy(static family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static family => family.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<LibraryFamilyDto?> AssignFamilyAsync(
        string libraryId,
        AssignLibraryFamilyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(libraryId))
            return null;

        return await _unitOfWork.ExecuteAsync(async token =>
        {
            var library = await _libraries.GetByIdAsync(libraryId.Trim(), token);
            if (library is null)
                return null;

            var previousFamilyId = library.FamilyId;
            LibraryFamilyRecord? family;
            if (!string.IsNullOrWhiteSpace(request.FamilyId))
            {
                family = await _families.GetByIdAsync(request.FamilyId.Trim(), token);
                if (family is null)
                {
                    throw new ArgumentException(
                        "The requested library family does not exist. 指定的类库族不存在。",
                        nameof(request));
                }
            }
            else
            {
                var name = NormalizeFamilyName(request.Name);
                family = (await _families.ListAsync(cancellationToken: token))
                    .SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                if (family is null)
                {
                    var now = DateTimeOffset.UtcNow;
                    family = new LibraryFamilyRecord
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = name,
                        Description = NormalizeOptional(request.Description),
                        CreatedAt = now.ToString("O"),
                        UpdatedAt = now.ToString("O")
                    };
                    await _families.AddAsync(family, token);
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
            if (!string.IsNullOrWhiteSpace(previousFamilyId)
                && !string.Equals(previousFamilyId, family.Id, StringComparison.OrdinalIgnoreCase))
            {
                var previousFamily = await _families.GetByIdAsync(previousFamilyId, token);
                if (previousFamily is not null)
                    await RefreshLatestArtifactAsync(previousFamily, token);
            }

            return MapFamily(family, members.Select(member => Map(member, family.Name)).ToArray());
        }, cancellationToken);
    }

    public async Task<LibraryDto?> ReindexAsync(
        string libraryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return null;
        }

        await UploadGate.WaitAsync(cancellationToken);
        try
        {
            var row = await _libraries.GetByIdAsync(libraryId.Trim(), cancellationToken);
            if (row is null)
            {
                return null;
            }

            var packagePath = Path.GetFullPath(row.PackagePath);
            if (!IsPathWithinRoot(packagePath, PackagesPath) || !File.Exists(packagePath))
            {
                throw new LibraryUploadException(
                    "The stored library package cannot be found in the allowed package root. 已存储类库包不在允许的包目录中或不存在。",
                    422);
            }

            var rescanned = await InspectPackageAsync(
                packagePath,
                row.FileName,
                row.Sha256,
                row.SizeBytes,
                cancellationToken);
            row.NodeCatalogJson = JsonSerializer.Serialize(rescanned.Nodes, JsonOptions);
            row.CompatibilityManifestJson = JsonSerializer.Serialize(rescanned.CompatibilityManifest, JsonOptions);
            row.CatalogSchemaVersion = CurrentCatalogSchemaVersion;
            await _unitOfWork.ExecuteAsync(
                token => _libraries.UpdateAsync(row, token),
                cancellationToken);
            var family = string.IsNullOrWhiteSpace(row.FamilyId)
                ? null
                : await _families.GetByIdAsync(row.FamilyId, cancellationToken);
            return Map(row, family?.Name);
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

    private static LibraryDto Map(LibraryRecord row, string? familyName = null)
    {
        var nodes = JsonSerializer.Deserialize<IReadOnlyList<LibraryNodeDto>>(row.NodeCatalogJson, JsonOptions) ?? [];
        var manifest = string.IsNullOrWhiteSpace(row.CompatibilityManifestJson)
            ? null
            : JsonSerializer.Deserialize<LibraryArtifactManifestDto>(row.CompatibilityManifestJson, JsonOptions);
        var uploadedAt = DateTimeOffset.TryParse(row.UploadedAt, out var parsed)
            ? parsed
            : DateTimeOffset.UnixEpoch;
        return new LibraryDto(
            row.Id,
            row.Name,
            row.Version,
            row.FileName,
            row.SizeBytes,
            row.Sha256,
            uploadedAt,
            nodes,
            ParseLifecycle(row.Status),
            row.FamilyId,
            string.IsNullOrWhiteSpace(row.SemanticVersion) ? row.Version : row.SemanticVersion,
            manifest,
            familyName);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetFamilyNamesAsync(
        IReadOnlyList<LibraryRecord> libraries,
        CancellationToken cancellationToken)
    {
        var familyIds = libraries
            .Select(static library => library.FamilyId)
            .Where(static familyId => !string.IsNullOrWhiteSpace(familyId))
            .Select(static familyId => familyId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (familyIds.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return (await _families.ListAsync(cancellationToken: cancellationToken))
            .Where(family => familyIds.Contains(family.Id))
            .ToDictionary(family => family.Id, family => family.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetFamilyName(
        LibraryRecord library,
        IReadOnlyDictionary<string, string> familyNames)
        => !string.IsNullOrWhiteSpace(library.FamilyId)
            && familyNames.TryGetValue(library.FamilyId, out var familyName)
            ? familyName
            : null;

    private static LibraryFamilyDto MapFamily(
        LibraryFamilyRecord family,
        IEnumerable<LibraryDto> artifacts)
    {
        var members = artifacts
            .Where(item => string.Equals(item.FamilyId, family.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => ParseSemanticVersion(item.SemanticVersion ?? item.Version))
            .ThenByDescending(static item => item.UploadedAt)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .ToArray();
        return new LibraryFamilyDto(
            family.Id,
            family.Name,
            family.Description,
            family.LatestArtifactId,
            ParseDate(family.CreatedAt),
            ParseDate(family.UpdatedAt),
            members);
    }

    /// <summary>
    /// Rebuilds a family's recommendation from immutable members. Archived
    /// artifacts remain visible for audit but are never advertised as the
    /// preferred target for a new reference or upgrade.
    /// 从不可变工件成员重新计算类库族推荐项。已归档工件仍可用于审计展示，
    /// 但不会再被作为新引用或升级的推荐目标。
    /// </summary>
    private async Task<LibraryRecord[]> RefreshLatestArtifactAsync(
        LibraryFamilyRecord family,
        CancellationToken cancellationToken)
    {
        var members = (await _libraries.ListAsync(cancellationToken: cancellationToken))
            .Where(item => string.Equals(item.FamilyId, family.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var latest = members
            .Where(item => ParseLifecycle(item.Status) == LibraryLifecycleDto.Available)
            .OrderByDescending(item => ParseSemanticVersion(item.SemanticVersion ?? item.Version))
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
            throw new ArgumentException(
                "The library family name must contain 1 to 120 characters. 类库族名称长度必须为 1 到 120 个字符。",
                nameof(value));
        }
        return name;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Version ParseSemanticVersion(string value)
        => Version.TryParse(value?.Trim().TrimStart('v', 'V'), out var version)
            ? version
            : new Version(0, 0);

    private static DateTimeOffset ParseDate(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;

    private async Task<LibraryDto?> FindAsyncCore(string libraryId, CancellationToken cancellationToken)
    {
        return await FindAsync(libraryId, cancellationToken);
    }

    private static LibraryLifecycleDto ParseLifecycle(string? status)
        => Enum.TryParse<LibraryLifecycleDto>(status, ignoreCase: true, out var lifecycle)
            ? lifecycle
            : LibraryLifecycleDto.Available;

    private async Task<(long Size, string Sha256)> CopyToTemporaryFileAsync(Stream source, string destinationPath, CancellationToken cancellationToken)
    {
        long total = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > _options.MaxPackageBytes)
            {
                throw new LibraryUploadException($"The library package cannot exceed {_options.MaxPackageBytes / (1024 * 1024)} MB. 类库压缩包不能超过 {_options.MaxPackageBytes / (1024 * 1024)} MB。", 413);
            }

            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        await destination.FlushAsync(cancellationToken);
        return (total, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private async Task<LibraryDto> InspectPackageAsync(
        string packagePath,
        string fileName,
        string sha256,
        long size,
        CancellationToken cancellationToken)
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
        var dllEntries = new List<ZipArchiveEntry>();
        foreach (var entry in archive.Entries)
        {
            var normalizedName = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(normalizedName) || normalizedName.Split('/').Any(static segment => segment is ".."))
            {
                throw new LibraryUploadException("The library package contains an unsafe path. 类库压缩包包含不安全的路径。", 422);
            }

            if (entry.Length > _options.MaxUncompressedBytes || (uncompressedBytes += entry.Length) > _options.MaxUncompressedBytes)
            {
                throw new LibraryUploadException("The uncompressed library content exceeds the safety limit. 类库解压后的内容超过安全大小限制。", 422);
            }

            if (string.Equals(Path.GetFileName(normalizedName), $"{libraryName}.dll", StringComparison.OrdinalIgnoreCase))
            {
                dllEntry = entry;
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
        var metadata = LibraryMetadataScanner.Scan(dllMemory, libraryName, version, sha256, enumMetadataIndex);
        var invalidFlipflop = metadata.Nodes.FirstOrDefault(node =>
            node.Type == NodeTypeDto.Flipflop && !node.IsAwaitable);
        if (invalidFlipflop is not null)
        {
            throw new LibraryUploadException(
                $"Flipflop method '{invalidFlipflop.MethodName}' must return Task or Task<T>. Flipflop 方法“{invalidFlipflop.MethodName}”必须返回 Task 或 Task<T>。",
                422);
        }
        return new LibraryDto(
            sha256,
            libraryName,
            version,
            Path.GetFileName(fileName),
            size,
            sha256,
            DateTimeOffset.UtcNow,
            metadata.Nodes,
            CompatibilityManifest: new LibraryArtifactManifestDto(
                sha256,
                metadata.AssemblyName,
                metadata.AssemblyVersion,
                version,
                metadata.ManifestNodes));
    }

    private static void ValidateFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(['/', '\\']) >= 0
            || !string.Equals(safeName, fileName, StringComparison.Ordinal)
            || !safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new LibraryUploadException("Only ZIP library packages can be uploaded. 只能上传 ZIP 类库压缩包。", 400);
        }
    }

    private static (string Name, string Version) ParsePackageName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        var separator = stem.LastIndexOf('-');
        if (separator <= 0 || separator == stem.Length - 1)
        {
            throw new LibraryUploadException("The filename must use the format [library-name]-[version].zip. 文件名格式不正确，应为：[类库名称]-[版本号].zip。", 422);
        }

        var name = stem[..separator].Trim();
        var version = stem[(separator + 1)..].Trim();
        if (name.Length == 0 || version.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new LibraryUploadException("The library name or version is invalid. 类库名称或版本号无效。", 422);
        }

        return (name, version);
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
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)) + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableReindexFailure(Exception exception)
        => exception is LibraryUploadException
            or InvalidDataException
            or BadImageFormatException
            or IOException
            or UnauthorizedAccessException;

    public void Dispose()
    {
        // UploadGate is process-wide and intentionally lives for the host
        // lifetime; scoped instances must not dispose it underneath another
        // request.
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
        return _ambiguousNames.Contains(normalized)
            ? null
            : _entries.GetValueOrDefault(normalized);
    }

    private static string UnwrapNullable(string typeName)
    {
        var normalized = typeName.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        const string nullablePrefix = "System.Nullable";
        if (!normalized.StartsWith(nullablePrefix, StringComparison.Ordinal)
            || !normalized.EndsWith('>'))
        {
            return normalized;
        }

        var start = normalized.IndexOf('<');
        return start < 0 ? normalized : normalized[(start + 1)..^1];
    }
}

internal static class LibraryMetadataScanner
{
    public static LibraryScanResult Scan(
        Stream assemblyStream,
        string fallbackName,
        string fallbackVersion,
        string libraryId,
        EnumMetadataIndex? enumMetadataIndex = null)
    {
        try
        {
            using var peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return new LibraryScanResult(fallbackName, fallbackVersion, [], []);
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
                var libraryAttribute = FindAttribute(
                        reader,
                        type.GetCustomAttributes(),
                        LibraryAttributeContract.FlowLibraryAttributeFullName);
                if (!libraryAttribute.HasValue)
                {
                    continue;
                }

                var className = GetTypeName(reader, typeHandle);
                var libraryName = ReadLibraryName(reader, type, libraryAttribute.Value, provider);
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    var nodeAttribute = FindAttribute(
                        reader,
                        method.GetCustomAttributes(),
                        LibraryAttributeContract.FlowNodeAttributeFullName);
                    if (!nodeAttribute.HasValue)
                    {
                        continue;
                    }

                    var signature = method.DecodeSignature(provider, null);
                    var nodeMetadata = ReadNodeMetadata(reader, nodeAttribute.Value, provider);
                    var parameterDefinitions = method.GetParameters()
                        .Select(reader.GetParameter)
                        .Where(static parameter => parameter.SequenceNumber > 0)
                        .OrderBy(static parameter => parameter.SequenceNumber)
                        .Select((parameter, index) => new { Parameter = parameter, Index = index })
                        .Where(item => signature.ParameterTypes.Length <= item.Index
                            || !string.Equals(signature.ParameterTypes[item.Index], FlowContextContract.FullName, StringComparison.Ordinal))
                        .ToArray();

                    var methodName = reader.GetString(method.Name);
                    var overloadSignature = $"{className}.{methodName}({string.Join(",", signature.ParameterTypes)})";
                    var nodeContractId = nodeMetadata.ContractId ?? $"{libraryName}.{methodName}";
                    var nodeIdentityConfidence = LibraryContractIdentityConfidenceDto.Explicit;
                    if (!nodeContractIds.Add(nodeContractId))
                    {
                        throw new LibraryUploadException(
                            $"Node contract ID '{nodeContractId}' is duplicated in the library package. 节点契约 ID“{nodeContractId}”在类库包中重复。",
                            422);
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
                        var clrParameterName = parameter.Name.IsNil ? $"param{index + 1}" : reader.GetString(parameter.Name);
                        var parameterName = parameterMetadata.Name ?? clrParameterName;
                        var isVariadic = FindAttribute(
                            reader,
                            parameter.GetCustomAttributes(),
                            LibraryAttributeContract.ParamArrayAttributeFullName).HasValue;
                        var isRequired = !isVariadic
                            && (parameter.Attributes & ParameterAttributes.Optional) == 0
                            && parameterMetadata.IsExplicit;
                        var parameterType = signature.ParameterTypes.Length > index
                            ? signature.ParameterTypes[index]
                            : "System.Object";
                        var parameterId = parameterMetadata.ContractId ?? clrParameterName;
                        var identityConfidence = LibraryContractIdentityConfidenceDto.Explicit;
                        if (!parameterIds.Add(parameterId) || parameterAliases.Contains(parameterId))
                        {
                            throw new LibraryUploadException(
                                $"Parameter contract ID '{parameterId}' is duplicated on node '{nodeContractId}'. 节点“{nodeContractId}”中的参数契约 ID“{parameterId}”重复。",
                                422);
                        }

                        foreach (var alias in parameterMetadata.Aliases)
                        {
                            if (string.Equals(alias, parameterId, StringComparison.Ordinal)
                                || parameterIds.Contains(alias)
                                || !parameterAliases.Add(alias))
                            {
                                throw new LibraryUploadException(
                                    $"Parameter alias '{alias}' is duplicated or conflicts with an active parameter ID on node '{nodeContractId}'. 参数别名“{alias}”重复，或与节点“{nodeContractId}”的活动参数 ID 冲突。",
                                    422);
                            }
                        }

                        var enumMetadata = enumMetadataIndex?.Resolve(parameterType);
                        var defaultValue = ReadParameterDefaultValue(reader, parameter, provider, enumMetadata);
                        var elementType = isVariadic ? GetArrayElementType(parameterType) : null;
                        parameters.Add(new LibraryParameterDto(
                            parameterId,
                            parameterName,
                            parameterType,
                            null,
                            isRequired,
                            isVariadic,
                            isVariadic ? parameterId : null,
                            elementType,
                            enumMetadata,
                            defaultValue,
                            parameterMetadata.Aliases,
                            identityConfidence));
                        manifestParameters.Add(new LibraryManifestParameterDto(
                            parameterId,
                            identityConfidence,
                            parameterMetadata.Aliases,
                            clrParameterName,
                            parameterName,
                            parameterType,
                            isRequired,
                            defaultValue,
                            isVariadic,
                            elementType,
                            enumMetadata));
                    }

                    nodes.Add(new LibraryNodeDto(
                        $"{libraryId}:{nodeContractId}",
                        nodeMetadata.Type,
                        nodeMetadata.DisplayName ?? methodName,
                        nodeMetadata.Description,
                        libraryId,
                        className,
                        methodName,
                        $"{assemblyName}.dll",
                        assemblyVersion,
                        signature.ReturnType,
                        parameters,
                        IsAwaitableReturnType(signature.ReturnType),
                        nodeContractId,
                        overloadSignature,
                        nodeIdentityConfidence,
                        libraryName));
                    manifestNodes.Add(new LibraryManifestNodeDto(
                        nodeContractId,
                        nodeIdentityConfidence,
                        nodeMetadata.Type,
                        className,
                        methodName,
                        overloadSignature,
                        signature.ReturnType,
                        IsAwaitableReturnType(signature.ReturnType),
                        manifestParameters));
                }
            }

            return new LibraryScanResult(
                string.IsNullOrWhiteSpace(assemblyName) ? fallbackName : assemblyName,
                string.IsNullOrWhiteSpace(assemblyVersion) ? fallbackVersion : assemblyVersion,
                nodes,
                manifestNodes);
        }
        catch (BadImageFormatException)
        {
            return new LibraryScanResult(fallbackName, fallbackVersion, [], []);
        }
        catch (ArgumentException)
        {
            return new LibraryScanResult(fallbackName, fallbackVersion, [], []);
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

                    if ((field.Attributes & (FieldAttributes.Static | FieldAttributes.Literal))
                        != (FieldAttributes.Static | FieldAttributes.Literal)
                        || field.GetDefaultValue().IsNil)
                    {
                        continue;
                    }

                    var value = ReadEnumConstant(reader, reader.GetConstant(field.GetDefaultValue()));
                    if (value is not null)
                    {
                        options.Add(new EnumValueOptionDto(fieldName, value));
                    }
                }

                if (options.Count == 0 || string.IsNullOrWhiteSpace(underlyingType))
                {
                    continue;
                }

                index.Add(new EnumParameterMetadataDto(
                    GetTypeName(reader, typeHandle),
                    FindAttribute(reader, type.GetCustomAttributes(), "System.FlagsAttribute").HasValue,
                    underlyingType,
                    options));
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

    private static CustomAttributeHandle? FindAttribute(
        MetadataReader reader,
        CustomAttributeHandleCollection attributes,
        string fullName)
    {
        foreach (var attribute in attributes)
        {
            if (string.Equals(GetAttributeName(reader, attribute), fullName, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    private static bool IsEnumType(MetadataReader reader, TypeDefinition type)
        => !type.BaseType.IsNil
            && string.Equals(GetTypeName(reader, type.BaseType), "System.Enum", StringComparison.Ordinal);

    private static string GetTypeName(MetadataReader reader, EntityHandle handle)
        => handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeName(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => GetTypeName(reader, (TypeReferenceHandle)handle),
            _ => string.Empty,
        };

    private static string? ReadEnumConstant(MetadataReader reader, Constant constant)
    {
        var value = reader.GetBlobReader(constant.Value);
        return constant.TypeCode switch
        {
            ConstantTypeCode.SByte => value.ReadSByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Byte => value.ReadByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int16 => value.ReadInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt16 => value.ReadUInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int32 => value.ReadInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt32 => value.ReadUInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int64 => value.ReadInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt64 => value.ReadUInt64().ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private static string? ReadParameterDefaultValue(
        MetadataReader reader,
        Parameter parameter,
        MetadataTypeNameProvider provider,
        EnumParameterMetadataDto? enumMetadata)
    {
        var constantHandle = parameter.GetDefaultValue();
        if (!constantHandle.IsNil)
        {
            var literal = ReadParameterConstant(reader, reader.GetConstant(constantHandle));
            return enumMetadata is null ? literal : FormatEnumDefaultValue(literal, enumMetadata);
        }

        return ReadDecimalDefaultValue(reader, parameter, provider)
            ?? ReadDateTimeDefaultValue(reader, parameter, provider);
    }

    private static string? ReadParameterConstant(MetadataReader reader, Constant constant)
    {
        var value = reader.GetBlobReader(constant.Value);
        return constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => value.ReadBoolean() ? "true" : "false",
            ConstantTypeCode.Char => ((char)value.ReadUInt16()).ToString(),
            ConstantTypeCode.SByte => value.ReadSByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Byte => value.ReadByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int16 => value.ReadInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt16 => value.ReadUInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int32 => value.ReadInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt32 => value.ReadUInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int64 => value.ReadInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt64 => value.ReadUInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Single => value.ReadSingle().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Double => value.ReadDouble().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.String => System.Text.Encoding.Unicode.GetString(reader.GetBlobBytes(constant.Value)),
            ConstantTypeCode.NullReference => null,
            _ => null,
        };
    }

    private static string? ReadDecimalDefaultValue(
        MetadataReader reader,
        Parameter parameter,
        MetadataTypeNameProvider provider)
    {
        var attribute = FindAttribute(
            reader,
            parameter.GetCustomAttributes(),
            "System.Runtime.CompilerServices.DecimalConstantAttribute");
        if (!attribute.HasValue)
        {
            return null;
        }

        try
        {
            var arguments = reader.GetCustomAttribute(attribute.Value).DecodeValue(provider).FixedArguments;
            if (arguments.Length != 5)
            {
                return null;
            }

            var scale = Convert.ToByte(arguments[0].Value, CultureInfo.InvariantCulture);
            var isNegative = Convert.ToByte(arguments[1].Value, CultureInfo.InvariantCulture) != 0;
            var high = unchecked((int)Convert.ToUInt32(arguments[2].Value, CultureInfo.InvariantCulture));
            var middle = unchecked((int)Convert.ToUInt32(arguments[3].Value, CultureInfo.InvariantCulture));
            var low = unchecked((int)Convert.ToUInt32(arguments[4].Value, CultureInfo.InvariantCulture));
            return new decimal(low, middle, high, isNegative, scale).ToString(CultureInfo.InvariantCulture);
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

    private static string? ReadDateTimeDefaultValue(
        MetadataReader reader,
        Parameter parameter,
        MetadataTypeNameProvider provider)
    {
        var attribute = FindAttribute(
            reader,
            parameter.GetCustomAttributes(),
            "System.Runtime.CompilerServices.DateTimeConstantAttribute");
        if (!attribute.HasValue)
        {
            return null;
        }

        try
        {
            var arguments = reader.GetCustomAttribute(attribute.Value).DecodeValue(provider).FixedArguments;
            if (arguments.Length != 1)
            {
                return null;
            }

            var ticks = Convert.ToInt64(arguments[0].Value, CultureInfo.InvariantCulture);
            return new DateTime(ticks, DateTimeKind.Unspecified).ToString("O", CultureInfo.InvariantCulture);
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
            string.Equals(option.NumericValue, numericValue, StringComparison.Ordinal));
        if (exactMatch is not null)
        {
            return exactMatch.Name;
        }

        if (!metadata.IsFlags
            || !ulong.TryParse(numericValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remaining))
        {
            return null;
        }

        var selected = new List<string>();
        foreach (var option in metadata.Options)
        {
            if (!ulong.TryParse(option.NumericValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optionValue)
                || optionValue == 0
                || (remaining & optionValue) != optionValue)
            {
                continue;
            }

            selected.Add(option.Name);
            remaining &= ~optionValue;
        }

        return remaining == 0 && selected.Count > 0
            ? string.Join(", ", selected)
            : null;
    }

    private static NodeMetadata ReadNodeMetadata(MetadataReader reader, CustomAttributeHandle handle, MetadataTypeNameProvider provider)
    {
        try
        {
            var value = reader.GetCustomAttribute(handle).DecodeValue(provider);
            var typeValue = ReadNamedValue(value, LibraryAttributeContract.NodeTypePropertyName);
            var nodeType = IsEnumValue(typeValue, NodeType.Flipflop)
                ? NodeTypeDto.Flipflop
                : NodeTypeDto.Action;
            var displayName = ReadNamedValue(value, LibraryAttributeContract.DisplayNamePropertyName) as string;
            var description = ReadNamedValue(value, LibraryAttributeContract.DescriptionPropertyName) as string;
            var contractId = NormalizeContractId(
                ReadNamedValue(value, LibraryAttributeContract.NodeContractIdPropertyName) as string,
                "node");
            return new NodeMetadata(
                nodeType,
                string.IsNullOrWhiteSpace(displayName) ? null : displayName,
                string.IsNullOrWhiteSpace(description) ? null : description,
                contractId);
        }
        catch (BadImageFormatException)
        {
            return new NodeMetadata(NodeTypeDto.Action, null, null, null);
        }
        catch (ArgumentException)
        {
            return new NodeMetadata(NodeTypeDto.Action, null, null, null);
        }
    }

    private static string ReadLibraryName(
        MetadataReader reader,
        TypeDefinition type,
        CustomAttributeHandle handle,
        MetadataTypeNameProvider provider)
    {
        var className = reader.GetString(type.Name);
        try
        {
            var value = reader.GetCustomAttribute(handle).DecodeValue(provider);
            var name = value.FixedArguments.Length > 0
                ? value.FixedArguments[0].Value as string
                : null;
            return string.IsNullOrWhiteSpace(name) ? className : name.Trim();
        }
        catch (BadImageFormatException)
        {
            return className;
        }
        catch (ArgumentException)
        {
            return className;
        }
    }

    private static ParameterMetadata ReadParameterMetadata(
        MetadataReader reader,
        Parameter parameter,
        MetadataTypeNameProvider provider)
    {
        var handle = FindAttribute(
            reader,
            parameter.GetCustomAttributes(),
            LibraryAttributeContract.NodeParamAttributeFullName);
        if (!handle.HasValue)
        {
            return new ParameterMetadata(null, null, [], true);
        }

        try
        {
            var value = reader.GetCustomAttribute(handle.Value).DecodeValue(provider);
            var name = ReadNamedValue(value, LibraryAttributeContract.ParameterNamePropertyName) as string;
            var contractId = NormalizeContractId(
                ReadNamedValue(value, LibraryAttributeContract.ParameterContractIdPropertyName) as string,
                "parameter");
            var aliases = ReadNamedStringArray(value, LibraryAttributeContract.ParameterAliasesPropertyName)
                .Select(alias => NormalizeContractId(alias, "parameter alias")!)
                .ToArray();
            var explicitValue = ReadNamedValue(value, LibraryAttributeContract.IsExplicitPropertyName);
            var isExplicit = explicitValue is bool boolValue ? boolValue : true;
            return new ParameterMetadata(
                string.IsNullOrWhiteSpace(name) ? null : name,
                contractId,
                aliases,
                isExplicit);
        }
        catch (BadImageFormatException)
        {
            return new ParameterMetadata(null, null, [], true);
        }
        catch (ArgumentException)
        {
            return new ParameterMetadata(null, null, [], true);
        }
    }

    private static object? ReadNamedValue(CustomAttributeValue<string> value, string name)
        => value.NamedArguments
            .FirstOrDefault(argument => string.Equals(argument.Name, name, StringComparison.Ordinal))
            .Value;

    private static string[] ReadNamedStringArray(CustomAttributeValue<string> value, string name)
    {
        var raw = ReadNamedValue(value, name);
        return raw is ImmutableArray<CustomAttributeTypedArgument<string>> values
            ? values
                .Select(static item => item.Value as string)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item!.Trim())
                .ToArray()
            : [];
    }

    private static string? NormalizeContractId(string? value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        var isValid = normalized.All(static character => (character is >= 'a' and <= 'z')
            || (character is >= '0' and <= '9')
            || character is '.' or '-' or '_');
        if (!isValid)
        {
            throw new LibraryUploadException(
                $"The {kind} contract ID '{normalized}' is invalid; use lowercase letters, digits, '.', '-' or '_'. {kind} 契约 ID“{normalized}”无效；只能使用小写字母、数字、“.”、“-”或“_”。",
                422);
        }

        return normalized;
    }

    private static bool IsEnumValue(object? value, NodeType expected)
    {
        if (value is not IConvertible convertible)
        {
            return false;
        }

        try
        {
            return Convert.ToInt32(convertible, System.Globalization.CultureInfo.InvariantCulture) == (int)expected;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool IsAwaitableReturnType(string returnType)
        => string.Equals(returnType, "System.Threading.Tasks.Task", StringComparison.Ordinal)
            || returnType.StartsWith("System.Threading.Tasks.Task<", StringComparison.Ordinal)
            // Metadata signatures preserve the CLR arity suffix (Task`1<T>)
            // while reflection exposes the same type as Task<T>.
            || returnType.StartsWith("System.Threading.Tasks.Task`1<", StringComparison.Ordinal);

    private static string GetArrayElementType(string type)
        => type.EndsWith("[]", StringComparison.Ordinal) ? type[..^2] : "System.Object";

    private static string GetAttributeName(MetadataReader reader, CustomAttributeHandle handle)
    {
        var attribute = reader.GetCustomAttribute(handle);
        var typeHandle = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default(EntityHandle),
        };
        return typeHandle.Kind switch
        {
            HandleKind.TypeReference => GetTypeName(reader, (TypeReferenceHandle)typeHandle),
            HandleKind.TypeDefinition => GetTypeName(reader, (TypeDefinitionHandle)typeHandle),
            _ => string.Empty,
        };
    }

    private static string GetTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        return declaring.IsNil
            ? CombineNamespace(reader.GetString(type.Namespace), name)
            : $"{GetTypeName(reader, declaring)}+{name}";
    }

    private static string GetTypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        var name = reader.GetString(type.Name);
        return type.ResolutionScope.Kind == HandleKind.TypeReference
            ? $"{GetTypeName(reader, (TypeReferenceHandle)type.ResolutionScope)}+{name}"
            : CombineNamespace(reader.GetString(type.Namespace), name);
    }

    private static string CombineNamespace(string @namespace, string name)
        => string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";
}

internal sealed record NodeMetadata(
    NodeTypeDto Type,
    string? DisplayName,
    string? Description,
    string? ContractId);

internal sealed record ParameterMetadata(
    string? Name,
    string? ContractId,
    IReadOnlyList<string> Aliases,
    bool IsExplicit);

internal sealed record LibraryScanResult(
    string AssemblyName,
    string AssemblyVersion,
    IReadOnlyList<LibraryNodeDto> Nodes,
    IReadOnlyList<LibraryManifestNodeDto> ManifestNodes);

internal sealed class MetadataTypeNameProvider : ISignatureTypeProvider<string, object?>, ICustomAttributeTypeProvider<string>
{
    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', Math.Max(shape.Rank - 1, 0))}]";

    public string GetByReferenceType(string elementType) => $"ref {elementType}";

    public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        => $"{genericType}<{string.Join(", ", typeArguments)}>";

    public string GetGenericMethodParameter(object? genericContext, int index) => $"TMethod{index}";

    public string GetGenericTypeParameter(object? genericContext, int index) => $"T{index}";

    public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;

    public string GetPointerType(string elementType) => $"{elementType}*";

    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        => typeCode switch
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
        => GetTypeName(reader, handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        => GetTypeName(reader, handle);

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSystemType() => "System.Type";

    public string GetTypeFromSerializedName(string name) => name;

    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;

    public bool IsSystemType(string type) => string.Equals(type, "System.Type", StringComparison.Ordinal);

    private static string GetTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        return declaring.IsNil
            ? string.IsNullOrWhiteSpace(reader.GetString(type.Namespace)) ? name : $"{reader.GetString(type.Namespace)}.{name}"
            : $"{GetTypeName(reader, declaring)}+{name}";
    }

    private static string GetTypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        var name = reader.GetString(type.Name);
        return type.ResolutionScope.Kind == HandleKind.TypeReference
            ? $"{GetTypeName(reader, (TypeReferenceHandle)type.ResolutionScope)}+{name}"
            : string.IsNullOrWhiteSpace(reader.GetString(type.Namespace)) ? name : $"{reader.GetString(type.Namespace)}.{name}";
    }
}
