using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

public sealed record McpApiKeyEntry(
    string Id,
    Guid? ProjectId,
    string Name,
    string KeyPrefix,
    string SecretHash,
    string Salt,
    IReadOnlyList<McpPermissionDto> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt,
    bool IsAdministrator = false);

public sealed record McpPreviewEntry(
    Guid Id,
    string Operation,
    string PrincipalId,
    Guid? ProjectId,
    Guid? FlowId,
    long? ExpectedVersion,
    string PayloadJson,
    string PreviewFingerprint,
    McpMutationPreviewStatusDto Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AppliedAt);

public sealed record McpAuditEntry(
    Guid Id,
    string PrincipalId,
    string Operation,
    Guid? ProjectId,
    Guid? FlowId,
    FlowVersionTrackDto? Track,
    long? FlowVersion,
    Guid? PreviewId,
    string Outcome,
    string? RequestHash,
    string? Summary,
    DateTimeOffset CreatedAt,
    long DurationMilliseconds,
    long InputBytes = 0,
    long OutputBytes = 0);

public sealed record McpIdempotencyEntry(
    Guid Id,
    string PrincipalId,
    string Operation,
    string KeyHash,
    string RequestHash,
    string ResponseJson,
    DateTimeOffset CreatedAt);

public interface IMcpApiKeyStore
{
    Task<IReadOnlyList<McpApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<McpApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default);
}

public interface IMcpPreviewStore
{
    Task<McpPreviewEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default);
}

public interface IMcpAuditStore
{
    Task AddAsync(McpAuditEntry entry, CancellationToken cancellationToken = default);
}

public interface IMcpIdempotencyStore
{
    Task<McpIdempotencyEntry?> FindAsync(
        string principalId,
        string operation,
        string keyHash,
        CancellationToken cancellationToken = default);

    Task AddAsync(McpIdempotencyEntry entry, CancellationToken cancellationToken = default);
}
