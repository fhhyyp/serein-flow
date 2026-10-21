using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Tools.McpToolSupport;

namespace SereinFlow.Mcp.Tools;
internal static class McpNodeTemplateToolHandlers
{
    [SereinFlowMcpTool("sereinflow_create_builtin_node_template", McpPermissionDto.ProjectRead)]
    internal static async Task<object> CreateBuiltinNodeTemplateAsync(SereinFlowMcpToolContext context, BuiltinNodeTemplateRequestDto arguments, CancellationToken cancellationToken)
    {
        var request = arguments;
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
