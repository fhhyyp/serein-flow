using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

internal static class McpNodeTemplateToolHandlers
{
    internal static async Task<object> CreateBuiltinNodeTemplateAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<BuiltinNodeTemplateRequestDto>(arguments);
        if (request.ProjectId == Guid.Empty)
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The built-in node template request is invalid.");

        await RequireActiveProjectAsync(
            context.Scope,
            context.Scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            context.RequirePrincipal(),
            request.ProjectId,
            McpPermissionDto.ProjectRead,
            cancellationToken);

        try
        {
            return context.Scope.ServiceProvider
                .GetRequiredService<BuiltinNodeTemplateService>()
                .Create(request);
        }
        catch (BuiltinNodeTemplateException exception)
        {
            throw new McpProtocolException(
                McpProtocolErrorCodes.InvalidParams,
                "The built-in node template request is invalid.",
                new
                {
                    code = exception.Code,
                    diagnosticId = Guid.NewGuid().ToString("N"),
                    fieldPath = exception.FieldPath,
                    expected = exception.Expected,
                    remediation = exception.Remediation,
                    statusCode = exception.StatusCode
                });
        }
    }
}
