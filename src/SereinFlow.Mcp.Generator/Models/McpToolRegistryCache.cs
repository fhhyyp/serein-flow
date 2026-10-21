using System.Collections.Immutable;
using System.Collections.Generic;

namespace SereinFlow.Mcp.Generator.Models;

/// <summary>当前程序集的工具目录生成模型。</summary>
internal sealed class McpToolRegistryCache(string assemblyName, ImmutableArray<McpToolCache> tools)
{
    public string AssemblyName { get; } = assemblyName;
    public ImmutableArray<McpToolCache> Tools { get; } = tools;
    public List<McpValidationCache> Validations { get; } = new List<McpValidationCache>();
}
