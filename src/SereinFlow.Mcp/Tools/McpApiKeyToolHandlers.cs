using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;
/// <summary>
/// Admin-only API key MCP tools. The executor still owns authorization,
/// idempotency serialization and audit; this class only owns tool behavior.
/// </summary>
internal static class McpApiKeyToolHandlers
{
    internal static Task<object> ListAsync(McpToolContext context, CancellationToken cancellationToken)
    {
        var requirePrincipalResult = context.RequirePrincipal();
        var listApiKeysAsyncResult = ListApiKeysAsync(context.Scope, requirePrincipalResult, cancellationToken);
        return listApiKeysAsyncResult;
    }

    internal static Task<object> CreateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult1 = context.RequirePrincipal();
        var createApiKeyAsyncResult = CreateApiKeyAsync(context.Scope, requirePrincipalResult1, arguments, cancellationToken);
        return createApiKeyAsyncResult;
    }

    internal static Task<object> RevokeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult2 = context.RequirePrincipal();
        var revokeApiKeyAsyncResult = RevokeApiKeyAsync(context.Scope, requirePrincipalResult2, arguments, cancellationToken);
        return revokeApiKeyAsyncResult;
    }

    internal static Task<object> RotateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult3 = context.RequirePrincipal();
        var rotateApiKeyAsyncResult = RotateApiKeyAsync(context.Scope, requirePrincipalResult3, arguments, cancellationToken);
        return rotateApiKeyAsyncResult;
    }

    private static async Task<object> ListApiKeysAsync(IServiceScope scope, McpPrincipal principal, CancellationToken cancellationToken)
    {
        var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        getRequiredServiceResult.RequireAdministrator(principal);
        var getRequiredServiceResult4 = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        var evaluatedValue = (await getRequiredServiceResult4.ListAsync(cancellationToken));
        var selectResult = evaluatedValue.Select(McpSecurityService.ToDto);
        var toArrayResult = selectResult.ToArray();
        return toArrayResult;
    }

    private static async Task<object> CreateApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var getRequiredServiceResult6 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        getRequiredServiceResult6.RequireAdministrator(principal);
        var tryGetGuidResult = TryGetGuid(arguments, "projectId");
        var getRequiredStringResult = GetRequiredString(arguments, "name");
        var readPermissionsResult = ReadPermissions(arguments);
        var readOptionalDateResult = ReadOptionalDate(arguments, "expiresAt");
        var evaluatedValue7 = GetOptionalBool(arguments, "isAdministrator") ?? false;
        var request = new CreateMcpApiKeyRequestDto(tryGetGuidResult, getRequiredStringResult, readPermissionsResult, readOptionalDateResult, evaluatedValue7);
        if (request.ProjectId is not null)
        {
            var getRequiredServiceResult5 = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var project = await getRequiredServiceResult5.FindAsync(request.ProjectId.Value, cancellationToken);
            if (project is null)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The API key project was not found.");
            }

            if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "An API key cannot be bound to an archived project.");
            }
        }

        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { request, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
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
        var getRequiredServiceResult8 = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        await getRequiredServiceResult8.AddAsync(created.Entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, "mcp.key.create", idempotencyKey, created.Dto.Key, requestPayload, cancellationToken);
        return created.Dto;
    }

    private static async Task<object> RevokeApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var getRequiredServiceResult9 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        getRequiredServiceResult9.RequireAdministrator(principal);
        var keyId = GetRequiredString(arguments, "keyId");
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.revoke", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
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

    private static async Task<object> RotateApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        var keyId = GetRequiredString(arguments, "keyId");
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
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

        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
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
