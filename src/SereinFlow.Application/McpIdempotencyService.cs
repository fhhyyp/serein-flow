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
        var trimResult = key.Trim();
        var getBytesResult = Encoding.UTF8.GetBytes(trimResult);
        var hashDataResult = SHA256.HashData(getBytesResult);
        var toHexStringResult = Convert.ToHexString(hashDataResult);
        var toLowerInvariantResult = toHexStringResult.ToLowerInvariant();
        return toLowerInvariantResult;
    }

    public Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string key, CancellationToken cancellationToken = default)
    {
        var findCoreAsyncResult = FindCoreAsync(principalId, operation, key, null, cancellationToken);
        return findCoreAsyncResult;
    }

    public Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string key, string requestPayload, CancellationToken cancellationToken = default)
    {
        var findCoreAsyncResult1 = FindCoreAsync(principalId, operation, key, requestPayload, cancellationToken);
        return findCoreAsyncResult1;
    }

    private async Task<McpIdempotencyEntry?> FindCoreAsync(string principalId, string operation, string key, string? requestPayload, CancellationToken cancellationToken)
    {
        var hashKeyResult = HashKey(key);
        var entry = await _store.FindAsync(principalId, operation, hashKeyResult, cancellationToken);
        if (entry is not null && !string.IsNullOrWhiteSpace(entry.RequestHash) && requestPayload is not null && !resolveEqualsResult())
        {
            throw new SereinFlowAccessException(McpErrorCodes.IdempotencyConflict, "The idempotency key was already used for a different MCP request. 该幂等键已用于另一个不同的 MCP 请求。", StatusCodes.Status409Conflict);
        }

        return entry;
        bool resolveEqualsResult()
        {
            var hashKeyResult2 = HashKey(requestPayload);
            var equalsResult = string.Equals(entry.RequestHash, hashKeyResult2, StringComparison.Ordinal);
            return equalsResult;
        }
    }

    public async Task<T?> FindResponseAsync<T>(string principalId, string operation, string key, CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(principalId, operation, key, cancellationToken);
        T? evaluatedValue = entry is null ? default : resolveDeserializeResult();
        return evaluatedValue;
        T? resolveDeserializeResult()
        {
            var createContractOptionsResult = SereinJsonSerialization.CreateContractOptions();
            var deserializeResult = JsonSerializer.Deserialize<T>(entry.ResponseJson, createContractOptionsResult);
            return deserializeResult;
        }
    }

    public Task SaveAsync<T>(string principalId, string operation, string key, T response, CancellationToken cancellationToken = default)
    {
        var saveCoreAsyncResult = SaveCoreAsync(principalId, operation, key, response, null, cancellationToken);
        return saveCoreAsyncResult;
    }

    public Task SaveAsync<T>(string principalId, string operation, string key, T response, string requestPayload, CancellationToken cancellationToken = default)
    {
        var saveCoreAsyncResult3 = SaveCoreAsync(principalId, operation, key, response, requestPayload, cancellationToken);
        return saveCoreAsyncResult3;
    }

    private Task SaveCoreAsync<T>(string principalId, string operation, string key, T response, string? requestPayload, CancellationToken cancellationToken)
    {
        var newGuidResult = Guid.NewGuid();
        var hashKeyResult4 = HashKey(key);
        string evaluatedValue5 = requestPayload is null ? string.Empty : HashKey(requestPayload);
        var createContractOptionsResult6 = SereinJsonSerialization.CreateContractOptions();
        var serializeResult = JsonSerializer.Serialize(response, createContractOptionsResult6);
        var mcpIdempotencyEntry = new McpIdempotencyEntry(newGuidResult, principalId, operation, hashKeyResult4, evaluatedValue5, serializeResult, DateTimeOffset.UtcNow);
        var addAsyncResult = _store.AddAsync(mcpIdempotencyEntry, cancellationToken);
        return addAsyncResult;
    }
}
