using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace SereinFlow.Mcp.Generator.Models;

/// <summary>工具语义分析结果；生成阶段只读取缓存，不再分析方法符号。</summary>
internal sealed class McpToolCache
{
    public string Name { get; set; } = string.Empty;
    public string HandlerTypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public string? RequestTypeName { get; set; }
    public ITypeSymbol? RequestType { get; set; }
    public string? ValidationMethodName { get; set; }
    public string? BindingMethodName { get; set; }
    public string MetadataExpression { get; set; } = string.Empty;
    public string InvocationArguments { get; set; } = string.Empty;
    public string ConstructorArguments { get; set; } = string.Empty;
    public Location? Location { get; set; }
    public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
}
