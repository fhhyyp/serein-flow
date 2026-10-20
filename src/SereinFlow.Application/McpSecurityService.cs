using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
public sealed record McpPrincipal(string Id, Guid? ProjectId, IReadOnlySet<McpPermissionDto> Permissions, bool IsLocal = false, bool IsAdministrator = false)
{
    public bool Has(McpPermissionDto permission)
    {
        var evaluatedValue = IsLocal || IsAdministrator || Permissions.Contains(permission);
        return evaluatedValue;
    }

    public bool CanAccess(Guid projectId) => IsLocal || IsAdministrator || ProjectId == projectId;
}

/// <summary>
/// Request-scoped MCP identity and transport metadata shared by stdio and HTTP.
/// The request ID is populated by the protocol dispatcher when a JSON-RPC
/// message is being handled.
/// MCP 请求级主体和传输元数据，供 stdio 与 HTTP 共用；JSON-RPC 消息处理期间由
/// 协议分发器填充 RequestId。
/// </summary>
public sealed record McpRequestContext(McpPrincipal? Principal, string Transport, string? SessionId = null, string? RequestId = null);
public interface IMcpRequestContextAccessor
{
    McpRequestContext? Current { get; set; }
}

public sealed class McpRequestContextAccessor : IMcpRequestContextAccessor
{
    private readonly AsyncLocal<McpRequestContext?> _current = new();
    public McpRequestContext? Current { get => _current.Value; set => _current.Value = value; }
}

public interface IMcpPrincipalAccessor
{
    McpPrincipal? Current { get; set; }
}

public sealed class McpPrincipalAccessor : IMcpPrincipalAccessor
{
    private readonly AsyncLocal<McpPrincipal?> _current = new();
    public McpPrincipal? Current { get => _current.Value; set => _current.Value = value; }
}

public sealed class McpSecurityService
{
    private static readonly IReadOnlySet<McpPermissionDto> AllPermissions = createAllPermissionsInitialValue();
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
        {
            return null;
        }

        var bootstrap = _bootstrapAdminKey;
        if (!string.IsNullOrWhiteSpace(bootstrap) && resolveFixedTimeEqualsResult())
        {
            McpPrincipal evaluatedValue1 = new("bootstrap-admin", null, AllPermissions, IsAdministrator: true);
            return evaluatedValue1;
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
                await _keys.UpdateAsync(entry with { LastUsedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
            }
            catch
            {
            // Authentication remains successful when usage telemetry fails.
            }

            var toHashSetResult = entry.Permissions.ToHashSet();
            McpPrincipal evaluatedValue2 = new(entry.Id, entry.ProjectId, toHashSetResult, false, entry.IsAdministrator);
            return evaluatedValue2;
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

    public void Require(McpPrincipal? principal, McpPermissionDto permission, Guid? projectId = null)
    {
        if (principal is null)
        {
            throw new McpSecurityException(McpErrorCodes.Unauthenticated, "MCP authentication is required.", StatusCodes.Status401Unauthorized);
        }

        if (!principal.Has(permission))
        {
            throw new McpSecurityException(McpErrorCodes.PermissionDenied, "The MCP caller does not have the required permission.", StatusCodes.Status403Forbidden);
        }

        if (projectId is not null && !principal.CanAccess(projectId.Value))
        {
            throw new McpSecurityException(McpErrorCodes.ProjectAccessDenied, "The MCP caller cannot access this project.", StatusCodes.Status403Forbidden);
        }
    }

    public void RequireAny(McpPrincipal? principal, Guid? projectId, params McpPermissionDto[] permissions)
    {
        if (principal is null)
        {
            throw new McpSecurityException(McpErrorCodes.Unauthenticated, "MCP authentication is required.", StatusCodes.Status401Unauthorized);
        }

        if (permissions is null || permissions.Length == 0 || !permissions.Any(principal.Has))
        {
            throw new McpSecurityException(McpErrorCodes.PermissionDenied, "The MCP caller does not have the required permission.", StatusCodes.Status403Forbidden);
        }

        if (projectId is not null && !principal.CanAccess(projectId.Value))
        {
            throw new McpSecurityException(McpErrorCodes.ProjectAccessDenied, "The MCP caller cannot access this project.", StatusCodes.Status403Forbidden);
        }
    }

    public void RequireAdministrator(McpPrincipal? principal)
    {
        if (principal is null)
        {
            throw new McpSecurityException(McpErrorCodes.Unauthenticated, "MCP authentication is required.", StatusCodes.Status401Unauthorized);
        }

        if (!principal.IsLocal && !principal.IsAdministrator)
        {
            throw new McpSecurityException(McpErrorCodes.AdministratorRequired, "Administrator permission is required for this MCP operation.", StatusCodes.Status403Forbidden);
        }
    }

    public static CreatedMcpApiKeyDto CreateKey(CreateMcpApiKeyRequestDto request)
    {
        var evaluatedValue4 = CreateKeyWithEntry(request).Dto;
        return evaluatedValue4;
    }

    public static (McpApiKeyEntry Entry, CreatedMcpApiKeyDto Dto) CreateKeyWithEntry(CreateMcpApiKeyRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("The MCP API key name is required.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Permissions);
        if (request.ExpiresAt is not null && request.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("The MCP API key expiration must be in the future.", nameof(request));
        }

        if (request.IsAdministrator && request.ProjectId is not null)
        {
            throw new ArgumentException("Administrator API keys cannot be project-scoped.", nameof(request));
        }

        if (!request.IsAdministrator && request.ProjectId is null)
        {
            throw new ArgumentException("A non-administrator API key must be bound to a project.", nameof(request));
        }

        var secret = $"sfk_{resolveToLowerInvariantResult()}";
        var getBytesResult6 = RandomNumberGenerator.GetBytes(16);
        var toHexStringResult7 = Convert.ToHexString(getBytesResult6);
        var salt = toHexStringResult7.ToLowerInvariant();
        var newGuidResult = Guid.NewGuid();
        var id = newGuidResult.ToString("N");
        var trimResult = request.Name.Trim();
        var hashResult = Hash(secret, salt);
        var distinctResult = request.Permissions.Distinct();
        var toArrayResult = distinctResult.ToArray();
        var entry = new McpApiKeyEntry(id, request.ProjectId, trimResult, secret[..12], hashResult, salt, toArrayResult, DateTimeOffset.UtcNow, request.ExpiresAt, null, null, request.IsAdministrator);
        var evaluatedValue8 = (entry, resolveCreatedMcpApiKeyDto());
        return evaluatedValue8;
        string resolveToLowerInvariantResult()
        {
            var getBytesResult5 = RandomNumberGenerator.GetBytes(24);
            var toHexStringResult = Convert.ToHexString(getBytesResult5);
            var toLowerInvariantResult = toHexStringResult.ToLowerInvariant();
            return toLowerInvariantResult;
        }

        CreatedMcpApiKeyDto resolveCreatedMcpApiKeyDto()
        {
            var toDtoResult = ToDto(entry);
            var createdMcpApiKeyDto = new CreatedMcpApiKeyDto(toDtoResult, secret);
            return createdMcpApiKeyDto;
        }
    }

    public static (McpApiKeyEntry Entry, CreatedMcpApiKeyDto Dto) RotateKeyWithEntry(McpApiKeyEntry existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var createMcpApiKeyRequestDto = new CreateMcpApiKeyRequestDto(existing.ProjectId, existing.Name, existing.Permissions, existing.ExpiresAt, existing.IsAdministrator);
        var createKeyWithEntryResult = CreateKeyWithEntry(createMcpApiKeyRequestDto);
        return createKeyWithEntryResult;
    }

    public static McpApiKeyDto ToDto(McpApiKeyEntry entry)
    {
        McpApiKeyDto evaluatedValue9 = new(entry.Id, entry.ProjectId, entry.Name, entry.KeyPrefix, entry.Permissions, entry.CreatedAt, entry.ExpiresAt, entry.RevokedAt, entry.LastUsedAt, entry.IsAdministrator);
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

    private static IReadOnlySet<McpPermissionDto> createAllPermissionsInitialValue()
    {
        var getValuesResult = Enum.GetValues<McpPermissionDto>();
        IReadOnlySet<McpPermissionDto> allPermissionsInitialValue = getValuesResult.ToHashSet();
        return allPermissionsInitialValue;
    }
}

public sealed class McpSecurityException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
