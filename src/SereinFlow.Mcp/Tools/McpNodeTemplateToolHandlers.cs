using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;
internal static class McpNodeTemplateToolHandlers
{
    internal static async Task<object> CreateBuiltinNodeTemplateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<BuiltinNodeTemplateRequestDto>(arguments);
        if (request.ProjectId == Guid.Empty)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The built-in node template request is invalid.");
        }

        var getRequiredServiceResult1 = context.Scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        var requirePrincipalResult = context.RequirePrincipal();
        await RequireActiveProjectAsync(context.Scope, getRequiredServiceResult1, requirePrincipalResult, request.ProjectId, McpPermissionDto.ProjectRead, cancellationToken);
        try
        {
            var getRequiredServiceResult = context.Scope.ServiceProvider.GetRequiredService<BuiltinNodeTemplateService>();
            var createResult = getRequiredServiceResult.Create(request);
            return createResult;
        }
        catch (BuiltinNodeTemplateException exception)
        {
            var newGuidResult = Guid.NewGuid();
            var toStringResult = newGuidResult.ToString("N");
            var evaluatedValue = new
            {
                code = exception.Code,
                diagnosticId = toStringResult,
                fieldPath = exception.FieldPath,
                expected = exception.Expected,
                remediation = exception.Remediation,
                statusCode = exception.StatusCode
            };
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The built-in node template request is invalid.", evaluatedValue);
        }
    }
}
