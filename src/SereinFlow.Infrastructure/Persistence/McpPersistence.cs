using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarMcpApiKeyStore : IMcpApiKeyStore
{
    private readonly IRepository<McpApiKeyRecord> _records;

    public SqlSugarMcpApiKeyStore(IRepository<McpApiKeyRecord> records) => _records = records;

    public async Task<IReadOnlyList<McpApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
        => (await _records.ListAsync(cancellationToken: cancellationToken)).Select(Map).ToArray();

    public async Task<McpApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var record = await _records.GetByIdAsync(id, cancellationToken);
        return record is null ? null : Map(record);
    }

    public Task AddAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default)
        => _records.AddAsync(Map(entry), cancellationToken);

    public Task<bool> UpdateAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default)
        => _records.UpdateAsync(Map(entry), cancellationToken);

    private static McpApiKeyEntry Map(McpApiKeyRecord record)
        => new(
            record.Id,
            Guid.TryParse(record.ProjectId, out var projectId) ? projectId : null,
            record.Name,
            record.KeyPrefix,
            record.SecretHash,
            record.Salt,
            DeserializePermissions(record.PermissionsJson),
            ParseDate(record.CreatedAt),
            ParseNullableDate(record.ExpiresAt),
            ParseNullableDate(record.RevokedAt),
            ParseNullableDate(record.LastUsedAt),
            record.IsAdministrator);

    private static McpApiKeyRecord Map(McpApiKeyEntry entry)
        => new()
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
            IsAdministrator = entry.IsAdministrator,
        };

    private static McpPermissionDto[] DeserializePermissions(string json)
        => JsonSerializer.Deserialize<McpPermissionDto[]>(json) ?? [];

    private static DateTimeOffset ParseDate(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UtcNow;

    private static DateTimeOffset? ParseNullableDate(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}

public sealed class SqlSugarMcpPreviewStore : IMcpPreviewStore
{
    private readonly IRepository<McpPreviewRecord> _records;

    public SqlSugarMcpPreviewStore(IRepository<McpPreviewRecord> records) => _records = records;

    public async Task<McpPreviewEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _records.GetByIdAsync(id.ToString("D"), cancellationToken);
        return record is null ? null : Map(record);
    }

    public Task AddAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        => _records.AddAsync(Map(entry), cancellationToken);

    public Task<bool> UpdateAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        => _records.UpdateAsync(Map(entry), cancellationToken);

    private static McpPreviewEntry Map(McpPreviewRecord record)
        => new(
            Guid.Parse(record.Id),
            record.Operation,
            record.PrincipalId,
            Guid.TryParse(record.ProjectId, out var projectId) ? projectId : null,
            Guid.TryParse(record.FlowId, out var flowId) ? flowId : null,
            record.ExpectedVersion,
            record.PayloadJson,
            record.PreviewFingerprint,
            Enum.TryParse<McpMutationPreviewStatusDto>(record.Status, true, out var status) ? status : McpMutationPreviewStatusDto.Pending,
            DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : DateTimeOffset.UtcNow,
            DateTimeOffset.TryParse(record.ExpiresAt, out var expiresAt) ? expiresAt : DateTimeOffset.UtcNow,
            DateTimeOffset.TryParse(record.AppliedAt, out var appliedAt) ? appliedAt : null);

    private static McpPreviewRecord Map(McpPreviewEntry entry)
        => new()
        {
            Id = entry.Id.ToString("D"),
            Operation = entry.Operation,
            PrincipalId = entry.PrincipalId,
            ProjectId = entry.ProjectId?.ToString("D"),
            FlowId = entry.FlowId?.ToString("D"),
            ExpectedVersion = entry.ExpectedVersion,
            PayloadJson = entry.PayloadJson,
            PreviewFingerprint = entry.PreviewFingerprint,
            Status = entry.Status.ToString(),
            CreatedAt = entry.CreatedAt.ToString("O"),
            ExpiresAt = entry.ExpiresAt.ToString("O"),
            AppliedAt = entry.AppliedAt?.ToString("O"),
        };
}

public sealed class SqlSugarMcpAuditStore : IMcpAuditStore
{
    private readonly IRepository<McpAuditRecord> _records;

    public SqlSugarMcpAuditStore(IRepository<McpAuditRecord> records) => _records = records;

    public Task AddAsync(McpAuditEntry entry, CancellationToken cancellationToken = default)
        => _records.AddAsync(new McpAuditRecord
        {
            Id = entry.Id.ToString("D"),
            PrincipalId = entry.PrincipalId,
            Operation = entry.Operation,
            ProjectId = entry.ProjectId?.ToString("D"),
            FlowId = entry.FlowId?.ToString("D"),
            Track = entry.Track?.ToString(),
            FlowVersion = entry.FlowVersion,
            PreviewId = entry.PreviewId?.ToString("D"),
            Outcome = entry.Outcome,
            RequestHash = entry.RequestHash,
            Summary = entry.Summary,
            CreatedAt = entry.CreatedAt.ToString("O"),
            DurationMilliseconds = entry.DurationMilliseconds,
            InputBytes = entry.InputBytes,
            OutputBytes = entry.OutputBytes,
        }, cancellationToken);
}

public sealed class SqlSugarMcpIdempotencyStore : IMcpIdempotencyStore
{
    private readonly IRepository<McpIdempotencyRecord> _records;

    public SqlSugarMcpIdempotencyStore(IRepository<McpIdempotencyRecord> records) => _records = records;

    public async Task<McpIdempotencyEntry?> FindAsync(
        string principalId,
        string operation,
        string keyHash,
        CancellationToken cancellationToken = default)
        => (await _records.ListAsync(
                item => item.PrincipalId == principalId
                    && item.Operation == operation
                    && item.KeyHash == keyHash,
                cancellationToken))
            .OrderByDescending(item => item.CreatedAt, StringComparer.Ordinal)
            .Select(Map)
            .FirstOrDefault();

    public Task AddAsync(McpIdempotencyEntry entry, CancellationToken cancellationToken = default)
        => _records.AddAsync(new McpIdempotencyRecord
        {
            Id = entry.Id.ToString("D"),
            PrincipalId = entry.PrincipalId,
            Operation = entry.Operation,
            KeyHash = entry.KeyHash,
            RequestHash = entry.RequestHash,
            ResponseJson = entry.ResponseJson,
            CreatedAt = entry.CreatedAt.ToString("O"),
        }, cancellationToken);

    private static McpIdempotencyEntry Map(McpIdempotencyRecord record)
        => new(
            Guid.Parse(record.Id),
            record.PrincipalId,
            record.Operation,
            record.KeyHash,
            record.RequestHash,
            record.ResponseJson,
            DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : DateTimeOffset.UtcNow);
}
