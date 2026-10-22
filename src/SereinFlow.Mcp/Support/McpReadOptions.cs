using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Support;

internal static class McpReadOptions
{
    internal static AiReadModelOptions ReadOptions(McpReadOptionsRequest arguments) => new(arguments.MaxItems, arguments.MaxJsonBytes, arguments.IncludeFlowLiteralValues, arguments.IncludeScriptSource);
}
