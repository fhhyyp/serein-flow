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

public static class McpAiGuidance
{
    public const string ResourceUri = "sereinflow://ai/guide";
    public const string ResourceName = "ai-guide";
    public const string SereinFlowResourceUri = "sereinflow://ai/skills/sereinflow";
    public const string SereinLangResourceUri = "sereinflow://ai/skills/sereinlang";
    public const string LibraryPackageResourceUri = "sereinflow://ai/skills/sereinflow-library-package";
    public const string MimeType = "text/markdown";

    public const string SereinFlowProjectsResourceUri = "sereinflow://ai/skills/sereinflow/projects";
    public const string SereinFlowFlowsResourceUri = "sereinflow://ai/skills/sereinflow/flows";
    public const string SereinFlowRuntimeResourceUri = "sereinflow://ai/skills/sereinflow/runtime";
    public const string SereinFlowWorkpiecesResourceUri = "sereinflow://ai/skills/sereinflow/workpieces";
    public const string SereinFlowApiKeysResourceUri = "sereinflow://ai/skills/sereinflow/api-keys";
    public const string SereinFlowReleaseResourceUri = "sereinflow://ai/skills/sereinflow/release";
    public const string SereinLangSyntaxResourceUri = "sereinflow://ai/skills/sereinlang/syntax";
    public const string SereinLangHostResourceUri = "sereinflow://ai/skills/sereinlang/host";
    public const string SereinLangGrammarResourceUri = "sereinflow://ai/skills/sereinlang/grammar";
    public const string LibraryBuildResourceUri = "sereinflow://ai/skills/sereinflow-library-package/build";
    public const string LibraryZipResourceUri = "sereinflow://ai/skills/sereinflow-library-package/zip";
    public const string LibraryMetadataResourceUri = "sereinflow://ai/skills/sereinflow-library-package/metadata";
    public const string LibraryImportResourceUri = "sereinflow://ai/skills/sereinflow-library-package/import";
    public const string LibraryUpgradeResourceUri = "sereinflow://ai/skills/sereinflow-library-package/upgrade";

    public static IReadOnlyList<McpAiGuidanceResource> ModuleResources { get; } =
    [
        new("sereinflow.projects", SereinFlowProjectsResourceUri, "sereinflow-projects", "Project discovery and read-only project inspection", "mcp/sereinflow-projects-skill.md"),
        new("sereinflow.flows", SereinFlowFlowsResourceUri, "sereinflow-flows", "Flow editing, patch and layout rules", "mcp/sereinflow-flows-skill.md"),
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

    public static bool IsGuidanceUri(string uri)
        => (uri is ResourceUri
            or SereinFlowResourceUri
            or SereinLangResourceUri
            or LibraryPackageResourceUri)
            || ModuleResources.Any(resource => string.Equals(resource.Uri, uri, StringComparison.Ordinal));
}

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
}

/// <summary>
/// Loads the MCP guidance from the server deployment at read time. The MCP
/// caller can select the URI, but never the backing file path.
/// </summary>
public sealed class McpAiGuidanceProvider
{
    private readonly Dictionary<string, string> _filePaths;
    private readonly long _maxBytes;

    public McpAiGuidanceProvider(McpAiGuidanceOptions options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        var root = Path.GetFullPath(contentRootPath);
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var configuredPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [McpAiGuidance.ResourceUri] = options.FilePath,
            [McpAiGuidance.SereinFlowResourceUri] = options.SereinFlowFilePath,
            [McpAiGuidance.SereinLangResourceUri] = options.SereinLangFilePath,
            [McpAiGuidance.LibraryPackageResourceUri] = options.LibraryPackageFilePath
        };
        foreach (var resource in McpAiGuidance.ModuleResources)
        {
            var relativePath = options.ModuleFilePaths.TryGetValue(resource.Key, out var configuredPath)
                ? configuredPath
                : resource.DefaultFilePath;
            configuredPaths[resource.Uri] = relativePath;
        }
        var resolvedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (uri, relativePath) in configuredPaths)
        {
            McpAiGuidanceOptions.ValidateRelativeFilePath(relativePath);
            var path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{McpAiGuidanceOptions.SectionName}:FilePath must remain inside the server content root.");
            }

            resolvedPaths[uri] = path;
        }

        _filePaths = resolvedPaths;
        _maxBytes = options.MaxBytes > 0
            ? options.MaxBytes
            : throw new ArgumentOutOfRangeException(nameof(options), "The AI guidance size limit must be positive.");
    }

    public async Task<McpResourceReadResult> ReadAsync(CancellationToken cancellationToken)
        => await ReadAsync(McpAiGuidance.ResourceUri, cancellationToken);

    public async Task<McpResourceReadResult> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (!_filePaths.TryGetValue(uri, out var filePath))
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.InvalidParams,
                "The SereinFlow AI guidance URI is not supported.",
                new { code = McpErrorCodes.AiGuidanceUriUnsupported });
        }

        string content;
        try
        {
            await using var stream = new FileStream(filePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

            if (stream.Length > _maxBytes)
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

        if (Encoding.UTF8.GetByteCount(content) > _maxBytes)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.RequestTooLarge,
                "The configured SereinFlow AI guidance is too large.",
                new { code = McpErrorCodes.AiGuidanceTooLarge });
        }

        if (string.IsNullOrWhiteSpace(content))
            throw Unavailable();

        return new McpResourceReadResult(
            uri,
            content,
            McpAiGuidance.MimeType);
    }

    private static McpProtocolException Unavailable()
        => new(
            McpProtocolErrorCodes.ResourceNotFound,
            "The SereinFlow AI guidance is not available.",
            new { code = McpErrorCodes.AiGuidanceUnavailable });
}
