using System.Text;
using Microsoft.Extensions.Configuration;

using SereinFlow.Contracts;
namespace SereinFlow.Mcp;

public sealed record McpAiGuidanceResource(
    string Key,
    string Uri,
    string Name,
    string Description,
    string DefaultFilePath);

public enum McpAiGuidanceId
{
    Guide,
    SereinFlow,
    SereinLang,
    LibraryPackage,
    SereinFlowProjects,
    SereinFlowFlows,
    SereinFlowUiUx,
    SereinFlowRuntime,
    SereinFlowWorkpieces,
    SereinFlowApiKeys,
    SereinFlowRelease,
    SereinLangSyntax,
    SereinLangHost,
    SereinLangGrammar,
    LibraryBuild,
    LibraryZip,
    LibraryMetadata,
    LibraryImport,
    LibraryUpgrade
}

public static class McpAiGuidance
{
    private const string AiRoot = McpResourceUris.Scheme + "://ai";
    private const string SkillsRoot = AiRoot + "/skills";
    private const string SereinFlowSkillsRoot = SkillsRoot + "/sereinflow";
    private const string SereinLangSkillsRoot = SkillsRoot + "/sereinlang";
    private const string LibraryPackageSkillsRoot = SkillsRoot + "/sereinflow-library-package";

    public const string ResourceUri = AiRoot + "/guide";
    public const string ResourceName = "ai-guide";
    public const string SereinFlowResourceUri = SereinFlowSkillsRoot;
    public const string SereinLangResourceUri = SereinLangSkillsRoot;
    public const string LibraryPackageResourceUri = LibraryPackageSkillsRoot;
    public const string MimeType = "text/markdown";

    public const string SereinFlowProjectsResourceUri = SereinFlowSkillsRoot + "/projects";
    public const string SereinFlowFlowsResourceUri = SereinFlowSkillsRoot + "/flows";
    public const string SereinFlowUiUxResourceUri = SereinFlowSkillsRoot + "/ui-ux";
    public const string SereinFlowRuntimeResourceUri = SereinFlowSkillsRoot + "/runtime";
    public const string SereinFlowWorkpiecesResourceUri = SereinFlowSkillsRoot + "/workpieces";
    public const string SereinFlowApiKeysResourceUri = SereinFlowSkillsRoot + "/api-keys";
    public const string SereinFlowReleaseResourceUri = SereinFlowSkillsRoot + "/release";
    public const string SereinLangSyntaxResourceUri = SereinLangSkillsRoot + "/syntax";
    public const string SereinLangHostResourceUri = SereinLangSkillsRoot + "/host";
    public const string SereinLangGrammarResourceUri = SereinLangSkillsRoot + "/grammar";
    public const string LibraryBuildResourceUri = LibraryPackageSkillsRoot + "/build";
    public const string LibraryZipResourceUri = LibraryPackageSkillsRoot + "/zip";
    public const string LibraryMetadataResourceUri = LibraryPackageSkillsRoot + "/metadata";
    public const string LibraryImportResourceUri = LibraryPackageSkillsRoot + "/import";
    public const string LibraryUpgradeResourceUri = LibraryPackageSkillsRoot + "/upgrade";

    public static IReadOnlyList<McpAiGuidanceResource> ModuleResources { get; } =
    [
        new("sereinflow.projects", SereinFlowProjectsResourceUri, "sereinflow-projects", "Project discovery and read-only project inspection", "mcp/sereinflow-projects-skill.md"),
        new("sereinflow.flows", SereinFlowFlowsResourceUri, "sereinflow-flows", "Flow editing and patch contract rules", "mcp/sereinflow-flows-skill.md"),
        new("sereinflow.ui-ux", SereinFlowUiUxResourceUri, "sereinflow-ui-ux", "Flow canvas layout and visual organization guidance", "mcp/sereinflow-ui-ux-skill.md"),
        new("sereinflow.runtime", SereinFlowRuntimeResourceUri, "sereinflow-runtime", "Run, debug and post-change verification rules", "mcp/sereinflow-runtime-skill.md"),
        new("sereinflow.workpieces", SereinFlowWorkpiecesResourceUri, "sereinflow-workpieces", "Run workpiece inspection, image preview and file download rules", "mcp/sereinflow-workpieces-skill.md"),
        new("sereinflow.api-keys", SereinFlowApiKeysResourceUri, "sereinflow-api-keys", "MCP API key lifecycle and secret handling rules", "mcp/sereinflow-api-keys-skill.md"),
        new("sereinflow.release", SereinFlowReleaseResourceUri, "sereinflow-release", "Publishing and rollback rules", "mcp/sereinflow-release-skill.md"),
        new("sereinlang.syntax", SereinLangSyntaxResourceUri, "sereinlang-syntax", "SereinLang lexical and expression rules", "mcp/sereinlang-syntax-skill.md"),
        new("sereinlang.host", SereinLangHostResourceUri, "sereinlang-host", "SereinLang imports and host interoperation", "mcp/sereinlang-host-skill.md"),
        new("sereinlang.grammar", SereinLangGrammarResourceUri, "sereinlang-grammar", "SereinLang formal grammar reference", "mcp/sereinlang-grammar-skill.md"),
        new(LibraryErrorCodes.Build, LibraryBuildResourceUri, "library-build", "Local C# library build and publish boundary", "mcp/sereinflow-library-build-skill.md"),
        new(LibraryErrorCodes.Zip, LibraryZipResourceUri, "library-zip", "SereinFlow library ZIP contract", "mcp/sereinflow-library-zip-skill.md"),
        new(LibraryErrorCodes.Metadata, LibraryMetadataResourceUri, "library-metadata", "SereinFlow library metadata contract", "mcp/sereinflow-library-metadata-skill.md"),
        new(LibraryErrorCodes.Import, LibraryImportResourceUri, "library-import", "Library package preview, import and attachment", "mcp/sereinflow-library-import-skill.md"),
        new("library.upgrade", LibraryUpgradeResourceUri, "library-upgrade", "Library families and project upgrade workflow", "mcp/sereinflow-library-upgrade-skill.md")
    ];

    public static IReadOnlyList<McpAiGuidanceResource> AllResources { get; } =
    [
        new("ai.guide", ResourceUri, ResourceName, "Compact capability index for SereinFlow AI guidance", "mcp/sereinflow-ai-guide.md"),
        new("sereinflow", SereinFlowResourceUri, "sereinflow", "SereinFlow capability index", "mcp/sereinflow-skill.md"),
        new("sereinlang", SereinLangResourceUri, "sereinlang", "SereinLang capability index", "mcp/sereinlang-skill.md"),
        new("sereinflow-library-package", LibraryPackageResourceUri, "sereinflow-library-package", "Library package capability index", "mcp/sereinflow-library-package-skill.md"),
        ..ModuleResources
    ];

    private static Dictionary<string, McpAiGuidanceResource> ResourcesByUri { get; } =
        AllResources.ToDictionary(static resource => resource.Uri, StringComparer.Ordinal);

    private static Dictionary<McpAiGuidanceId, string> UrisById { get; } =
        new Dictionary<McpAiGuidanceId, string>
        {
            [McpAiGuidanceId.Guide] = ResourceUri,
            [McpAiGuidanceId.SereinFlow] = SereinFlowResourceUri,
            [McpAiGuidanceId.SereinLang] = SereinLangResourceUri,
            [McpAiGuidanceId.LibraryPackage] = LibraryPackageResourceUri,
            [McpAiGuidanceId.SereinFlowProjects] = SereinFlowProjectsResourceUri,
            [McpAiGuidanceId.SereinFlowFlows] = SereinFlowFlowsResourceUri,
            [McpAiGuidanceId.SereinFlowUiUx] = SereinFlowUiUxResourceUri,
            [McpAiGuidanceId.SereinFlowRuntime] = SereinFlowRuntimeResourceUri,
            [McpAiGuidanceId.SereinFlowWorkpieces] = SereinFlowWorkpiecesResourceUri,
            [McpAiGuidanceId.SereinFlowApiKeys] = SereinFlowApiKeysResourceUri,
            [McpAiGuidanceId.SereinFlowRelease] = SereinFlowReleaseResourceUri,
            [McpAiGuidanceId.SereinLangSyntax] = SereinLangSyntaxResourceUri,
            [McpAiGuidanceId.SereinLangHost] = SereinLangHostResourceUri,
            [McpAiGuidanceId.SereinLangGrammar] = SereinLangGrammarResourceUri,
            [McpAiGuidanceId.LibraryBuild] = LibraryBuildResourceUri,
            [McpAiGuidanceId.LibraryZip] = LibraryZipResourceUri,
            [McpAiGuidanceId.LibraryMetadata] = LibraryMetadataResourceUri,
            [McpAiGuidanceId.LibraryImport] = LibraryImportResourceUri,
            [McpAiGuidanceId.LibraryUpgrade] = LibraryUpgradeResourceUri
        };

    public static string GetUri(McpAiGuidanceId id)
        => UrisById[id];

    public static McpAiGuidanceResource GetResource(McpAiGuidanceId id)
        => ResourcesByUri[GetUri(id)];

    public static bool IsGuidanceUri(string uri)
        => ResourcesByUri.ContainsKey(uri);
}

public sealed record McpAiGuidanceResourceOverride(
    string? Uri,
    string? FilePath);

public sealed class McpAiGuidanceOptions
{
    public const string SectionName = "SereinFlow:Mcp:AiGuidance";

    public string FilePath { get; init; } = "mcp/sereinflow-ai-guide.md";

    public string SereinFlowFilePath { get; init; } = "mcp/sereinflow-skill.md";

    public string SereinLangFilePath { get; init; } = "mcp/sereinlang-skill.md";

    public string LibraryPackageFilePath { get; init; } = "mcp/sereinflow-library-package-skill.md";

    public IReadOnlyDictionary<string, string> ModuleFilePaths { get; init; } =
        McpAiGuidance.ModuleResources.ToDictionary(
            static resource => resource.Key,
            static resource => resource.DefaultFilePath,
            StringComparer.Ordinal);

    public IReadOnlyDictionary<string, McpAiGuidanceResourceOverride> ResourceOverrides { get; init; } =
        new Dictionary<string, McpAiGuidanceResourceOverride>(StringComparer.Ordinal);

    public long MaxBytes { get; init; } = 512 * 1024;

    public static McpAiGuidanceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var filePath = configuration[$"{SectionName}:FilePath"]
            ?? configuration["SereinFlow:Mcp:AiGuidanceFile"]
            ?? configuration["SereinFlow:Mcp:SkillFile"]
            ?? "mcp/sereinflow-ai-guide.md";
        var sereinFlowFilePath = configuration[$"{SectionName}:SereinFlowFilePath"]
            ?? "mcp/sereinflow-skill.md";
        var sereinLangFilePath = configuration[$"{SectionName}:SereinLangFilePath"]
            ?? "mcp/sereinlang-skill.md";
        var libraryPackageFilePath = configuration[$"{SectionName}:LibraryPackageFilePath"]
            ?? "mcp/sereinflow-library-package-skill.md";
        var moduleFilePaths = McpAiGuidance.ModuleResources.ToDictionary(
            static resource => resource.Key,
            static resource => resource.DefaultFilePath,
            StringComparer.Ordinal);
        foreach (var child in configuration.GetSection($"{SectionName}:Modules").GetChildren())
        {
            var resource = McpAiGuidance.ModuleResources.FirstOrDefault(
                resource => string.Equals(resource.Key, child.Key, StringComparison.Ordinal));
            if (resource is null)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Modules contains unsupported resource key '{child.Key}'.");
            }

            moduleFilePaths[resource.Key] = child.Value ?? string.Empty;
        }
        var resourceOverrides = new Dictionary<string, McpAiGuidanceResourceOverride>(StringComparer.Ordinal);
        foreach (var child in configuration.GetSection($"{SectionName}:Resources").GetChildren())
        {
            if (!McpAiGuidance.AllResources.Any(resource => string.Equals(resource.Key, child.Key, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Resources contains unsupported resource key '{child.Key}'.");
            }

            var uri = child["Uri"]?.Trim();
            var resourceFilePath = child["FilePath"]?.Trim();
            if (string.IsNullOrWhiteSpace(uri) && string.IsNullOrWhiteSpace(resourceFilePath))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Resources:{child.Key} must configure Uri, FilePath, or both.");
            }
            if (!string.IsNullOrWhiteSpace(uri))
                ValidateResourceUri(uri);
            if (!string.IsNullOrWhiteSpace(resourceFilePath))
                ValidateRelativeFilePath(resourceFilePath);
            resourceOverrides[child.Key] = new McpAiGuidanceResourceOverride(uri, resourceFilePath);
        }
        var maxBytes = long.TryParse(configuration[$"{SectionName}:MaxBytes"], out var parsed)
            && parsed > 0
            ? parsed
            : 512 * 1024;

        ValidateRelativeFilePath(filePath);
        ValidateRelativeFilePath(sereinFlowFilePath);
        ValidateRelativeFilePath(sereinLangFilePath);
        ValidateRelativeFilePath(libraryPackageFilePath);
        foreach (var moduleFilePath in moduleFilePaths.Values)
            ValidateRelativeFilePath(moduleFilePath);

        return new McpAiGuidanceOptions
        {
            FilePath = filePath.Trim(),
            SereinFlowFilePath = sereinFlowFilePath.Trim(),
            SereinLangFilePath = sereinLangFilePath.Trim(),
            LibraryPackageFilePath = libraryPackageFilePath.Trim(),
            ModuleFilePaths = moduleFilePaths.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Trim(),
                StringComparer.Ordinal),
            ResourceOverrides = resourceOverrides,
            MaxBytes = maxBytes
        };
    }

    internal static void ValidateRelativeFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || Path.IsPathRooted(filePath)
            || filePath is "." or ".."
            || filePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(static segment => segment is "." or ".."))
        {
            throw new InvalidOperationException(
                $"{SectionName}:FilePath must be a non-empty relative path without '.' or '..' segments.");
        }
    }

    internal static void ValidateResourceUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || !string.Equals(parsed.Scheme, McpResourceUris.Scheme, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(parsed.Host)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment)
            || uri.IndexOfAny(['{', '}']) >= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Resources URI must be an absolute sereinflow URI without templates, query, or fragment.");
        }
    }
}

/// <summary>
/// Resolves MCP guidance from the current configuration and server deployment
/// at read time. Configuration controls the public URI and backing file path;
/// the MCP caller can select only a currently registered URI.
/// </summary>
public sealed class McpAiGuidanceProvider
{
    private readonly Func<McpAiGuidanceOptions> _optionsAccessor;
    private readonly string _root;
    private readonly string _rootPrefix;

    public McpAiGuidanceProvider(McpAiGuidanceOptions options, string contentRootPath)
        : this(() => options ?? throw new ArgumentNullException(nameof(options)), contentRootPath)
    {
    }

    public McpAiGuidanceProvider(IConfiguration configuration, string contentRootPath)
        : this(() => McpAiGuidanceOptions.FromConfiguration(
            configuration ?? throw new ArgumentNullException(nameof(configuration))), contentRootPath)
    {
    }

    private McpAiGuidanceProvider(Func<McpAiGuidanceOptions> optionsAccessor, string contentRootPath)
    {
        _optionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        _root = Path.GetFullPath(contentRootPath);
        _rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        _ = CreateSnapshot();
    }

    public IReadOnlyList<McpResourceDescriptor> GetResourceDescriptors()
    {
        var snapshot = CreateSnapshot();
        return Enum.GetValues<McpAiGuidanceId>()
            .Select(id => snapshot.ById[id])
            .Select(static resource => new McpResourceDescriptor(
                resource.Resource.Uri,
                resource.Resource.Name,
                resource.Resource.Description,
                McpAiGuidance.MimeType))
            .ToArray();
    }

    public string GetUri(McpAiGuidanceId id)
        => CreateSnapshot().ById[id].Resource.Uri;

    public bool IsGuidanceUri(string uri)
        => CreateSnapshot().ByUri.ContainsKey(uri);

    public async Task<McpResourceReadResult> ReadAsync(CancellationToken cancellationToken)
        => await ReadAsync(McpAiGuidanceId.Guide, cancellationToken);

    public async Task<McpResourceReadResult> ReadAsync(McpAiGuidanceId id, CancellationToken cancellationToken)
    {
        var snapshot = CreateSnapshot();
        return await ReadAsync(snapshot.ById[id], snapshot.MaxBytes, cancellationToken);
    }

    public async Task<McpResourceReadResult> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        var snapshot = CreateSnapshot();
        if (!snapshot.ByUri.TryGetValue(uri, out var resource))
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.InvalidParams,
                "The SereinFlow AI guidance URI is not supported.",
                new { code = McpErrorCodes.AiGuidanceUriUnsupported });
        }

        return await ReadAsync(resource, snapshot.MaxBytes, cancellationToken);
    }

    private GuidanceSnapshot CreateSnapshot()
    {
        var options = _optionsAccessor();
        var byId = new Dictionary<McpAiGuidanceId, ResolvedGuidanceResource>();
        var byUri = new Dictionary<string, ResolvedGuidanceResource>(StringComparer.Ordinal);
        foreach (var id in Enum.GetValues<McpAiGuidanceId>())
        {
            var resource = McpAiGuidance.GetResource(id);
            var relativePath = GetConfiguredFilePath(id, resource, options);
            var uri = resource.Uri;
            if (options.ResourceOverrides.TryGetValue(resource.Key, out var resourceOverride))
            {
                uri = string.IsNullOrWhiteSpace(resourceOverride.Uri) ? uri : resourceOverride.Uri.Trim();
                relativePath = string.IsNullOrWhiteSpace(resourceOverride.FilePath)
                    ? relativePath
                    : resourceOverride.FilePath.Trim();
            }

            McpAiGuidanceOptions.ValidateResourceUri(uri);
            McpAiGuidanceOptions.ValidateRelativeFilePath(relativePath);
            var path = Path.GetFullPath(Path.Combine(_root, relativePath));
            if (!path.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{McpAiGuidanceOptions.SectionName}:FilePath must remain inside the server content root.");
            }

            var resolved = new ResolvedGuidanceResource(resource with { Uri = uri }, path);
            if (McpResourceCatalog.DirectResources.Any(
                    direct => string.Equals(direct.UriTemplate, uri, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"{McpAiGuidanceOptions.SectionName}:Resources URI '{uri}' conflicts with a direct MCP resource.");
            }
            if (!byUri.TryAdd(uri, resolved))
                throw new InvalidOperationException($"{McpAiGuidanceOptions.SectionName}:Resources contains duplicate URI '{uri}'.");
            byId[id] = resolved;
        }

        var maxBytes = options.MaxBytes > 0
            ? options.MaxBytes
            : throw new ArgumentOutOfRangeException(nameof(options), "The AI guidance size limit must be positive.");
        return new GuidanceSnapshot(byId, byUri, maxBytes);
    }

    private static string GetConfiguredFilePath(
        McpAiGuidanceId id,
        McpAiGuidanceResource resource,
        McpAiGuidanceOptions options)
        => id switch
        {
            McpAiGuidanceId.Guide => options.FilePath,
            McpAiGuidanceId.SereinFlow => options.SereinFlowFilePath,
            McpAiGuidanceId.SereinLang => options.SereinLangFilePath,
            McpAiGuidanceId.LibraryPackage => options.LibraryPackageFilePath,
            _ => options.ModuleFilePaths.TryGetValue(resource.Key, out var configuredPath)
                ? configuredPath
                : resource.DefaultFilePath
        };

    private static async Task<McpResourceReadResult> ReadAsync(
        ResolvedGuidanceResource resource,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        string content;
        try
        {
            await using var stream = new FileStream(resource.FilePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

            if (stream.Length > maxBytes)
                throw new McpProtocolException(
                    McpProtocolErrorCodes.RequestTooLarge,
                    "The configured SereinFlow AI guidance is too large.",
                    new { code = McpErrorCodes.AiGuidanceTooLarge });

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 8192,
                leaveOpen: false);
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (McpProtocolException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw Unavailable();
        }
        catch (DirectoryNotFoundException)
        {
            throw Unavailable();
        }
        catch (UnauthorizedAccessException)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.PermissionDenied,
                "The configured SereinFlow AI guidance cannot be read.",
                new { code = McpErrorCodes.AiGuidanceAccessDenied });
        }
        catch (IOException)
        {
            throw Unavailable();
        }

        if (Encoding.UTF8.GetByteCount(content) > maxBytes)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.RequestTooLarge,
                "The configured SereinFlow AI guidance is too large.",
                new { code = McpErrorCodes.AiGuidanceTooLarge });
        }

        if (string.IsNullOrWhiteSpace(content))
            throw Unavailable();

        return new McpResourceReadResult(
            resource.Resource.Uri,
            content,
            McpAiGuidance.MimeType);
    }

    private sealed record ResolvedGuidanceResource(McpAiGuidanceResource Resource, string FilePath);

    private sealed record GuidanceSnapshot(
        Dictionary<McpAiGuidanceId, ResolvedGuidanceResource> ById,
        Dictionary<string, ResolvedGuidanceResource> ByUri,
        long MaxBytes);

    private static McpProtocolException Unavailable()
        => new(
            McpProtocolErrorCodes.ResourceNotFound,
            "The SereinFlow AI guidance is not available.",
            new { code = McpErrorCodes.AiGuidanceUnavailable });
}
