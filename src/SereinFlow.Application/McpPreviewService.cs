using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed class McpPreviewService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();
    private readonly IMcpPreviewStore _store;

    public McpPreviewService(IMcpPreviewStore store) => _store = store;

    public async Task<McpPreviewEntry> CreateAsync<T>(
        string operation,
        McpPrincipal principal,
        Guid? projectId,
        Guid? flowId,
        long? expectedVersion,
        T payload,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var fingerprint = Fingerprint(operation, principal.Id, payloadJson);
        var entry = new McpPreviewEntry(
            Guid.NewGuid(),
            operation,
            principal.Id,
            projectId,
            flowId,
            expectedVersion,
            payloadJson,
            fingerprint,
            McpMutationPreviewStatusDto.Pending,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(10),
            null);
        await _store.AddAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<McpPreviewEntry> RequireAsync(
        Guid id,
        string fingerprint,
        McpPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var entry = await _store.FindAsync(id, cancellationToken)
            ?? throw new McpSecurityException("mcp.preview_not_found", "The MCP preview was not found.", 404);
        if (!string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
            throw new McpSecurityException("mcp.preview_owner_mismatch", "The MCP preview belongs to another caller.", 403);
        if (!string.Equals(entry.PreviewFingerprint, fingerprint, StringComparison.Ordinal))
            throw new McpSecurityException("mcp.preview_fingerprint_mismatch", "The MCP preview fingerprint is invalid.", 409);
        if (entry.Status != McpMutationPreviewStatusDto.Pending)
            throw new McpSecurityException("mcp.preview_not_pending", "The MCP preview is no longer pending.", 409);
        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expired = entry with { Status = McpMutationPreviewStatusDto.Expired };
            await _store.UpdateAsync(expired, cancellationToken);
            throw new McpSecurityException("mcp.preview_expired", "The MCP preview has expired.", 409);
        }
        return entry;
    }

    public Task<bool> MarkAppliedAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        => _store.UpdateAsync(entry with
        {
            Status = McpMutationPreviewStatusDto.Applied,
            AppliedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

    public static string Fingerprint(string operation, string principalId, string payloadJson)
    {
        var input = Encoding.UTF8.GetBytes($"{operation}\n{principalId}\n{payloadJson}");
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    public static T Deserialize<T>(McpPreviewEntry entry)
        => JsonSerializer.Deserialize<T>(entry.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("The stored MCP preview payload is invalid.");
}
