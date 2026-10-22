using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpJson;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpApiKeyTools(
    IMcpApiKeyStore store,
    IProjectRepository projects,
    McpIdempotencyService idempotency,
    McpSecurityService security)
{
    [SereinFlowMcpTool("sereinflow_list_mcp_api_keys", McpPermissionDto.McpKeysManage)]
    internal async Task<McpApiKeyDto[]> ListAsync(SereinFlowMcpCaller context, CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        security.RequireAdministrator(principal);
        return ((await store.ListAsync(cancellationToken))).Select(McpSecurityService.ToDto).ToArray();
    }

    [SereinFlowMcpTool("sereinflow_create_mcp_api_key", McpPermissionDto.McpKeysManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> CreateAsync(
        SereinFlowMcpCaller context,
        McpCreateApiKeyRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        security.RequireAdministrator(principal);
        var request = new CreateMcpApiKeyRequestDto(arguments.ProjectId, arguments.Name, arguments.Permissions.Distinct().ToArray(), arguments.ExpiresAt, arguments.IsAdministrator);
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
                secret = (string?)null,
                replayed = true
            };
        }

        var created = McpSecurityService.CreateKeyWithEntry(request);
        await store.AddAsync(created.Entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, "mcp.key.create", idempotencyKey, created.Dto.Key, requestPayload, cancellationToken);
        return created.Dto;
    }

    [SereinFlowMcpTool("sereinflow_revoke_mcp_api_key", McpPermissionDto.McpKeysManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> RevokeAsync(
        SereinFlowMcpCaller context,
        RevokeMcpApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        security.RequireAdministrator(principal);
        var keyId = request.KeyId.Trim();
        var idempotencyKey = request.IdempotencyKey.Trim();
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.revoke", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
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

    [SereinFlowMcpTool("sereinflow_rotate_mcp_api_key", McpPermissionDto.McpKeysManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> RotateAsync(
        SereinFlowMcpCaller context,
        McpRotateApiKeyRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
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
