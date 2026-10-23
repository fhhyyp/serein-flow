using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Prevents duplicate MCP mutations by tracking idempotency keys.
/// 通过跟踪幂等键避免重复执行 MCP 变更。
/// </summary>
public sealed class McpIdempotencyService
{
    private readonly IMcpIdempotencyStore _store;
    public McpIdempotencyService(IMcpIdempotencyStore store)
    {
        _store = store;
    }

    public static string HashKey(string key)
    {
        var normalizedKey = key.Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey))).ToLowerInvariant();
    }

    public Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string key, CancellationToken cancellationToken = default)
    {
        return FindCoreAsync(principalId, operation, key, null, cancellationToken);
    }

    public Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string key, string requestPayload, CancellationToken cancellationToken = default)
    {
        return FindCoreAsync(principalId, operation, key, requestPayload, cancellationToken);
    }

    private async Task<McpIdempotencyEntry?> FindCoreAsync(string principalId, string operation, string key, string? requestPayload, CancellationToken cancellationToken)
    {
        var keyHash = HashKey(key);
        var entry = await _store.FindAsync(principalId, operation, keyHash, cancellationToken);
        if (entry is not null && !string.IsNullOrWhiteSpace(entry.RequestHash) && requestPayload is not null && !RequestHashMatches())
        {
            throw new SereinFlowAccessException(McpErrorCodes.IdempotencyConflict, "The idempotency key was already used for a different MCP request. 该幂等键已用于另一个不同的 MCP 请求。", StatusCodes.Status409Conflict);
        }

        return entry;
        bool RequestHashMatches()
        {
            var hashKey = HashKey(requestPayload);
            return string.Equals(entry.RequestHash, hashKey, StringComparison.Ordinal);
        }
    }

    public async Task<T?> FindResponseAsync<T>(string principalId, string operation, string key, CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(principalId, operation, key, cancellationToken);
        return entry is null ? default : DeserializeStoredResponse();
        T? DeserializeStoredResponse()
        {
            var contractOptions = SereinJsonSerialization.CreateContractOptions();
            return JsonSerializer.Deserialize<T>(entry.ResponseJson, contractOptions);
        }
    }

    public Task SaveAsync<T>(string principalId, string operation, string key, T response, CancellationToken cancellationToken = default)
    {
        return SaveCoreAsync(principalId, operation, key, response, null, cancellationToken);
    }

    public Task SaveAsync<T>(string principalId, string operation, string key, T response, string requestPayload, CancellationToken cancellationToken = default)
    {
        return SaveCoreAsync(principalId, operation, key, response, requestPayload, cancellationToken);
    }

    private Task SaveCoreAsync<T>(string principalId, string operation, string key, T response, string? requestPayload, CancellationToken cancellationToken)
    {
        var mcpIdempotencyEntry = new McpIdempotencyEntry(Guid.NewGuid(), principalId, operation, HashKey(key), (requestPayload is null ? string.Empty : HashKey(requestPayload)), JsonSerializer.Serialize(response, SereinJsonSerialization.CreateContractOptions()), DateTimeOffset.UtcNow);
        return _store.AddAsync(mcpIdempotencyEntry, cancellationToken);
    }
}
