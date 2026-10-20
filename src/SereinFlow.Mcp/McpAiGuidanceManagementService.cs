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
    {
        var evaluatedValue1 = provider.CreateSnapshot().ById.Values;
        var selectResult = evaluatedValue1.Select(resource =>
        {
            McpAiGuidanceSourceDto evaluatedValue = File.Exists(resource.FilePath) ? McpAiGuidanceSourceDto.Disk : McpAiGuidanceSourceDto.Embedded;
            var toResourceDtoResult = ToResourceDto(resource, evaluatedValue);
            return toResourceDtoResult;
        });
        var toArrayResult = selectResult.ToArray();
        return toArrayResult;
    }

    public async Task<McpAiGuidanceContentDto> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var snapshot = provider.CreateSnapshot();
        var resolveResult = Resolve(snapshot, key);
        var readAsyncResult = await ReadAsync(resolveResult, snapshot.MaxBytes, cancellationToken);
        return readAsyncResult;
    }

    public async Task<McpAiGuidanceContentDto> SaveAsync(string key, SaveMcpAiGuidanceRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _mutationLock.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var snapshot = provider.CreateSnapshot();
            var resource = Resolve(snapshot, key);
            ValidateContent(request.Content, snapshot.MaxBytes);
            var readAsyncResult2 = await ReadAsync(resource, snapshot.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, readAsyncResult2);
            EnsureWritablePath(resource.FilePath);
            var directory = Path.GetDirectoryName(resource.FilePath)!;
            Directory.CreateDirectory(directory);
            EnsureWritablePath(resource.FilePath);
            var evaluatedValue3 = $".{Path.GetFileName(resource.FilePath)}.{Guid.NewGuid():N}.tmp";
            temporaryPath = Path.Combine(directory, evaluatedValue3);
            var uTF8Encoding = new UTF8Encoding(false);
            await File.WriteAllTextAsync(temporaryPath, request.Content, uTF8Encoding, cancellationToken);
            // Configuration and external file edits may change while the temporary file is written.
            var latest = provider.CreateSnapshot();
            var resolveResult4 = Resolve(latest, key);
            var readAsyncResult5 = await ReadAsync(resolveResult4, latest.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, readAsyncResult5);
            ValidateContent(request.Content, latest.MaxBytes);
            EnsureWritablePath(resource.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, resource.FilePath, overwrite: true);
            temporaryPath = null;
            var guidanceContent = new GuidanceContent(request.Content, McpAiGuidanceSourceDto.Disk);
            var toContentDtoResult = ToContentDto(resource, guidanceContent, latest.MaxBytes);
            return toContentDtoResult;
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
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _mutationLock.Release();
        }
    }

    public async Task<McpAiGuidanceContentDto> ResetAsync(string key, ResetMcpAiGuidanceRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = provider.CreateSnapshot();
            var resource = Resolve(snapshot, key);
            var readAsyncResult6 = await ReadAsync(resource, snapshot.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, readAsyncResult6);
            var content = await ReadContentAsync(resource, snapshot.MaxBytes, embeddedOnly: true, cancellationToken);
            var latest = provider.CreateSnapshot();
            var resolveResult7 = Resolve(latest, key);
            var readAsyncResult8 = await ReadAsync(resolveResult7, latest.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, readAsyncResult8);
            ValidateContent(content.Text, latest.MaxBytes);
            EnsureWritablePath(resource.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(resource.FilePath))
            {
                File.Delete(resource.FilePath);
            }

            var toContentDtoResult9 = ToContentDto(resource, content, latest.MaxBytes);
            return toContentDtoResult9;
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
    {
        var evaluatedValue10 = snapshot.ById.Values.FirstOrDefault(resource =>
        {
            var equalsResult = string.Equals(resource.Resource.Key, key, StringComparison.Ordinal);
            return equalsResult;
        }) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP skill is not registered.", new { code = McpErrorCodes.AiGuidanceKeyUnsupported });
        return evaluatedValue10;
    }

    private static async Task<McpAiGuidanceContentDto> ReadAsync(ResolvedGuidanceResource resource, long maxBytes, CancellationToken cancellationToken)
    {
        var readContentAsyncResult = await ReadContentAsync(resource, maxBytes, embeddedOnly: false, cancellationToken);
        var toContentDtoResult11 = ToContentDto(resource, readContentAsyncResult, maxBytes);
        return toContentDtoResult11;
    }

    private static McpAiGuidanceResourceDto ToResourceDto(ResolvedGuidanceResource resource, McpAiGuidanceSourceDto source)
    {
        McpAiGuidanceResourceDto evaluatedValue12 = new(resource.Resource.Key, resource.Resource.Uri, resource.Resource.Name, resource.Resource.Description, source);
        return evaluatedValue12;
    }

    private static McpAiGuidanceContentDto ToContentDto(ResolvedGuidanceResource resource, GuidanceContent content, long maxBytes)
    {
        var identity = $"{resource.Resource.Key}\0{resource.Resource.Uri}\0{resource.FilePath}\0{content.Source}\0{content.Text}";
        var getBytesResult = Encoding.UTF8.GetBytes(identity);
        var hashDataResult = SHA256.HashData(getBytesResult);
        var revision = Convert.ToHexStringLower(hashDataResult);
        var toResourceDtoResult13 = ToResourceDto(resource, content.Source);
        McpAiGuidanceContentDto evaluatedValue14 = new(toResourceDtoResult13, content.Text, revision, maxBytes);
        return evaluatedValue14;
    }

    private static void RequireRevision(string expectedRevision, McpAiGuidanceContentDto current)
    {
        if (string.IsNullOrWhiteSpace(expectedRevision))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The skill revision is required.", new { code = McpErrorCodes.InvalidArguments });
        }

        if (!string.Equals(expectedRevision, current.Revision, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict, "The skill has changed. Reload it before saving or restoring defaults.", new { code = McpErrorCodes.AiGuidanceConflict });
        }
    }

    private static void ValidateContent(string content, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "Skill content must not be empty.", new { code = McpErrorCodes.AiGuidanceContentInvalid });
        }

        if (Encoding.UTF8.GetByteCount(content) > maxBytes)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge, "Skill content exceeds the configured size limit.", new { code = McpErrorCodes.AiGuidanceTooLarge });
        }
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
                {
                    throw AccessDenied();
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }

    private static McpProtocolException AccessDenied()
    {
        McpProtocolException evaluatedValue15 = new(McpProtocolErrorCodes.PermissionDenied, "The MCP skill override cannot be modified.", new { code = McpErrorCodes.AiGuidanceAccessDenied });
        return evaluatedValue15;
    }

    private static McpProtocolException SaveFailed()
    {
        McpProtocolException evaluatedValue16 = new(McpProtocolErrorCodes.TransientFailure, "The MCP skill override could not be saved. Check the server directory and retry.", new { code = McpErrorCodes.AiGuidanceSaveFailed });
        return evaluatedValue16;
    }

    public void Dispose()
    {
        _mutationLock.Dispose();
    }
}
