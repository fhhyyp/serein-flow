using System.Text.Json;
using SqlSugar;
using SereinFlow.Application.Persistence;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Security.ApiKeys;

public sealed class ApiKeyStore : IApiKeyStore
{
    private readonly IRepository<ApiKeyRecord> _records;
    private readonly ISqlSugarClient _database;
    public ApiKeyStore(IRepository<ApiKeyRecord> records, ISqlSugarClient database)
    {
        _records = records;
        _database = database;
    }

    public async Task<IReadOnlyList<ApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        return (await _records.ListAsync(cancellationToken: cancellationToken)).Select(Map).ToArray();
    }

    public async Task<ApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var record = await _records.GetByIdAsync(id, cancellationToken);
        return record is null ? null : Map(record);
    }

    public Task AddAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        var entity = Map(entry);
        return _records.AddAsync(entity, cancellationToken);
    }

    public Task<bool> UpdateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        var entity = Map(entry);
        return _records.UpdateAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateLastUsedAsync(string id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timestamp = lastUsedAt.ToString("O");
        var update = _database
            .Updateable<ApiKeyRecord>()
            .SetColumns(record => record.LastUsedAt == timestamp)
            .Where(record => record.Id == id);
        {
            return await update.ExecuteCommandAsync(cancellationToken) > 0;
        }
    }

    private static ApiKeyEntry Map(ApiKeyRecord record)
    {
        Guid? parsedProjectId = Guid.TryParse(record.ProjectId, out var projectId) ? projectId : null;
        var permissions = DeserializePermissions(record.PermissionsJson);
        var createdAt = ParseDate(record.CreatedAt);
        var expiresAt = ParseNullableDate(record.ExpiresAt);
        var revokedAt = ParseNullableDate(record.RevokedAt);
        var lastUsedAt = ParseNullableDate(record.LastUsedAt);
        return new(record.Id, parsedProjectId, record.Name, record.KeyPrefix, record.SecretHash, record.Salt, permissions, createdAt, expiresAt, revokedAt, lastUsedAt, record.IsAdministrator);
    }

    private static ApiKeyRecord Map(ApiKeyEntry entry)
    {
        return new()
        {
            Id = entry.Id,
            ProjectId = entry.ProjectId?.ToString("D"),
            Name = entry.Name,
            KeyPrefix = entry.KeyPrefix,
            SecretHash = entry.SecretHash,
            Salt = entry.Salt,
            PermissionsJson = JsonSerializer.Serialize(entry.Permissions),
            CreatedAt = entry.CreatedAt.ToString("O"),
            ExpiresAt = entry.ExpiresAt?.ToString("O"),
            RevokedAt = entry.RevokedAt?.ToString("O"),
            LastUsedAt = entry.LastUsedAt?.ToString("O"),
            IsAdministrator = entry.IsAdministrator
        };
    }

    private static SereinFlowPermission[] DeserializePermissions(string json)
    {
        using var document = JsonDocument.Parse(json);
        {
            return document.RootElement
                .EnumerateArray()
                .Select(value =>
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && Enum.IsDefined((SereinFlowPermission)number))
                {
                    return (SereinFlowPermission)number;
                }

                var name = value.GetString();
                if (string.Equals(name, "mcp.keys.manage", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "McpKeysManage", StringComparison.OrdinalIgnoreCase))
                {
                    return SereinFlowPermission.SecurityManage;
                }

                if (Enum.TryParse<SereinFlowPermission>(name, ignoreCase: true, out var legacy) && Enum.IsDefined(legacy))
                {
                    return legacy;
                }

                {
                    return SereinFlowOperationNames.TryParse(name, out var permission) ? permission : throw new JsonException("Unknown stored API key permission.");
                }
            })
                .ToArray();
        }
    }

    private static DateTimeOffset ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset? ParseNullableDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}
