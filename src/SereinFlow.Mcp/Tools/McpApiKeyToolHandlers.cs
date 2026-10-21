using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Tools.McpToolSupport;

namespace SereinFlow.Mcp.Tools;
/// <summary>
/// Admin-only API key MCP tools. The executor still owns authorization,
/// idempotency serialization and audit; this class only owns tool behavior.
/// </summary>
internal sealed class McpApiKeyToolHandlers(McpSecurityService security, IMcpApiKeyStore store,
    IProjectRepository projects, McpIdempotencyService idempotency)
{
    [SereinFlowMcpTool("sereinflow_list_mcp_api_keys", McpPermissionDto.McpKeysManage)]
    internal Task<object> ListAsync(SereinFlowMcpCaller context, CancellationToken cancellationToken)
    {
        var requirePrincipalResult = context.RequirePrincipal();
        var listApiKeysAsyncResult = ListApiKeysAsync(requirePrincipalResult, cancellationToken);
        return listApiKeysAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_create_mcp_api_key", McpPermissionDto.McpKeysManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal Task<object> CreateAsync(SereinFlowMcpCaller context, McpCreateApiKeyRequest arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult1 = context.RequirePrincipal();
        var createApiKeyAsyncResult = CreateApiKeyAsync(requirePrincipalResult1, arguments, cancellationToken);
        return createApiKeyAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_revoke_mcp_api_key", McpPermissionDto.McpKeysManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal Task<object> RevokeAsync(SereinFlowMcpCaller context, RevokeMcpApiKeyRequest request, CancellationToken cancellationToken)
    {
        var requirePrincipalResult2 = context.RequirePrincipal();
        var revokeApiKeyAsyncResult = RevokeApiKeyAsync(requirePrincipalResult2, request, cancellationToken);
        return revokeApiKeyAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_rotate_mcp_api_key", McpPermissionDto.McpKeysManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal Task<object> RotateAsync(SereinFlowMcpCaller context, McpRotateApiKeyRequest arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult3 = context.RequirePrincipal();
        var rotateApiKeyAsyncResult = RotateApiKeyAsync(requirePrincipalResult3, arguments, cancellationToken);
        return rotateApiKeyAsyncResult;
    }

    private async Task<object> ListApiKeysAsync(McpPrincipal principal, CancellationToken cancellationToken)
    {
        security.RequireAdministrator(principal);
        var evaluatedValue = (await store.ListAsync(cancellationToken));
        var selectResult = evaluatedValue.Select(McpSecurityService.ToDto);
        var toArrayResult = selectResult.ToArray();
        return toArrayResult;
    }

    private async Task<object> CreateApiKeyAsync(McpPrincipal principal, McpCreateApiKeyRequest arguments, CancellationToken cancellationToken)
    {
        security.RequireAdministrator(principal);
        var tryGetGuidResult = arguments.ProjectId;
        var getRequiredStringResult = arguments.Name;
        var readPermissionsResult = arguments.Permissions.Distinct().ToArray();
        var readOptionalDateResult = arguments.ExpiresAt;
        var evaluatedValue7 = arguments.IsAdministrator;
        var request = new CreateMcpApiKeyRequestDto(tryGetGuidResult, getRequiredStringResult, readPermissionsResult, readOptionalDateResult, evaluatedValue7);
        if (request.ProjectId is not null)
        {
            var project = await projects.FindAsync(request.ProjectId.Value, cancellationToken);
            if (project is null)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The API key project was not found.");
            }

            if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "An API key cannot be bound to an archived project.");
            }
        }

        var idempotencyKey = arguments.IdempotencyKey;
        var requestPayload = Serialize(new { request, idempotencyKey });
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.create", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var key = JsonSerializer.Deserialize<McpApiKeyDto>(replay.ResponseJson, ContractJsonOptions) ?? throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The stored API key idempotency response is invalid.");
            return new
            {
                key,
                secret = (string? )null,
                replayed = true
            };
        }

        var created = McpSecurityService.CreateKeyWithEntry(request);
        await store.AddAsync(created.Entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, "mcp.key.create", idempotencyKey, created.Dto.Key, requestPayload, cancellationToken);
        return created.Dto;
    }

    private async Task<object> RevokeApiKeyAsync(McpPrincipal principal, RevokeMcpApiKeyRequest request, CancellationToken cancellationToken)
    {
        security.RequireAdministrator(principal);
        var keyId = request.KeyId.Trim();
        var idempotencyKey = request.IdempotencyKey.Trim();
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.revoke", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var entry = await store.FindAsync(keyId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP API key was not found.");
        var revoked = entry with
        {
            RevokedAt = DateTimeOffset.UtcNow
        };
        await store.UpdateAsync(revoked, cancellationToken);
        var response = McpSecurityService.ToDto(revoked);
        await idempotency.SaveAsync(principal.Id, "mcp.key.revoke", idempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private async Task<object> RotateApiKeyAsync(McpPrincipal principal, McpRotateApiKeyRequest arguments, CancellationToken cancellationToken)
    {
        security.RequireAdministrator(principal);
        var keyId = arguments.KeyId;
        var idempotencyKey = arguments.IdempotencyKey;
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.rotate", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var replayed = JsonSerializer.Deserialize<RotatedMcpApiKeyDto>(replay.ResponseJson, ContractJsonOptions) ?? throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The stored API key rotation response is invalid.");
            return replayed with
            {
                Secret = null,
                Replayed = true
            };
        }

        var existing = await store.FindAsync(keyId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP API key was not found.");
        if (existing.RevokedAt is not null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The MCP API key has already been revoked.");
        }

        var replacement = McpSecurityService.RotateKeyWithEntry(existing);
        await store.AddAsync(replacement.Entry, cancellationToken);
        await store.UpdateAsync(existing with { RevokedAt = DateTimeOffset.UtcNow }, cancellationToken);
        var response = new RotatedMcpApiKeyDto(existing.Id, replacement.Dto.Key, replacement.Dto.Secret);
        await idempotency.SaveAsync(principal.Id, "mcp.key.rotate", idempotencyKey, response with { Secret = null }, requestPayload, cancellationToken);
        return response;
    }
}

/// <summary>
/// Carries request data for revoke MCP API key operations.
/// 承载RevokeMCPAPI密钥操作的请求数据。
/// </summary>
internal sealed record RevokeMcpApiKeyRequest(
    [property: Required] string KeyId,
    [property: Required] string IdempotencyKey);
