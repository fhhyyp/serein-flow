using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpScriptTools(ScriptAdapter.ISereinLangCompiler _sereinLangCompiler)
{
    [SereinFlowMcpTool("sereinflow_compile_sereinlang", SereinFlowPermission.ScriptCompile)]
    internal async Task<ScriptCompileResultDto> CompileScriptAsync(
        SereinFlowMcpCaller context,
        ScriptCompileRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var request = arguments;
        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinLang source is required. 必须提供 SereinLang 源码。");
        }

        return await _sereinLangCompiler.CompileAsync(request, cancellationToken);
    }
}
