using System.Security.Cryptography;
using System.Text;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpAiGuidanceProvider;

namespace SereinFlow.Mcp;

/// <summary>Manages disk overrides for the same registered resources served by MCP.</summary>
public sealed class McpAiGuidanceManagementService(McpAiGuidanceProvider provider) : IDisposable
{
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public IReadOnlyList<McpAiGuidanceResourceDto> List()
        => provider.CreateSnapshot().ById.Values
            .Select(resource => ToResourceDto(resource, File.Exists(resource.FilePath)
                ? McpAiGuidanceSourceDto.Disk
                : McpAiGuidanceSourceDto.Embedded))
            .ToArray();

    public async Task<McpAiGuidanceContentDto> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var snapshot = provider.CreateSnapshot();
        return await ReadAsync(Resolve(snapshot, key), snapshot.MaxBytes, cancellationToken);
    }

    public async Task<McpAiGuidanceContentDto> SaveAsync(
        string key,
        SaveMcpAiGuidanceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _mutationLock.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var snapshot = provider.CreateSnapshot();
            var resource = Resolve(snapshot, key);
            ValidateContent(request.Content, snapshot.MaxBytes);
            RequireRevision(request.ExpectedRevision, await ReadAsync(resource, snapshot.MaxBytes, cancellationToken));
            EnsureWritablePath(resource.FilePath);
            var directory = Path.GetDirectoryName(resource.FilePath)!;
            Directory.CreateDirectory(directory);
            EnsureWritablePath(resource.FilePath);
            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(resource.FilePath)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporaryPath, request.Content, new UTF8Encoding(false), cancellationToken);

            // Configuration and external file edits may change while the temporary file is written.
            var latest = provider.CreateSnapshot();
            RequireRevision(request.ExpectedRevision,
                await ReadAsync(Resolve(latest, key), latest.MaxBytes, cancellationToken));
            ValidateContent(request.Content, latest.MaxBytes);
            EnsureWritablePath(resource.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, resource.FilePath, overwrite: true);
            temporaryPath = null;
            return ToContentDto(resource, new GuidanceContent(request.Content, McpAiGuidanceSourceDto.Disk), latest.MaxBytes);
        }
        catch (UnauthorizedAccessException)
        {
            throw AccessDenied();
        }
        catch (IOException)
        {
            throw SaveFailed();
        }
        finally
        {
            try
            {
                if (temporaryPath is not null)
                    File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            _mutationLock.Release();
        }
    }

    public async Task<McpAiGuidanceContentDto> ResetAsync(
        string key,
        ResetMcpAiGuidanceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = provider.CreateSnapshot();
            var resource = Resolve(snapshot, key);
            RequireRevision(request.ExpectedRevision, await ReadAsync(resource, snapshot.MaxBytes, cancellationToken));
            var content = await ReadContentAsync(resource, snapshot.MaxBytes, embeddedOnly: true, cancellationToken);
            var latest = provider.CreateSnapshot();
            RequireRevision(request.ExpectedRevision,
                await ReadAsync(Resolve(latest, key), latest.MaxBytes, cancellationToken));
            ValidateContent(content.Text, latest.MaxBytes);
            EnsureWritablePath(resource.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(resource.FilePath))
                File.Delete(resource.FilePath);
            return ToContentDto(resource, content, latest.MaxBytes);
        }
        catch (UnauthorizedAccessException)
        {
            throw AccessDenied();
        }
        catch (IOException)
        {
            throw SaveFailed();
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private static ResolvedGuidanceResource Resolve(GuidanceSnapshot snapshot, string key)
        => snapshot.ById.Values.FirstOrDefault(resource => string.Equals(resource.Resource.Key, key, StringComparison.Ordinal))
            ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound,
                "The MCP skill is not registered.", new { code = McpErrorCodes.AiGuidanceKeyUnsupported });

    private static async Task<McpAiGuidanceContentDto> ReadAsync(
        ResolvedGuidanceResource resource, long maxBytes, CancellationToken cancellationToken)
        => ToContentDto(resource, await ReadContentAsync(resource, maxBytes, embeddedOnly: false, cancellationToken), maxBytes);

    private static McpAiGuidanceResourceDto ToResourceDto(ResolvedGuidanceResource resource, McpAiGuidanceSourceDto source)
        => new(resource.Resource.Key, resource.Resource.Uri, resource.Resource.Name, resource.Resource.Description, source);

    private static McpAiGuidanceContentDto ToContentDto(ResolvedGuidanceResource resource, GuidanceContent content, long maxBytes)
    {
        var identity = $"{resource.Resource.Key}\0{resource.Resource.Uri}\0{resource.FilePath}\0{content.Source}\0{content.Text}";
        var revision = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new(ToResourceDto(resource, content.Source), content.Text, revision, maxBytes);
    }

    private static void RequireRevision(string expectedRevision, McpAiGuidanceContentDto current)
    {
        if (string.IsNullOrWhiteSpace(expectedRevision))
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams,
                "The skill revision is required.", new { code = McpErrorCodes.InvalidArguments });
        if (!string.Equals(expectedRevision, current.Revision, StringComparison.Ordinal))
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict,
                "The skill has changed. Reload it before saving or restoring defaults.", new { code = McpErrorCodes.AiGuidanceConflict });
    }

    private static void ValidateContent(string content, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams,
                "Skill content must not be empty.", new { code = McpErrorCodes.AiGuidanceContentInvalid });
        if (Encoding.UTF8.GetByteCount(content) > maxBytes)
            throw new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge,
                "Skill content exceeds the configured size limit.", new { code = McpErrorCodes.AiGuidanceTooLarge });
    }

    private void EnsureWritablePath(string filePath)
    {
        // Configured paths are already validated by the provider. Reject links as well:
        // a directory junction or file symlink must not redirect a write outside the root.
        var root = provider.ContentRootPath;
        var relativePath = Path.GetRelativePath(root, filePath);
        var current = root;
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw AccessDenied();
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static McpProtocolException AccessDenied()
        => new(McpProtocolErrorCodes.PermissionDenied, "The MCP skill override cannot be modified.",
            new { code = McpErrorCodes.AiGuidanceAccessDenied });

    private static McpProtocolException SaveFailed()
        => new(McpProtocolErrorCodes.TransientFailure, "The MCP skill override could not be saved. Check the server directory and retry.",
            new { code = McpErrorCodes.AiGuidanceSaveFailed });

    public void Dispose() => _mutationLock.Dispose();
}
