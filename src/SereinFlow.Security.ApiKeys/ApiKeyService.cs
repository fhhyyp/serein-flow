using System.Security.Cryptography;
using System.Text;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Security.ApiKeys;
public sealed class ApiKeyService
{
    public SereinFlowActor? ResolveBootstrapActor(string id) => id == "bootstrap-admin" && !string.IsNullOrWhiteSpace(_bootstrapAdminKey) ? ApiKeyActors.Create(id, null, AllPermissions, IsAdministrator: true) : null;
    public async Task<SereinFlowActor?> ResolveActorAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id == "bootstrap-admin")
        {
            var result1 = ResolveBootstrapActor(id);
            return result1;
        }

        var entry = await _keys.FindAsync(id, cancellationToken);
        if (entry is null || entry.RevokedAt is not null || entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var permissions = entry.Permissions.ToHashSet();
        var actor = ApiKeyActors.Create(entry.Id, entry.ProjectId, permissions, IsAdministrator: entry.IsAdministrator);
        return actor;
    }

    private static readonly IReadOnlySet<SereinFlowPermission> AllPermissions = CreateAllPermissionsInitialValue();
    private readonly IApiKeyStore _keys;
    private readonly string? _bootstrapAdminKey;
    public ApiKeyService(IApiKeyStore keys, string? bootstrapAdminKey = null)
    {
        _keys = keys;
        _bootstrapAdminKey = bootstrapAdminKey;
    }

    public async Task<SereinFlowActor?> AuthenticateAsync(string? secret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var bootstrap = _bootstrapAdminKey;
        if (!string.IsNullOrWhiteSpace(bootstrap) && resolveFixedTimeEqualsResult())
        {
            var evaluatedValue1 = ApiKeyActors.Create("bootstrap-admin", null, AllPermissions, IsAdministrator: true);
            {
                var result3 = ApiKeyActors.WithStoreReference(evaluatedValue1);
                return result3;
            }
        }

        foreach (var entry in await _keys.ListAsync(cancellationToken))
        {
            if (entry.RevokedAt is not null || entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                continue;
            }

            if (!Verify(secret, entry.Salt, entry.SecretHash))
            {
                continue;
            }

            try
            {
                // Last-used tracking is telemetry, but it must finish before the
                // scoped key store can be disposed. A store failure must not turn
                // an otherwise valid authentication into a denial.
                // 最近使用时间属于遥测，但必须在作用域释放前完成；仓储失败不能
                // 把本来有效的认证变成拒绝。
                await _keys.UpdateLastUsedAsync(entry.Id, DateTimeOffset.UtcNow, CancellationToken.None);
            }
            catch
            {
            // Authentication remains successful when usage telemetry fails.
            // 使用情况遥测失败不影响身份验证成功结果。
            }

            var toHashSetResult = entry.Permissions.ToHashSet();
            var evaluatedValue2 = ApiKeyActors.Create(entry.Id, entry.ProjectId, toHashSetResult, IsAdministrator: entry.IsAdministrator);
            {
                var result4 = ApiKeyActors.WithStoreReference(evaluatedValue2);
                return result4;
            }
        }

        return null;
        bool resolveFixedTimeEqualsResult()
        {
            var getBytesResult = Encoding.UTF8.GetBytes(bootstrap);
            var getBytesResult3 = Encoding.UTF8.GetBytes(secret);
            var fixedTimeEqualsResult = CryptographicOperations.FixedTimeEquals(getBytesResult, getBytesResult3);
            return fixedTimeEqualsResult;
        }
    }

    public static CreatedApiKeyDto CreateKey(CreateApiKeyRequest request)
    {
        var evaluatedValue4 = CreateKeyWithEntry(request).Dto;
        return evaluatedValue4;
    }

    public static (ApiKeyEntry Entry, CreatedApiKeyDto Dto) CreateKeyWithEntry(CreateApiKeyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("The MCP API key name is required. 必须提供 MCP API 密钥名称。", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Permissions);
        if (request.ExpiresAt is not null && request.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("The MCP API key expiration must be in the future. MCP API 密钥的过期时间必须晚于当前时间。", nameof(request));
        }

        if (request.IsAdministrator && request.ProjectId is not null)
        {
            throw new ArgumentException("Administrator API keys cannot be project-scoped. 管理员 API 密钥不能限定为项目范围。", nameof(request));
        }

        if (!request.IsAdministrator && request.ProjectId is null)
        {
            throw new ArgumentException("A non-administrator API key must be bound to a project. 非管理员 API 密钥必须绑定到项目。", nameof(request));
        }

        var secret = $"sfk_{resolveToLowerInvariantResult()}";
        var getBytesResult6 = RandomNumberGenerator.GetBytes(16);
        var toHexStringResult7 = Convert.ToHexString(getBytesResult6);
        var salt = toHexStringResult7.ToLowerInvariant();
        var newGuidResult = Guid.NewGuid();
        var id = newGuidResult.ToString("N");
        var trimResult = request.Name.Trim();
        var hashResult = Hash(secret, salt);
        var toArrayResult = request.Permissions.Distinct().ToArray();
        var entry = new ApiKeyEntry(id, request.ProjectId, trimResult, secret[..12], hashResult, salt, toArrayResult, DateTimeOffset.UtcNow, request.ExpiresAt, null, null, request.IsAdministrator);
        var evaluatedValue8 = (entry, resolveCreatedApiKeyDto());
        return evaluatedValue8;
        string resolveToLowerInvariantResult()
        {
            var getBytesResult5 = RandomNumberGenerator.GetBytes(24);
            var toHexStringResult = Convert.ToHexString(getBytesResult5);
            var toLowerInvariantResult = toHexStringResult.ToLowerInvariant();
            return toLowerInvariantResult;
        }

        CreatedApiKeyDto resolveCreatedApiKeyDto()
        {
            var toDtoResult = ToDto(entry);
            var createdApiKeyDto = new CreatedApiKeyDto(toDtoResult, secret);
            return createdApiKeyDto;
        }
    }

    public static (ApiKeyEntry Entry, CreatedApiKeyDto Dto) RotateKeyWithEntry(ApiKeyEntry existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var createMcpApiKeyRequestDto = new CreateApiKeyRequest(existing.ProjectId, existing.Name, existing.Permissions, existing.ExpiresAt, existing.IsAdministrator);
        var createKeyWithEntryResult = CreateKeyWithEntry(createMcpApiKeyRequestDto);
        return createKeyWithEntryResult;
    }

    public static ApiKeyDto ToDto(ApiKeyEntry entry)
    {
        ApiKeyDto evaluatedValue9 = new(entry.Id, entry.ProjectId, entry.Name, entry.KeyPrefix, entry.Permissions, entry.CreatedAt, entry.ExpiresAt, entry.RevokedAt, entry.LastUsedAt, entry.IsAdministrator);
        return evaluatedValue9;
    }

    public static string Hash(string secret, string salt)
    {
        var getBytesResult10 = Encoding.UTF8.GetBytes($"{salt}:{secret}");
        var hashDataResult = SHA256.HashData(getBytesResult10);
        var toHexStringResult11 = Convert.ToHexString(hashDataResult);
        var toLowerInvariantResult12 = toHexStringResult11.ToLowerInvariant();
        return toLowerInvariantResult12;
    }

    private static bool Verify(string secret, string salt, string expected)
    {
        var actual = Hash(secret, salt);
        var getBytesResult13 = Encoding.UTF8.GetBytes(actual);
        var getBytesResult14 = Encoding.UTF8.GetBytes(expected);
        var fixedTimeEqualsResult15 = CryptographicOperations.FixedTimeEquals(getBytesResult13, getBytesResult14);
        return fixedTimeEqualsResult15;
    }

    private static IReadOnlySet<SereinFlowPermission> CreateAllPermissionsInitialValue()
    {
        IReadOnlySet<SereinFlowPermission> allPermissionsInitialValue = Enum.GetValues<SereinFlowPermission>().ToHashSet();
        return allPermissionsInitialValue;
    }
}
