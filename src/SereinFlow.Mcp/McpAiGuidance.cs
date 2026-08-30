using System.Text;
using Microsoft.Extensions.Configuration;

namespace SereinFlow.Mcp;

public static class McpAiGuidance
{
    public const string ResourceUri = "sereinflow://ai/guide";
    public const string ResourceName = "ai-guide";
    public const string SereinFlowResourceUri = "sereinflow://ai/skills/sereinflow";
    public const string SereinLangResourceUri = "sereinflow://ai/skills/sereinlang";
    public const string LibraryPackageResourceUri = "sereinflow://ai/skills/sereinflow-library-package";
    public const string MimeType = "text/markdown";

    public static bool IsGuidanceUri(string uri)
        => uri is ResourceUri
            or SereinFlowResourceUri
            or SereinLangResourceUri
            or LibraryPackageResourceUri;
}

public sealed class McpAiGuidanceOptions
{
    public const string SectionName = "SereinFlow:Mcp:AiGuidance";

    public string FilePath { get; init; } = "mcp/sereinflow-ai-guide.md";

    public string SereinFlowFilePath { get; init; } = "mcp/sereinflow-skill.md";

    public string SereinLangFilePath { get; init; } = "mcp/sereinlang-skill.md";

    public string LibraryPackageFilePath { get; init; } = "mcp/sereinflow-library-package-skill.md";

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
        var maxBytes = long.TryParse(configuration[$"{SectionName}:MaxBytes"], out var parsed)
            && parsed > 0
            ? parsed
            : 512 * 1024;

        ValidateRelativeFilePath(filePath);
        ValidateRelativeFilePath(sereinFlowFilePath);
        ValidateRelativeFilePath(sereinLangFilePath);
        ValidateRelativeFilePath(libraryPackageFilePath);
        return new McpAiGuidanceOptions
        {
            FilePath = filePath.Trim(),
            SereinFlowFilePath = sereinFlowFilePath.Trim(),
            SereinLangFilePath = sereinLangFilePath.Trim(),
            LibraryPackageFilePath = libraryPackageFilePath.Trim(),
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
                -32602,
                "The SereinFlow AI guidance URI is not supported.",
                new { code = "mcp.ai_guidance_uri_unsupported" });
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
                    -32012,
                    "The configured SereinFlow AI guidance is too large.",
                    new { code = "mcp.ai_guidance_too_large" });

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
                -32003,
                "The configured SereinFlow AI guidance cannot be read.",
                new { code = "mcp.ai_guidance_access_denied" });
        }
        catch (IOException)
        {
            throw Unavailable();
        }

        if (Encoding.UTF8.GetByteCount(content) > _maxBytes)
        {
            throw new McpProtocolException(
                -32012,
                "The configured SereinFlow AI guidance is too large.",
                new { code = "mcp.ai_guidance_too_large" });
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
            -32004,
            "The SereinFlow AI guidance is not available.",
            new { code = "mcp.ai_guidance_unavailable" });
}
