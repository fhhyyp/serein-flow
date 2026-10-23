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
            return ResolveBootstrapActor(id);
        }

        var entry = await _keys.FindAsync(id, cancellationToken);
        if (entry is null || entry.RevokedAt is not null || entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var permissions = entry.Permissions.ToHashSet();
        return ApiKeyActors.Create(entry.Id, entry.ProjectId, permissions, IsAdministrator: entry.IsAdministrator);
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
        if (!string.IsNullOrWhiteSpace(bootstrap) && BootstrapSecretMatches())
        {
            var actor = ApiKeyActors.Create("bootstrap-admin", null, AllPermissions, IsAdministrator: true);
            {
                return ApiKeyActors.WithStoreReference(actor);
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

            var permissions = entry.Permissions.ToHashSet();
            var apiKeyActors = ApiKeyActors.Create(entry.Id, entry.ProjectId, permissions, IsAdministrator: entry.IsAdministrator);
            {
                return ApiKeyActors.WithStoreReference(apiKeyActors);
            }
        }

        return null;
        bool BootstrapSecretMatches()
        {
            var bytes = Encoding.UTF8.GetBytes(bootstrap);
            var mappedBytes = Encoding.UTF8.GetBytes(secret);
            return CryptographicOperations.FixedTimeEquals(bytes, mappedBytes);
        }
    }

    public static CreatedApiKeyDto CreateKey(CreateApiKeyRequest request)
    {
        return CreateKeyWithEntry(request).Dto;
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

        var secret = $"sfk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var id = Guid.NewGuid().ToString("N");
        var name = request.Name.Trim();
        var secretHash = Hash(secret, salt);
        var permissions = request.Permissions.Distinct().ToArray();
        var entry = new ApiKeyEntry(id, request.ProjectId, name, secret[..12], secretHash, salt, permissions, DateTimeOffset.UtcNow, request.ExpiresAt, null, null, request.IsAdministrator);
        return (entry, new CreatedApiKeyDto(ToDto(entry), secret));
    }

    public static (ApiKeyEntry Entry, CreatedApiKeyDto Dto) RotateKeyWithEntry(ApiKeyEntry existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var createMcpApiKeyRequestDto = new CreateApiKeyRequest(existing.ProjectId, existing.Name, existing.Permissions, existing.ExpiresAt, existing.IsAdministrator);
        return CreateKeyWithEntry(createMcpApiKeyRequestDto);
    }

    public static ApiKeyDto ToDto(ApiKeyEntry entry)
    {
        return new(entry.Id, entry.ProjectId, entry.Name, entry.KeyPrefix, entry.Permissions, entry.CreatedAt, entry.ExpiresAt, entry.RevokedAt, entry.LastUsedAt, entry.IsAdministrator);
    }

    public static string Hash(string secret, string salt)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{secret}"))).ToLowerInvariant();
    }

    private static bool Verify(string secret, string salt, string expected)
    {
        var actual = Hash(secret, salt);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
    }

    private static HashSet<SereinFlowPermission> CreateAllPermissionsInitialValue()
    {
        return Enum.GetValues<SereinFlowPermission>().ToHashSet();
    }
}
