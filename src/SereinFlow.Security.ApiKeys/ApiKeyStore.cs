using SereinFlow.Contracts;

namespace SereinFlow.Security.ApiKeys;
public sealed record ApiKeyEntry(string Id, Guid? ProjectId, string Name, string KeyPrefix, string SecretHash, string Salt, IReadOnlyList<SereinFlowPermission> Permissions, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, DateTimeOffset? LastUsedAt, bool IsAdministrator = false);
public interface IApiKeyStore
{
    Task<IReadOnlyList<ApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task<ApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default);
    /// <summary>
    /// Updates usage telemetry without changing key authorization or revocation fields.
    /// 只更新使用情况遥测，不更改密钥授权或撤销字段。
    /// </summary>
    Task<bool> UpdateLastUsedAsync(string id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default);
}
