using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Support;
using static SereinFlow.Mcp.Support.McpErrors;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpNodeTemplateTools(
    BuiltinNodeTemplateService _builtinNodeTemplateService,
    LibraryNodeTemplateService _libraryNodeTemplateService,
    McpResourceAuthorization _mcpResourceAuthorization)
{
    [SereinFlowMcpTool("sereinflow_create_library_node_template", McpPermissionDto.ProjectRead)]
    internal async Task<LibraryNodeTemplateDto> CreateNodeTemplateAsync(
        SereinFlowMcpCaller context,
        LibraryNodeTemplateRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        if (request.ProjectId == Guid.Empty)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The library node template request is invalid.");
        }

        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, request.ProjectId, McpPermissionDto.ProjectRead, cancellationToken);
        try
        {
            return await _libraryNodeTemplateService.CreateAsync(request, cancellationToken);
        }
        catch (LibraryNodeTemplateException exception)
        {
            throw InvalidLibraryNodeTemplate(exception);
        }
    }

    [SereinFlowMcpTool("sereinflow_create_builtin_node_template", McpPermissionDto.ProjectRead)]
    internal async Task<BuiltinNodeTemplateDto> CreateBuiltinNodeTemplateAsync(
        SereinFlowMcpCaller context,
        BuiltinNodeTemplateRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var request = arguments;
        if (request.ProjectId == Guid.Empty)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The built-in node template request is invalid.");
        }

        var principal = context.RequirePrincipal();
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, request.ProjectId, McpPermissionDto.ProjectRead, cancellationToken);
        try
        {
            return _builtinNodeTemplateService.Create(request);
        }
        catch (BuiltinNodeTemplateException exception)
        {
            var toString = Guid.NewGuid().ToString("N");
            var value = new
            {
                code = exception.Code,
                diagnosticId = toString,
                fieldPath = exception.FieldPath,
                expected = exception.Expected,
                remediation = exception.Remediation,
                statusCode = exception.StatusCode
            };
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The built-in node template request is invalid.", value);
        }
    }
}
