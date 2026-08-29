using System.Security.Cryptography;
using System.Text;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed record McpPrincipal(
    string Id,
    Guid? ProjectId,
    IReadOnlySet<McpPermissionDto> Permissions,
    bool IsLocal = false,
    bool IsAdministrator = false)
{
    public bool Has(McpPermissionDto permission)
        => IsLocal || IsAdministrator || Permissions.Contains(permission);

    public bool CanAccess(Guid projectId)
        => IsLocal || IsAdministrator || ProjectId == projectId;
}

/// <summary>
/// Request-scoped MCP identity and transport metadata shared by stdio and HTTP.
/// The request ID is populated by the protocol dispatcher when a JSON-RPC
/// message is being handled.
/// MCP 请求级主体和传输元数据，供 stdio 与 HTTP 共用；JSON-RPC 消息处理期间由
/// 协议分发器填充 RequestId。
/// </summary>
public sealed record McpRequestContext(
    McpPrincipal? Principal,
    string Transport,
    string? SessionId = null,
    string? RequestId = null);

public interface IMcpRequestContextAccessor
{
    McpRequestContext? Current { get; set; }
}

public sealed class McpRequestContextAccessor : IMcpRequestContextAccessor
{
    private readonly AsyncLocal<McpRequestContext?> _current = new();

    public McpRequestContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

public interface IMcpPrincipalAccessor
{
    McpPrincipal? Current { get; set; }
}

public sealed class McpPrincipalAccessor : IMcpPrincipalAccessor
{
    private readonly AsyncLocal<McpPrincipal?> _current = new();

    public McpPrincipal? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

public sealed class McpSecurityService
{
    private static readonly IReadOnlySet<McpPermissionDto> AllPermissions =
        Enum.GetValues<McpPermissionDto>().ToHashSet();
    private readonly IMcpApiKeyStore _keys;
    private readonly string? _bootstrapAdminKey;

    public McpSecurityService(IMcpApiKeyStore keys, string? bootstrapAdminKey = null)
    {
        _keys = keys;
        _bootstrapAdminKey = bootstrapAdminKey;
    }

    public async Task<McpPrincipal?> AuthenticateAsync(string? secret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return null;
        var bootstrap = _bootstrapAdminKey;
        if (!string.IsNullOrWhiteSpace(bootstrap)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(bootstrap),
                Encoding.UTF8.GetBytes(secret)))
        {
            return new("bootstrap-admin", null, AllPermissions, IsAdministrator: true);
        }

        foreach (var entry in await _keys.ListAsync(cancellationToken))
        {
            if (entry.RevokedAt is not null || entry.ExpiresAt <= DateTimeOffset.UtcNow)
                continue;
            if (!Verify(secret, entry.Salt, entry.SecretHash))
                continue;
            try
            {
                // Last-used tracking is telemetry, but it must finish before the
                // scoped key store can be disposed. A store failure must not turn
                // an otherwise valid authentication into a denial.
                // 最近使用时间属于遥测，但必须在作用域释放前完成；仓储失败不能
                // 把本来有效的认证变成拒绝。
                await _keys.UpdateAsync(entry with { LastUsedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
            }
            catch
            {
                // Authentication remains successful when usage telemetry fails.
            }
            return new(entry.Id, entry.ProjectId, entry.Permissions.ToHashSet(), false, entry.IsAdministrator);
        }
        return null;
    }

    public void Require(McpPrincipal? principal, McpPermissionDto permission, Guid? projectId = null)
    {
        if (principal is null)
            throw new McpSecurityException("mcp.unauthenticated", "MCP authentication is required.", 401);
        if (!principal.Has(permission))
            throw new McpSecurityException("mcp.permission_denied", "The MCP caller does not have the required permission.", 403);
        if (projectId is not null && !principal.CanAccess(projectId.Value))
            throw new McpSecurityException("mcp.project_access_denied", "The MCP caller cannot access this project.", 403);
    }

    public void RequireAdministrator(McpPrincipal? principal)
    {
        if (principal is null)
            throw new McpSecurityException("mcp.unauthenticated", "MCP authentication is required.", 401);
        if (!principal.IsLocal && !principal.IsAdministrator)
            throw new McpSecurityException("mcp.administrator_required", "Administrator permission is required for this MCP operation.", 403);
    }

    public static CreatedMcpApiKeyDto CreateKey(CreateMcpApiKeyRequestDto request)
        => CreateKeyWithEntry(request).Dto;

    public static (McpApiKeyEntry Entry, CreatedMcpApiKeyDto Dto) CreateKeyWithEntry(CreateMcpApiKeyRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("The MCP API key name is required.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Permissions);
        if (request.ExpiresAt is not null && request.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("The MCP API key expiration must be in the future.", nameof(request));
        if (request.IsAdministrator && request.ProjectId is not null)
            throw new ArgumentException("Administrator API keys cannot be project-scoped.", nameof(request));
        if (!request.IsAdministrator && request.ProjectId is null)
            throw new ArgumentException("A non-administrator API key must be bound to a project.", nameof(request));
        var secret = $"sfk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var id = Guid.NewGuid().ToString("N");
        var entry = new McpApiKeyEntry(
            id,
            request.ProjectId,
            request.Name.Trim(),
            secret[..12],
            Hash(secret, salt),
            salt,
            request.Permissions.Distinct().ToArray(),
            DateTimeOffset.UtcNow,
            request.ExpiresAt,
            null,
            null,
            request.IsAdministrator);
        return (entry, new CreatedMcpApiKeyDto(ToDto(entry), secret));
    }

    public static (McpApiKeyEntry Entry, CreatedMcpApiKeyDto Dto) RotateKeyWithEntry(McpApiKeyEntry existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            existing.ProjectId,
            existing.Name,
            existing.Permissions,
            existing.ExpiresAt,
            existing.IsAdministrator));
    }

    public static McpApiKeyDto ToDto(McpApiKeyEntry entry)
        => new(entry.Id, entry.ProjectId, entry.Name, entry.KeyPrefix, entry.Permissions, entry.CreatedAt, entry.ExpiresAt, entry.RevokedAt, entry.LastUsedAt, entry.IsAdministrator);

    public static string Hash(string secret, string salt)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{secret}"))).ToLowerInvariant();

    private static bool Verify(string secret, string salt, string expected)
    {
        var actual = Hash(secret, salt);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
    }
}

public sealed class McpSecurityException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
