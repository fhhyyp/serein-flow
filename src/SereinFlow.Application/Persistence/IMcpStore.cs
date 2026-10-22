using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;
/// <summary>
/// Represents MCP API key entry data used by SereinFlow.
/// 表示 SereinFlow 使用的MCPAPI密钥入口数据。
/// </summary>
/// <summary>
/// Represents MCP preview entry data used by SereinFlow.
/// 表示 SereinFlow 使用的MCP预览入口数据。
/// </summary>
public sealed record McpPreviewEntry(Guid Id, string Operation, string PrincipalId, Guid? ProjectId, Guid? FlowId, long? ExpectedVersion, string PayloadJson, string PreviewFingerprint, McpMutationPreviewStatusDto Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AppliedAt);
/// <summary>
/// Represents MCP audit entry data used by SereinFlow.
/// 表示 SereinFlow 使用的MCP审计入口数据。
/// </summary>
public sealed record McpAuditEntry(Guid Id, string PrincipalId, string Operation, Guid? ProjectId, Guid? FlowId, FlowVersionTrackDto? Track, long? FlowVersion, Guid? PreviewId, string Outcome, string? RequestHash, string? Summary, DateTimeOffset CreatedAt, long DurationMilliseconds, long InputBytes = 0, long OutputBytes = 0);
/// <summary>
/// Represents MCP idempotency entry data used by SereinFlow.
/// 表示 SereinFlow 使用的MCP幂等入口数据。
/// </summary>
public sealed record McpIdempotencyEntry(Guid Id, string PrincipalId, string Operation, string KeyHash, string RequestHash, string ResponseJson, DateTimeOffset CreatedAt);
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
    Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string keyHash, CancellationToken cancellationToken = default);
    Task AddAsync(McpIdempotencyEntry entry, CancellationToken cancellationToken = default);
}
