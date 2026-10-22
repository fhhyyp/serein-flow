using System.Security.Cryptography;
using System.Text;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpAiGuidanceProvider;

namespace SereinFlow.Mcp;
/// <summary>
/// Manages disk overrides for the same registered resources served by MCP.
/// 管理 MCP 已注册资源对应的磁盘覆盖文件。
/// </summary>
public sealed class McpAiGuidanceManagementService(McpAiGuidanceProvider provider) : IDisposable
{
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    public IReadOnlyList<McpAiGuidanceResourceDto> List()
    {
        return provider.CreateSnapshot().ById.Values.Select(resource =>
            ToResourceDto(resource, File.Exists(resource.FilePath) ? McpAiGuidanceSourceDto.Disk : McpAiGuidanceSourceDto.Embedded))
            .ToArray();
    }

    public async Task<McpAiGuidanceContentDto> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var snapshot = provider.CreateSnapshot();
        var resource = Resolve(snapshot, key);
        return await ReadAsync(resource, snapshot.MaxBytes, cancellationToken);
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
            var current = await ReadAsync(resource, snapshot.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, current);
            EnsureWritablePath(resource.FilePath);
            var directory = Path.GetDirectoryName(resource.FilePath)!;
            Directory.CreateDirectory(directory);
            EnsureWritablePath(resource.FilePath);
            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(resource.FilePath)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporaryPath, request.Content, new UTF8Encoding(false), cancellationToken);
            // Configuration and external file edits may change while the temporary file is written.
            // 写入临时文件期间，配置和外部文件仍可能发生变化。
            var latest = provider.CreateSnapshot();
            var latestResource = Resolve(latest, key);
            var latestContent = await ReadAsync(latestResource, latest.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, latestContent);
            ValidateContent(request.Content, latest.MaxBytes);
            EnsureWritablePath(resource.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, resource.FilePath, overwrite: true);
            temporaryPath = null;
            var guidanceContent = new GuidanceContent(request.Content, McpAiGuidanceSourceDto.Disk);
            return ToContentDto(resource, guidanceContent, latest.MaxBytes);
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
            var current = await ReadAsync(resource, snapshot.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, current);
            var content = await ReadContentAsync(resource, snapshot.MaxBytes, embeddedOnly: true, cancellationToken);
            var latest = provider.CreateSnapshot();
            var latestResource = Resolve(latest, key);
            var latestContent = await ReadAsync(latestResource, latest.MaxBytes, cancellationToken);
            RequireRevision(request.ExpectedRevision, latestContent);
            ValidateContent(content.Text, latest.MaxBytes);
            EnsureWritablePath(resource.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(resource.FilePath))
            {
                File.Delete(resource.FilePath);
            }

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
    {
        return snapshot.ById.Values.FirstOrDefault(resource =>
            string.Equals(resource.Resource.Key, key, StringComparison.Ordinal)) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP skill is not registered. MCP 技能未注册。", new { code = McpErrorCodes.AiGuidanceKeyUnsupported });
    }

    private static async Task<McpAiGuidanceContentDto> ReadAsync(ResolvedGuidanceResource resource, long maxBytes, CancellationToken cancellationToken)
    {
        var content = await ReadContentAsync(resource, maxBytes, embeddedOnly: false, cancellationToken);
        return ToContentDto(resource, content, maxBytes);
    }

    private static McpAiGuidanceResourceDto ToResourceDto(ResolvedGuidanceResource resource, McpAiGuidanceSourceDto source)
    {
        return new McpAiGuidanceResourceDto(resource.Resource.Key, resource.Resource.Uri, resource.Resource.Name, resource.Resource.Description, source);
    }

    private static McpAiGuidanceContentDto ToContentDto(ResolvedGuidanceResource resource, GuidanceContent content, long maxBytes)
    {
        var identity = $"{resource.Resource.Key}\0{resource.Resource.Uri}\0{resource.FilePath}\0{content.Source}\0{content.Text}";
        var revision = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var descriptor = ToResourceDto(resource, content.Source);
        return new McpAiGuidanceContentDto(descriptor, content.Text, revision, maxBytes);
    }

    private static void RequireRevision(string expectedRevision, McpAiGuidanceContentDto current)
    {
        if (string.IsNullOrWhiteSpace(expectedRevision))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The skill revision is required. 必须提供技能修订版本。", new { code = McpErrorCodes.InvalidArguments });
        }

        if (!string.Equals(expectedRevision, current.Revision, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict, "The skill has changed. Reload it before saving or restoring defaults. 技能已发生变化，请重新加载后再保存或恢复默认值。", new { code = McpErrorCodes.AiGuidanceConflict });
        }
    }

    private static void ValidateContent(string content, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "Skill content must not be empty. 技能内容不能为空。", new { code = McpErrorCodes.AiGuidanceContentInvalid });
        }

        if (Encoding.UTF8.GetByteCount(content) > maxBytes)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge, "Skill content exceeds the configured size limit. 技能内容超过配置的大小限制。", new { code = McpErrorCodes.AiGuidanceTooLarge });
        }
    }

    private void EnsureWritablePath(string filePath)
    {
        // Configured paths are already validated by the provider. Reject links as well:
        // a directory junction or file symlink must not redirect a write outside the root.
        // 提供程序已校验配置路径，此处仍须拒绝链接，防止目录联接或文件符号链接将写入重定向到根目录之外。
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
        return new McpProtocolException(McpProtocolErrorCodes.PermissionDenied, "The MCP skill override cannot be modified. 无法修改 MCP 技能覆盖文件。", new { code = McpErrorCodes.AiGuidanceAccessDenied });
    }

    private static McpProtocolException SaveFailed()
    {
        return new McpProtocolException(McpProtocolErrorCodes.TransientFailure, "The MCP skill override could not be saved. Check the server directory and retry. MCP 技能覆盖文件保存失败，请检查服务器目录后重试。", new { code = McpErrorCodes.AiGuidanceSaveFailed });
    }

    public void Dispose()
    {
        _mutationLock.Dispose();
    }
}
