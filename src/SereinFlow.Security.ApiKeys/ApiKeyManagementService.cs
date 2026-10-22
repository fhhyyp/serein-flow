using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using Microsoft.AspNetCore.Http;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Security.ApiKeys;
/// <summary>
/// Optional API key lifecycle used by the HTTP management surface.
/// Secrets are returned only at creation or rotation time; persistence stores hashes.
/// HTTP 管理接口使用的可选 API 密钥生命周期服务；仅在创建或轮换时返回密钥明文，持久化只保存哈希。
/// </summary>
public sealed class ApiKeyManagementService
{
    private static readonly SemaphoreSlim InitialSetupGate = new(1, 1);
    private readonly IApiKeyStore _keys;
    private readonly IProjectRepository _projects;
    public ApiKeyManagementService(IApiKeyStore keys, IProjectRepository projects)
    {
        _keys = keys;
        _projects = projects;
    }

    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var toArrayResult = (await _keys.ListAsync(cancellationToken)).Select(ApiKeyService.ToDto).ToArray();
        return toArrayResult;
    }

    public async Task<CreatedApiKeyDto> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ValidateProjectAsync(request.ProjectId, cancellationToken);
        var created = ApiKeyService.CreateKeyWithEntry(request);
        await _keys.AddAsync(created.Entry, cancellationToken);
        return created.Dto;
    }

    public async Task<CreatedApiKeyDto> CreateInitialAdministratorAsync(CancellationToken cancellationToken = default)
    {
        await InitialSetupGate.WaitAsync(cancellationToken);
        try
        {
            var existing = await _keys.ListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                throw new SereinFlowAccessException(ApiKeyErrorCodes.SetupAlreadyCompleted, "API key setup has already been completed. API 密钥初始化已完成。", StatusCodes.Status409Conflict);
            }

            var getValuesResult = Enum.GetValues<SereinFlowPermission>();
            var createMcpApiKeyRequestDto = new CreateApiKeyRequest(null, "SereinFlow Web Console", getValuesResult, null, IsAdministrator: true);
            var createAsyncResult = await CreateAsync(createMcpApiKeyRequestDto, cancellationToken);
            return createAsyncResult;
        }
        finally
        {
            InitialSetupGate.Release();
        }
    }

    public async Task<ApiKeyDto> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing.RevokedAt is not null)
        {
            var toDtoResult = ApiKeyService.ToDto(existing);
            return toDtoResult;
        }

        var revoked = existing with
        {
            RevokedAt = DateTimeOffset.UtcNow
        };
        await _keys.UpdateAsync(revoked, cancellationToken);
        var toDtoResult1 = ApiKeyService.ToDto(revoked);
        return toDtoResult1;
    }

    public async Task<RotatedApiKeyDto> RotateAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing.RevokedAt is not null)
        {
            throw new SereinFlowAccessException(ApiKeyErrorCodes.AlreadyRevoked, "The API key has already been revoked. API 密钥已撤销。", StatusCodes.Status409Conflict);
        }

        var replacement = ApiKeyService.RotateKeyWithEntry(existing);
        await _keys.AddAsync(replacement.Entry, cancellationToken);
        await _keys.UpdateAsync(existing with { RevokedAt = DateTimeOffset.UtcNow }, cancellationToken);
        var rotatedApiKeyDto = new RotatedApiKeyDto(existing.Id, replacement.Dto.Key, replacement.Dto.Secret);
        return rotatedApiKeyDto;
    }

    private async Task<ApiKeyEntry> FindAsync(string id, CancellationToken cancellationToken)
    {
        var evaluatedValue2 = await _keys.FindAsync(id, cancellationToken) ?? throw new SereinFlowAccessException(ApiKeyErrorCodes.NotFound, "The API key was not found. 未找到 API 密钥。", StatusCodes.Status404NotFound);
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
            throw new SereinFlowAccessException(McpErrorCodes.ProjectNotFound, "The API key project was not found. 未找到 API 密钥所属项目。", StatusCodes.Status404NotFound);
        }

        if (project.Status == ProjectStatus.Archived)
        {
            throw new SereinFlowAccessException(McpErrorCodes.ProjectArchived, "An API key cannot be bound to an archived project. API 密钥不能绑定到已归档的项目。", StatusCodes.Status409Conflict);
        }
    }
}
