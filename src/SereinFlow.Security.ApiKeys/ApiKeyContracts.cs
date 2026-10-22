using SereinFlow.Contracts;

namespace SereinFlow.Security.ApiKeys;
public sealed record ApiKeyDto(string Id, Guid? ProjectId, string Name, string KeyPrefix, IReadOnlyList<SereinFlowPermission> Permissions, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, DateTimeOffset? LastUsedAt, bool IsAdministrator = false);
/// <summary>
/// Carries request data for create MCP API key operations.
/// 承载创建MCPAPI密钥操作的请求数据。
/// </summary>
public sealed record CreateApiKeyRequest(Guid? ProjectId, string Name, IReadOnlyList<SereinFlowPermission> Permissions, DateTimeOffset? ExpiresAt = null, bool IsAdministrator = false);
/// <summary>
/// Defines the API data contract for created MCP API key.
/// 定义已创建MCPAPI密钥的 API 数据契约。
/// </summary>
public sealed record CreatedApiKeyDto(ApiKeyDto Key, string Secret);
/// <summary>
/// Defines the API data contract for rotated MCP API key.
/// 定义RotatedMCPAPI密钥的 API 数据契约。
/// </summary>
public sealed record RotatedApiKeyDto(string RevokedKeyId, ApiKeyDto Key, string? Secret, bool Replayed = false);
