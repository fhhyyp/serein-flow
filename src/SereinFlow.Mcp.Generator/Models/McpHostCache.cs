using Microsoft.CodeAnalysis;

namespace SereinFlow.Mcp.Generator.Models;

/// <summary>宿主注册入口及业务配置钩子的生成模型。</summary>
internal sealed class McpHostCache
{
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Accessibility { get; set; } = string.Empty;
    public string RegistrationMethodName { get; set; } = string.Empty;
    public string RegistryTypeName { get; set; } = string.Empty;
    public string? Error { get; set; }
    public Location? Location { get; set; }
    public string HintName => Namespace + "." + ClassName + ".g.cs";
}
