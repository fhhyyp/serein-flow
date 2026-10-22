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
        var toArrayResult = (await _records.ListAsync(cancellationToken: cancellationToken)).Select(Map).ToArray();
        return toArrayResult;
    }

    public async Task<ApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var record = await _records.GetByIdAsync(id, cancellationToken);
        ApiKeyEntry? evaluatedValue1 = record is null ? null : Map(record);
        return evaluatedValue1;
    }

    public Task AddAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        var mapResult = Map(entry);
        var addAsyncResult = _records.AddAsync(mapResult, cancellationToken);
        return addAsyncResult;
    }

    public Task<bool> UpdateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        var mapResult2 = Map(entry);
        var updateAsyncResult = _records.UpdateAsync(mapResult2, cancellationToken);
        return updateAsyncResult;
    }

    public async Task<bool> UpdateLastUsedAsync(string id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timestamp = lastUsedAt.ToString("O");
        var update = _database.Updateable<ApiKeyRecord>().SetColumns(record => record.LastUsedAt == timestamp).Where(record => record.Id == id);
        {
            var result1 = await update.ExecuteCommandAsync(cancellationToken) > 0;
            return result1;
        }
    }

    private static ApiKeyEntry Map(ApiKeyRecord record)
    {
        Guid? evaluatedValue3 = Guid.TryParse(record.ProjectId, out var projectId) ? projectId : null;
        var deserializePermissionsResult = DeserializePermissions(record.PermissionsJson);
        var parseDateResult = ParseDate(record.CreatedAt);
        var parseNullableDateResult = ParseNullableDate(record.ExpiresAt);
        var parseNullableDateResult4 = ParseNullableDate(record.RevokedAt);
        var parseNullableDateResult5 = ParseNullableDate(record.LastUsedAt);
        ApiKeyEntry evaluatedValue6 = new(record.Id, evaluatedValue3, record.Name, record.KeyPrefix, record.SecretHash, record.Salt, deserializePermissionsResult, parseDateResult, parseNullableDateResult, parseNullableDateResult4, parseNullableDateResult5, record.IsAdministrator);
        return evaluatedValue6;
    }

    private static ApiKeyRecord Map(ApiKeyEntry entry)
    {
        ApiKeyRecord evaluatedValue7 = new()
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
        return evaluatedValue7;
    }

    private static SereinFlowPermission[] DeserializePermissions(string json)
    {
        using var document = JsonDocument.Parse(json);
        {
            var result3 = document.RootElement.EnumerateArray().Select(value =>
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
                    var result2 = SereinFlowOperationNames.TryParse(name, out var permission) ? permission : throw new JsonException("Unknown stored API key permission.");
                    return result2;
                }
            }).ToArray();
            return result3;
        }
    }

    private static DateTimeOffset ParseDate(string value)
    {
        DateTimeOffset evaluatedValue9 = DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UtcNow;
        return evaluatedValue9;
    }

    private static DateTimeOffset? ParseNullableDate(string? value)
    {
        DateTimeOffset? evaluatedValue10 = DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
        return evaluatedValue10;
    }
}
