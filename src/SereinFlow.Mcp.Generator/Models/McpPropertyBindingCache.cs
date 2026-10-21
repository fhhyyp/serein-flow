namespace SereinFlow.Mcp.Generator.Models;

/// <summary>声明式请求属性的 JSON 绑定模型。</summary>
internal sealed class McpPropertyBindingCache
{
    public string PropertyName { get; set; } = string.Empty;
    public string WireNameExpression { get; set; } = string.Empty;
    public string ParameterExpression { get; set; } = string.Empty;
    public string ReadExpression { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool IgnoreWhitespace { get; set; }
}
