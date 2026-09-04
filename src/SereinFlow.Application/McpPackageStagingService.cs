using System.Security.Cryptography;

namespace SereinFlow.Application;

public sealed record McpStagedPackage(string Path, long SizeBytes, string Sha256);

/// <summary>
/// Stores package bytes only in a generated, bounded temporary location while
/// a preview is pending. The path is never accepted from a client.
/// 预览待确认期间仅将包字节保存到受限临时目录；路径由服务生成，客户端不能提交路径。
/// </summary>
public sealed class McpPackageStagingService : IDisposable
{
    private readonly string _rootPath;
    private readonly long _maxBytes;
    private readonly TimeSpan _maxAge;
    private readonly IFileUploadSettings? _fileUploadSettings;

    public McpPackageStagingService(
        string rootPath,
        long maxBytes = FileUploadLimits.DefaultMaxFileSizeBytes,
        TimeSpan? maxAge = null,
        IFileUploadSettings? fileUploadSettings = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("The MCP package staging root is required.", nameof(rootPath));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        _rootPath = Path.GetFullPath(rootPath);
        _maxBytes = maxBytes;
        _maxAge = maxAge ?? TimeSpan.FromMinutes(30);
        _fileUploadSettings = fileUploadSettings;
        if (_maxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        Directory.CreateDirectory(_rootPath);
        CleanupStaleFiles();
    }

    public async Task<McpStagedPackage> StageAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        CleanupStaleFiles();
        var maxBytes = _fileUploadSettings?.MaxLibraryUploadBytes ?? _maxBytes;
        var path = Path.Combine(_rootPath, $"mcp-{Guid.NewGuid():N}.zip");
        long size = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using var destination = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                    break;
                size += read;
                if (size > maxBytes)
                    throw new InvalidOperationException("The staged MCP package exceeds the configured size limit.");
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await destination.FlushAsync(cancellationToken);
            return new McpStagedPackage(path, size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            Delete(path);
            throw;
        }
    }

    public FileStream OpenRead(string path)
    {
        if (!IsOwnedPath(path) || !File.Exists(path))
            throw new FileNotFoundException("The staged MCP package is no longer available.");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
    }

    public bool IsOwnedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.TrimEndingDirectorySeparator(_rootPath) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(fullPath).StartsWith("mcp-", StringComparison.Ordinal)
                && string.Equals(Path.GetExtension(fullPath), ".zip", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public void Delete(string path)
    {
        if (!IsOwnedPath(path))
            return;
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    public void Dispose()
    {
    }

    private void CleanupStaleFiles()
    {
        var cutoff = DateTime.UtcNow - _maxAge;
        try
        {
            foreach (var path in Directory.EnumerateFiles(_rootPath, "mcp-*.zip", SearchOption.TopDirectoryOnly))
            {
                if (!IsOwnedPath(path))
                    continue;
                DateTime lastWrite;
                try
                {
                    lastWrite = File.GetLastWriteTimeUtc(path);
                }
                catch (IOException)
                {
                    continue;
                }

                if (lastWrite < cutoff)
                    Delete(path);
            }
        }
        catch (DirectoryNotFoundException)
        {
            Directory.CreateDirectory(_rootPath);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
