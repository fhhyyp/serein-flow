using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SqlSugar;

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
            throw new ArgumentException("Library root path cannot be empty.", nameof(rootPath));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxPackageBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxUncompressedBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
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
/// The API only reads ZIP/PE metadata; it never calls Assembly.Load or executes
/// code from the uploaded package. The Worker boundary owns runtime loading.
/// </summary>
public sealed class SqliteLibraryCatalogService : ILibraryCatalogService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly SqliteDatabase _database;
    private readonly LibraryCatalogOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteLibraryCatalogService(SqliteDatabase database, LibraryCatalogOptions options)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Directory.CreateDirectory(_options.RootPath);
        Directory.CreateDirectory(PackagesPath);
    }

    private string PackagesPath => Path.Combine(_options.RootPath, "packages");

    public IReadOnlyList<LibraryDto> List()
        => QueryRows()
            .Select(Map)
            .Where(static library => library is not null)
            .Cast<LibraryDto>()
            .ToArray();

    public LibraryDto? Find(string libraryId)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return null;
        }

        var rows = _database.Query<LibraryRow>(
            "SELECT Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson FROM Libraries WHERE Id = @id LIMIT 1",
            new SugarParameter("@id", libraryId.Trim()));
        var row = rows.Count == 0 ? null : rows[0];
        return row is null ? null : Map(row);
    }

    public async Task<LibraryUploadResultDto> UploadAsync(
        Stream package,
        string fileName,
        long? declaredLength = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidateFileName(fileName);
        if (declaredLength is > 0 && declaredLength > _options.MaxPackageBytes)
        {
            throw new LibraryUploadException($"类库压缩包不能超过 {_options.MaxPackageBytes / (1024 * 1024)} MB。", 413);
        }

        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = Path.Combine(_options.RootPath, $".upload-{Guid.NewGuid():N}.tmp");
        try
        {
            var (size, sha256) = await CopyToTemporaryFileAsync(package, temporaryPath, cancellationToken);
            var existing = Find(sha256);
            if (existing is not null)
            {
                return new LibraryUploadResultDto(existing, true);
            }

            var packageInfo = await InspectPackageAsync(temporaryPath, fileName, sha256, size, cancellationToken);
            var finalPath = Path.Combine(PackagesPath, $"{sha256}.zip");
            File.Move(temporaryPath, finalPath, overwrite: false);

            var library = packageInfo with { };
            var nodesJson = JsonSerializer.Serialize(library.Nodes, JsonOptions);
            _database.Execute(
                "INSERT INTO Libraries (Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson) VALUES (@id, @name, @version, @fileName, @sizeBytes, @sha256, @uploadedAt, @packagePath, @nodeCatalogJson)",
                new SugarParameter("@id", library.Id),
                new SugarParameter("@name", library.Name),
                new SugarParameter("@version", library.Version),
                new SugarParameter("@fileName", library.FileName),
                new SugarParameter("@sizeBytes", library.SizeBytes),
                new SugarParameter("@sha256", library.Sha256),
                new SugarParameter("@uploadedAt", library.UploadedAt.ToString("O")),
                new SugarParameter("@packagePath", finalPath),
                new SugarParameter("@nodeCatalogJson", nodesJson));

            return new LibraryUploadResultDto(library, false);
        }
        catch (LibraryUploadException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new LibraryUploadException("类库压缩包无法读取或已损坏。", exception);
        }
        catch (JsonException exception)
        {
            throw new LibraryUploadException("类库节点清单格式无效。", exception);
        }
        finally
        {
            TryDelete(temporaryPath);
            _gate.Release();
        }
    }

    public bool Delete(string libraryId)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return false;
        }

        var rows = _database.Query<LibraryRow>(
            "SELECT Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson FROM Libraries WHERE Id = @id LIMIT 1",
            new SugarParameter("@id", libraryId.Trim()));
        var row = rows.Count == 0 ? null : rows[0];
        if (row is null)
        {
            return false;
        }

        _database.Execute("DELETE FROM Libraries WHERE Id = @id", new SugarParameter("@id", libraryId.Trim()));
        TryDelete(row.PackagePath);
        return true;
    }

    private IReadOnlyList<LibraryRow> QueryRows()
        => _database.Query<LibraryRow>("SELECT Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson FROM Libraries ORDER BY Name, Version");

    private static LibraryDto Map(LibraryRow row)
    {
        var nodes = JsonSerializer.Deserialize<IReadOnlyList<LibraryNodeDto>>(row.NodeCatalogJson, JsonOptions) ?? [];
        var uploadedAt = DateTimeOffset.TryParse(row.UploadedAt, out var parsed)
            ? parsed
            : DateTimeOffset.UnixEpoch;
        return new LibraryDto(row.Id, row.Name, row.Version, row.FileName, row.SizeBytes, row.Sha256, uploadedAt, nodes);
    }

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
                throw new LibraryUploadException($"类库压缩包不能超过 {_options.MaxPackageBytes / (1024 * 1024)} MB。", 413);
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
            throw new LibraryUploadException($"类库压缩包必须包含 1 到 {_options.MaxEntries} 个文件。", 422);
        }

        long uncompressedBytes = 0;
        ZipArchiveEntry? dllEntry = null;
        foreach (var entry in archive.Entries)
        {
            var normalizedName = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(normalizedName) || normalizedName.Split('/').Any(static segment => segment is ".."))
            {
                throw new LibraryUploadException("类库压缩包包含不安全的路径。", 422);
            }

            if (entry.Length > _options.MaxUncompressedBytes || (uncompressedBytes += entry.Length) > _options.MaxUncompressedBytes)
            {
                throw new LibraryUploadException("类库解压后的内容超过安全大小限制。", 422);
            }

            if (string.Equals(Path.GetFileName(normalizedName), $"{libraryName}.dll", StringComparison.OrdinalIgnoreCase))
            {
                dllEntry = entry;
            }
        }

        if (dllEntry is null)
        {
            throw new LibraryUploadException($"压缩包中未找到与类库同名的 {libraryName}.dll。", 422);
        }

        var dllMemory = new MemoryStream();
        await using (var dllStream = dllEntry.Open())
        {
            await dllStream.CopyToAsync(dllMemory, cancellationToken);
        }

        dllMemory.Position = 0;
        var metadata = LibraryMetadataScanner.Scan(dllMemory, libraryName, version, sha256);
        return new LibraryDto(
            sha256,
            libraryName,
            version,
            Path.GetFileName(fileName),
            size,
            sha256,
            DateTimeOffset.UtcNow,
            metadata.Nodes);
    }

    private static void ValidateFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(['/', '\\']) >= 0
            || !string.Equals(safeName, fileName, StringComparison.Ordinal)
            || !safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new LibraryUploadException("只能上传 ZIP 类库压缩包。", 400);
        }
    }

    private static (string Name, string Version) ParsePackageName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        var separator = stem.LastIndexOf('-');
        if (separator <= 0 || separator == stem.Length - 1)
        {
            throw new LibraryUploadException("文件名格式不正确，应为：[类库名称]-[版本号].zip。", 422);
        }

        var name = stem[..separator].Trim();
        var version = stem[(separator + 1)..].Trim();
        if (name.Length == 0 || version.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new LibraryUploadException("类库名称或版本号无效。", 422);
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

    public void Dispose() => _gate.Dispose();

    private sealed class LibraryRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string UploadedAt { get; set; } = string.Empty;
        public string PackagePath { get; set; } = string.Empty;
        public string NodeCatalogJson { get; set; } = "[]";
    }
}

internal static class LibraryMetadataScanner
{
    public static LibraryScanResult Scan(Stream assemblyStream, string fallbackName, string fallbackVersion, string libraryId)
    {
        try
        {
            using var peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return new LibraryScanResult(fallbackName, fallbackVersion, []);
            }

            var reader = peReader.GetMetadataReader();
            var assembly = reader.GetAssemblyDefinition();
            var assemblyName = reader.GetString(assembly.Name);
            var assemblyVersion = assembly.Version.ToString();
            var provider = new MetadataTypeNameProvider();
            var nodes = new List<LibraryNodeDto>();

            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                if (!HasAttribute(reader, type.GetCustomAttributes(), "FlowLibraryAttribute"))
                {
                    continue;
                }

                var className = GetTypeName(reader, typeHandle);
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if (!HasAttribute(reader, method.GetCustomAttributes(), "FlowNodeAttribute"))
                    {
                        continue;
                    }

                    var signature = method.DecodeSignature(provider, null);
                    var nodeAttribute = method.GetCustomAttributes().First(attribute => GetAttributeName(reader, attribute).EndsWith("FlowNodeAttribute", StringComparison.Ordinal));
                    var nodeMetadata = ReadNodeMetadata(reader, nodeAttribute, provider);
                    var parameters = method.GetParameters()
                        .Select(reader.GetParameter)
                        .Where(static parameter => parameter.SequenceNumber > 0)
                        .OrderBy(static parameter => parameter.SequenceNumber)
                        .Select((parameter, index) => new LibraryParameterDto(
                            $"param-{index + 1}",
                            parameter.Name.IsNil ? $"param{index + 1}" : reader.GetString(parameter.Name),
                            signature.ParameterTypes.Length > index ? signature.ParameterTypes[index] : "System.Object",
                            null,
                            (parameter.Attributes & ParameterAttributes.Optional) == 0))
                        .ToArray();

                    var methodName = reader.GetString(method.Name);
                    nodes.Add(new LibraryNodeDto(
                        $"{libraryId}:{className}:{methodName}",
                        nodeMetadata.Type,
                        nodeMetadata.DisplayName ?? methodName,
                        nodeMetadata.Description,
                        libraryId,
                        className,
                        methodName,
                        $"{assemblyName}.dll",
                        assemblyVersion,
                        signature.ReturnType,
                        parameters));
                }
            }

            return new LibraryScanResult(
                string.IsNullOrWhiteSpace(assemblyName) ? fallbackName : assemblyName,
                string.IsNullOrWhiteSpace(assemblyVersion) ? fallbackVersion : assemblyVersion,
                nodes);
        }
        catch (BadImageFormatException)
        {
            return new LibraryScanResult(fallbackName, fallbackVersion, []);
        }
        catch (ArgumentException)
        {
            return new LibraryScanResult(fallbackName, fallbackVersion, []);
        }
    }

    private static bool HasAttribute(MetadataReader reader, CustomAttributeHandleCollection attributes, string suffix)
        => attributes.Any(attribute => GetAttributeName(reader, attribute).EndsWith(suffix, StringComparison.Ordinal));

    private static NodeMetadata ReadNodeMetadata(MetadataReader reader, CustomAttributeHandle handle, MetadataTypeNameProvider provider)
    {
        try
        {
            var value = reader.GetCustomAttribute(handle).DecodeValue(provider);
            var typeValue = value.NamedArguments.FirstOrDefault(argument => argument.Name == "NodeType").Value;
            var nodeType = typeValue switch
            {
                byte byteValue when byteValue == 1 => NodeTypeDto.Flipflop,
                short shortValue when shortValue == 1 => NodeTypeDto.Flipflop,
                int intValue when intValue == 1 => NodeTypeDto.Flipflop,
                long longValue when longValue == 1 => NodeTypeDto.Flipflop,
                _ => NodeTypeDto.Action,
            };
            var displayName = value.NamedArguments.FirstOrDefault(argument => argument.Name == "AnotherName").Value as string;
            var description = value.NamedArguments.FirstOrDefault(argument => argument.Name == "Desc").Value as string;
            return new NodeMetadata(nodeType, string.IsNullOrWhiteSpace(displayName) ? null : displayName, string.IsNullOrWhiteSpace(description) ? null : description);
        }
        catch (BadImageFormatException)
        {
            return new NodeMetadata(NodeTypeDto.Action, null, null);
        }
        catch (ArgumentException)
        {
            return new NodeMetadata(NodeTypeDto.Action, null, null);
        }
    }

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

internal sealed record NodeMetadata(NodeTypeDto Type, string? DisplayName, string? Description);

internal sealed record LibraryScanResult(string AssemblyName, string AssemblyVersion, IReadOnlyList<LibraryNodeDto> Nodes);

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
