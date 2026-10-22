using SereinFlow.Application.Persistence;
using Microsoft.AspNetCore.Http;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;
/// <summary>
/// Shared MCP API key lifecycle used by the HTTP management surface and MCP tools.
/// Secrets are returned only at creation or rotation time; persistence stores hashes.
/// HTTP 管理接口与 MCP 工具共用的 API 密钥生命周期服务；仅在创建或轮换时返回密钥明文，持久化只保存哈希。
/// </summary>
public sealed class McpApiKeyManagementService
{
    private static readonly SemaphoreSlim InitialSetupGate = new(1, 1);
    private readonly IMcpApiKeyStore _keys;
    private readonly IProjectRepository _projects;
    public McpApiKeyManagementService(IMcpApiKeyStore keys, IProjectRepository projects)
    {
        _keys = keys;
        _projects = projects;
    }

    public async Task<IReadOnlyList<McpApiKeyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var toArrayResult = (await _keys.ListAsync(cancellationToken))
            .Select(McpSecurityService.ToDto)
            .ToArray();
        return toArrayResult;
    }

    public async Task<CreatedMcpApiKeyDto> CreateAsync(CreateMcpApiKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ValidateProjectAsync(request.ProjectId, cancellationToken);
        var created = McpSecurityService.CreateKeyWithEntry(request);
        await _keys.AddAsync(created.Entry, cancellationToken);
        return created.Dto;
    }

    public async Task<CreatedMcpApiKeyDto> CreateInitialAdministratorAsync(CancellationToken cancellationToken = default)
    {
        await InitialSetupGate.WaitAsync(cancellationToken);
        try
        {
            var existing = await _keys.ListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                throw new McpSecurityException(McpErrorCodes.KeySetupAlreadyCompleted, "MCP API key setup has already been completed. MCP API 密钥初始化已完成。", StatusCodes.Status409Conflict);
            }

            var getValuesResult = Enum.GetValues<McpPermissionDto>();
            var createMcpApiKeyRequestDto = new CreateMcpApiKeyRequestDto(null, "SereinFlow Web Console", getValuesResult, null, IsAdministrator: true);
            var createAsyncResult = await CreateAsync(createMcpApiKeyRequestDto, cancellationToken);
            return createAsyncResult;
        }
        finally
        {
            InitialSetupGate.Release();
        }
    }

    public async Task<McpApiKeyDto> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing.RevokedAt is not null)
        {
            var toDtoResult = McpSecurityService.ToDto(existing);
            return toDtoResult;
        }

        var revoked = existing with
        {
            RevokedAt = DateTimeOffset.UtcNow
        };
        await _keys.UpdateAsync(revoked, cancellationToken);
        var toDtoResult1 = McpSecurityService.ToDto(revoked);
        return toDtoResult1;
    }

    public async Task<RotatedMcpApiKeyDto> RotateAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing.RevokedAt is not null)
        {
            throw new McpSecurityException(McpErrorCodes.KeyAlreadyRevoked, "The MCP API key has already been revoked. MCP API 密钥已撤销。", StatusCodes.Status409Conflict);
        }

        var replacement = McpSecurityService.RotateKeyWithEntry(existing);
        await _keys.AddAsync(replacement.Entry, cancellationToken);
        await _keys.UpdateAsync(existing with { RevokedAt = DateTimeOffset.UtcNow }, cancellationToken);
        var rotatedMcpApiKeyDto = new RotatedMcpApiKeyDto(existing.Id, replacement.Dto.Key, replacement.Dto.Secret);
        return rotatedMcpApiKeyDto;
    }

    private async Task<McpApiKeyEntry> FindAsync(string id, CancellationToken cancellationToken)
    {
        var evaluatedValue2 = await _keys.FindAsync(id, cancellationToken) ?? throw new McpSecurityException(McpErrorCodes.KeyNotFound, "The MCP API key was not found. 未找到 MCP API 密钥。", StatusCodes.Status404NotFound);
        return evaluatedValue2;
    }

    private async Task ValidateProjectAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return;
        }

        var project = await _projects.FindAsync(projectId.Value, cancellationToken);
        if (project is null)
        {
            throw new McpSecurityException(McpErrorCodes.ProjectNotFound, "The API key project was not found. 未找到 API 密钥所属项目。", StatusCodes.Status404NotFound);
        }

        if (project.Status == ProjectStatus.Archived)
        {
            throw new McpSecurityException(McpErrorCodes.ProjectArchived, "An API key cannot be bound to an archived project. API 密钥不能绑定到已归档的项目。", StatusCodes.Status409Conflict);
        }
    }
}
