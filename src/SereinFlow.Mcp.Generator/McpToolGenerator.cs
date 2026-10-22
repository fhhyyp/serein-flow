using Microsoft.CodeAnalysis;
using Serein.Generator.Framework;
using SereinFlow.Mcp.Generator.Extensions;

namespace SereinFlow.Mcp.Generator;

/// <summary>
/// Declares tool aggregation and host output; the framework owns discovery and result routing.
/// 声明工具聚合及宿主输出；框架负责发现和结果路由。
/// </summary>
[Generator]
public sealed partial class McpToolGenerator : FrameworkGenerator
{
    protected override void Configure(GeneratorBuilder builder)
    {
        builder.AddInitializationSource("McpToolkits.g.cs", _attributeText);
        builder.AddInitializationSource("SereinFlowMcpToolkits.g.cs", _sereinFlowAttributeText);

        builder.ForMethodAttribute("SereinFlow.Mcp.Framework.McpToolAttribute", AttributeMatch.IncludeDerived)
            .Analyze(McpToolAnalyzer.Analyze)
            .Named("McpTools")
            .Collect()
            .UniqueBy(tool => tool.Name, McpToolRegistryExtension.Duplicate, tool => tool.Location)
            .EmitWithAssemblyName((tools, assemblyName, output) =>
            {
                var code = tools.GenerateRegistry(assemblyName, output);
                output.AddSource("McpToolRegistry.g.cs", code);
            }, "McpRegistryOutput", "McpHost");

        builder.ForClassAttribute("SereinFlow.Mcp.Framework.McpHostAttribute")
            .Require(McpHostExtension.InvalidHost, TypeRules.TopLevel, TypeRules.NonGeneric, TypeRules.Static, TypeRules.Partial,
                McpHostAnalyzer.RegistrationName)
            .Analyze(McpHostAnalyzer.Analyze)
            .Emit((host, output) =>
            {
                var code = host.GenerateCode(output);
                output.AddSource(host.HintName, code);
            }, "McpHostOutput");
    }
}
