using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Stores and validates expiring MCP mutation previews before application.
/// 在应用变更前保存并校验限时有效的 MCP 预览。
/// </summary>
public sealed class McpPreviewService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();
    private readonly IMcpPreviewStore _store;
    public McpPreviewService(IMcpPreviewStore store)
    {
        _store = store;
    }

    public async Task<McpPreviewEntry> CreateAsync<T>(string operation, SereinFlowActor principal, Guid? projectId, Guid? flowId, long? expectedVersion, T payload, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var fingerprint = Fingerprint(operation, principal.Id, payloadJson);
        var id = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var entry = new McpPreviewEntry(id, operation, principal.Id, projectId, flowId, expectedVersion, payloadJson, fingerprint, McpMutationPreviewStatusDto.Pending, DateTimeOffset.UtcNow, expiresAt, null);
        await _store.AddAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<McpPreviewEntry> RequireAsync(Guid id, string fingerprint, SereinFlowActor principal, CancellationToken cancellationToken = default)
    {
        var entry = await _store.FindAsync(id, cancellationToken) ?? throw new SereinFlowAccessException(McpErrorCodes.PreviewNotFound, "The MCP preview was not found. 未找到 MCP 预览。", StatusCodes.Status404NotFound);
        if (!string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
        {
            throw new SereinFlowAccessException(McpErrorCodes.PreviewOwnerMismatch, "The MCP preview belongs to another caller. 该 MCP 预览属于其他调用方。", StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(entry.PreviewFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new SereinFlowAccessException(McpErrorCodes.PreviewFingerprintMismatch, "The MCP preview fingerprint is invalid. MCP 预览指纹无效。", StatusCodes.Status409Conflict);
        }

        if (entry.Status != McpMutationPreviewStatusDto.Pending)
        {
            throw new SereinFlowAccessException(McpErrorCodes.PreviewNotPending, "The MCP preview is no longer pending. MCP 预览已不处于待处理状态。", StatusCodes.Status409Conflict);
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expired = entry with
            {
                Status = McpMutationPreviewStatusDto.Expired
            };
            await _store.UpdateAsync(expired, cancellationToken);
            throw new SereinFlowAccessException(McpErrorCodes.PreviewExpired, "The MCP preview has expired. MCP 预览已过期。", StatusCodes.Status409Conflict);
        }

        return entry;
    }

    public Task<bool> MarkAppliedAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
    {
        return _store.UpdateAsync(entry with { Status = McpMutationPreviewStatusDto.Applied, AppliedAt = DateTimeOffset.UtcNow }, cancellationToken);
    }

    public static string Fingerprint(string operation, string principalId, string payloadJson)
    {
        var input = Encoding.UTF8.GetBytes($"{operation}\n{principalId}\n{payloadJson}");
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    public static T Deserialize<T>(McpPreviewEntry entry)
    {
        return JsonSerializer.Deserialize<T>(entry.PayloadJson, JsonOptions) ?? throw new InvalidOperationException("The stored MCP preview payload is invalid. 存储的 MCP 预览载荷无效。");
    }
}
