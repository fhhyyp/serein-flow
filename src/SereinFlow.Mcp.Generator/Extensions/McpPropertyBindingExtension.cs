using SereinFlow.Mcp.Generator.Models;

namespace SereinFlow.Mcp.Generator.Extensions;

internal static class McpPropertyBindingExtension
{
    internal static void GeneratorPropertyBindings(this GeneratorCache<McpToolRegistryCache> generator)
    {
        foreach (var binding in generator.Cache.Validations)
        {
            if (binding.BindingMethodName is null) continue;
            for (var index = 0; index < binding.Bindings.Count; index++)
                generator.AppendCode($"private static readonly global::SereinFlow.Mcp.Framework.McpParameterOptions {binding.BindingMethodName}Parameter{index} = {binding.Bindings[index].ParameterExpression};");
            generator.AppendCode($"private static {binding.TypeName} {binding.BindingMethodName}(global::System.Text.Json.JsonElement arguments, global::System.Text.Json.JsonSerializerOptions options)");
            generator.AppendCodeBlok(() =>
            {
                generator.AppendCode($"var request = new {binding.TypeName}();");
                for (var index = 0; index < binding.Bindings.Count; index++)
                {
                    var property = binding.Bindings[index];
                    generator.AppendCodeBlok(() =>
                    {
                        generator.AppendCode($"var name = {property.WireNameExpression};");
                        generator.AppendCode($"var parameter = {binding.BindingMethodName}Parameter{index};");
                        if (property.Required) generator.AppendCode("global::SereinFlow.Mcp.Framework.McpArgumentReader.Require(arguments, name, parameter);");
                        generator.AppendCode("if (arguments.TryGetProperty(name, out var value))");
                        generator.AppendCodeBlok(() =>
                        {
                            if (property.IgnoreWhitespace)
                                generator.AppendCode("if (value.ValueKind != global::System.Text.Json.JsonValueKind.String || !global::System.String.IsNullOrWhiteSpace(value.GetString()))");
                            generator.AppendCode($"request.@{property.PropertyName} = {property.ReadExpression};");
                        });
                    });
                }
                generator.AppendCode($"{binding.MethodName}(request, \"$\", new global::System.Collections.Generic.HashSet<object>(global::System.Collections.Generic.ReferenceEqualityComparer.Instance));");
                generator.AppendCode("return request;");
            });
        }
    }
}
