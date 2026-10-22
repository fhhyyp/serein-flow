
namespace SereinFlow.Mcp;

[McpBindProperties]
internal class McpReadOptionsRequest
{
    public int MaxItems { get; set; } = 200;
    public int MaxJsonBytes { get; set; } = 64 * 1024;
    public bool IncludeFlowLiteralValues { get; set; } = false;
    public bool IncludeScriptSource { get; set; } = false;
}
