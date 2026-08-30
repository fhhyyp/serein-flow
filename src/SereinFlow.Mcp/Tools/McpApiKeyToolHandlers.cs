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
        => ListApiKeysAsync(context.Scope, context.RequirePrincipal(), cancellationToken);

    internal static Task<object> CreateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => CreateApiKeyAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> RevokeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => RevokeApiKeyAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> RotateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => RotateApiKeyAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    private static async Task<object> ListApiKeysAsync(IServiceScope scope, McpPrincipal principal, CancellationToken cancellationToken)
    {
        scope.ServiceProvider.GetRequiredService<McpSecurityService>().RequireAdministrator(principal);
        return (await scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>().ListAsync(cancellationToken))
            .Select(McpSecurityService.ToDto)
            .ToArray();
    }

    private static async Task<object> CreateApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        scope.ServiceProvider.GetRequiredService<McpSecurityService>().RequireAdministrator(principal);
        var request = new CreateMcpApiKeyRequestDto(
            TryGetGuid(arguments, "projectId"),
            GetRequiredString(arguments, "name"),
            ReadPermissions(arguments),
            ReadOptionalDate(arguments, "expiresAt"),
            GetOptionalBool(arguments, "isAdministrator") ?? false);
        if (request.ProjectId is not null)
        {
            var project = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
                .FindAsync(request.ProjectId.Value, cancellationToken);
            if (project is null)
                throw new McpProtocolException(-32004, "The API key project was not found.");
            if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
                throw new McpProtocolException(-32011, "An API key cannot be bound to an archived project.");
        }
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { request, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.create", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var key = JsonSerializer.Deserialize<McpApiKeyDto>(replay.ResponseJson, ContractJsonOptions)
                ?? throw new McpProtocolException(-32603, "The stored API key idempotency response is invalid.");
            return new { key, secret = (string?)null, replayed = true };
        }
        var created = McpSecurityService.CreateKeyWithEntry(request);
        await scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>().AddAsync(created.Entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, "mcp.key.create", idempotencyKey, created.Dto.Key, requestPayload, cancellationToken);
        return created.Dto;
    }

    private static async Task<object> RevokeApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        scope.ServiceProvider.GetRequiredService<McpSecurityService>().RequireAdministrator(principal);
        var keyId = GetRequiredString(arguments, "keyId");
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.revoke", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        var entry = await store.FindAsync(keyId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The MCP API key was not found.");
        var revoked = entry with { RevokedAt = DateTimeOffset.UtcNow };
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
            var replayed = JsonSerializer.Deserialize<RotatedMcpApiKeyDto>(replay.ResponseJson, ContractJsonOptions)
                ?? throw new McpProtocolException(-32603, "The stored API key rotation response is invalid.");
            return replayed with { Secret = null, Replayed = true };
        }

        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        var existing = await store.FindAsync(keyId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The MCP API key was not found.");
        if (existing.RevokedAt is not null)
            throw new McpProtocolException(-32011, "The MCP API key has already been revoked.");

        var replacement = McpSecurityService.RotateKeyWithEntry(existing);
        await store.AddAsync(replacement.Entry, cancellationToken);
        await store.UpdateAsync(existing with { RevokedAt = DateTimeOffset.UtcNow }, cancellationToken);
        var response = new RotatedMcpApiKeyDto(existing.Id, replacement.Dto.Key, replacement.Dto.Secret);
        await idempotency.SaveAsync(
            principal.Id,
            "mcp.key.rotate",
            idempotencyKey,
            response with { Secret = null },
            requestPayload,
            cancellationToken);
        return response;
    }

}
