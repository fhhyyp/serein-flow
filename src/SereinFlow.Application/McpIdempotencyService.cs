using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed class McpIdempotencyService
{
    private readonly IMcpIdempotencyStore _store;

    public McpIdempotencyService(IMcpIdempotencyStore store) => _store = store;

    public static string HashKey(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()))).ToLowerInvariant();

    public Task<McpIdempotencyEntry?> FindAsync(
        string principalId,
        string operation,
        string key,
        CancellationToken cancellationToken = default)
        => FindCoreAsync(principalId, operation, key, null, cancellationToken);

    public Task<McpIdempotencyEntry?> FindAsync(
        string principalId,
        string operation,
        string key,
        string requestPayload,
        CancellationToken cancellationToken = default)
        => FindCoreAsync(principalId, operation, key, requestPayload, cancellationToken);

    private async Task<McpIdempotencyEntry?> FindCoreAsync(
        string principalId,
        string operation,
        string key,
        string? requestPayload,
        CancellationToken cancellationToken)
    {
        var entry = await _store.FindAsync(principalId, operation, HashKey(key), cancellationToken);
        if (entry is not null
            && !string.IsNullOrWhiteSpace(entry.RequestHash)
            && requestPayload is not null
            && !string.Equals(entry.RequestHash, HashKey(requestPayload), StringComparison.Ordinal))
        {
            throw new McpSecurityException(
                "mcp.idempotency_conflict",
                "The idempotency key was already used for a different MCP request.",
                409);
        }
        return entry;
    }

    public async Task<T?> FindResponseAsync<T>(
        string principalId,
        string operation,
        string key,
        CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(principalId, operation, key, cancellationToken);
        return entry is null
            ? default
            : JsonSerializer.Deserialize<T>(entry.ResponseJson, SereinJsonSerialization.CreateWebOptions());
    }

    public Task SaveAsync<T>(
        string principalId,
        string operation,
        string key,
        T response,
        CancellationToken cancellationToken = default)
        => SaveCoreAsync(principalId, operation, key, response, null, cancellationToken);

    public Task SaveAsync<T>(
        string principalId,
        string operation,
        string key,
        T response,
        string requestPayload,
        CancellationToken cancellationToken = default)
        => SaveCoreAsync(principalId, operation, key, response, requestPayload, cancellationToken);

    private Task SaveCoreAsync<T>(
        string principalId,
        string operation,
        string key,
        T response,
        string? requestPayload,
        CancellationToken cancellationToken)
        => _store.AddAsync(
            new McpIdempotencyEntry(
                Guid.NewGuid(),
                principalId,
                operation,
                HashKey(key),
                requestPayload is null ? string.Empty : HashKey(requestPayload),
                JsonSerializer.Serialize(response, SereinJsonSerialization.CreateWebOptions()),
                DateTimeOffset.UtcNow),
            cancellationToken);
}
