using SqlSugar;

namespace SereinFlow.Security.ApiKeys;
[SugarTable("McpApiKeys")]
public sealed class ApiKeyRecord
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "[]";
    public string CreatedAt { get; set; } = string.Empty;
    public string? ExpiresAt { get; set; }
    public string? RevokedAt { get; set; }
    public string? LastUsedAt { get; set; }
    public bool IsAdministrator { get; set; }
}
