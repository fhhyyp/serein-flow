using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator.Extensions;

/// <summary>通过强类型属性访问生成嵌套校验，避免运行时枚举 PropertyInfo。</summary>
internal static class McpValidationExtension
{
    internal static void GeneratorValidations(this GeneratorCache<McpToolRegistryCache> generator)
    {
        foreach (var validation in generator.Cache.Validations)
        {
            generator.AppendCode($"private static void {validation.MethodName}({validation.TypeName} value, string path, global::System.Collections.Generic.HashSet<object> visited)");
            generator.AppendCodeBlok(() =>
            {
                generator.AppendCode("if (!global::SereinFlow.Mcp.Framework.McpRequestBinder.ValidateObject(value, path, visited)) return;");
                for (var index = 0; index < validation.Properties.Count; index++)
                {
                    var property = validation.Properties[index];
                    generator.AppendCode($"if (value.@{property.PropertyName} is {{ }} child{index})");
                    generator.AppendCodeBlok(() =>
                    {
                        if (!property.IsCollection)
                        {
                            generator.AppendCode($"{property.MethodName}(child{index}, path + \".{property.PropertyName}\", visited);");
                            return;
                        }
                        generator.AppendCode($"var index{index} = 0;");
                        generator.AppendCode($"foreach (var item in child{index}{(property.IsDictionary ? ".Values" : "")})");
                        generator.AppendCodeBlok(() =>
                        {
                            generator.AppendCode($"if (item is not null) {property.MethodName}(item, path + \".{property.PropertyName}[\" + index{index} + \"]\", visited);");
                            generator.AppendCode($"index{index}++;");
                        });
                    });
                }
            });
        }
    }
}
