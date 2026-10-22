using SereinFlow.Mcp;
using SereinFlow.Mcp.Framework;
using SereinFlow.Mcp.Integration;
using System.Reflection;
using System.Text.Json;

namespace SereinFlow.ArchitectureTests;

public sealed class McpFrameworkIsolationTests
{
    [Fact]
    public void ToolBusinessMethodsReceiveTypedRequestsInsteadOfRawJson()
    {
        var methods = typeof(SereinFlowMcpBackend).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes().OfType<ISereinFlowMcpToolMetadata>().Any()).ToArray();
        Assert.Equal(50, methods.Length);
        Assert.All(methods, method => Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(JsonElement)));
        Assert.All(methods, method =>
        {
            Assert.False(method.IsStatic);
            var parameterTypes = method.GetParameters().Select(static parameter => parameter.ParameterType);
            Assert.DoesNotContain(typeof(IServiceProvider), parameterTypes);
            Assert.DoesNotContain(typeof(Microsoft.Extensions.DependencyInjection.IServiceScope), parameterTypes);
            Assert.DoesNotContain(typeof(SereinFlowMcpToolContext), parameterTypes);
        });
        var handlers = methods.Select(static method => method.DeclaringType!).Distinct();
        Assert.All(handlers, handler =>
        {
            var constructors = handler.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var constructor = Assert.Single(constructors);
            var parameterTypes = constructor.GetParameters().Select(static parameter => parameter.ParameterType).ToArray();
            Assert.NotEmpty(parameterTypes);
            Assert.DoesNotContain(typeof(IServiceProvider), parameterTypes);
            Assert.DoesNotContain(typeof(Microsoft.Extensions.DependencyInjection.IServiceScope), parameterTypes);
            Assert.DoesNotContain(typeof(SereinFlowMcpToolContext), parameterTypes);
        });
    }

    [Fact]
    public void BusinessHostUsesTheSharedToolModelAndExecutor()
    {
        var business = typeof(SereinFlowMcpBackend).Assembly;
        var framework = typeof(McpToolCatalog).Assembly;
        Assert.NotEqual(business, framework);
        var duplicatedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "McpToolDescriptor", "McpToolDefinition", "McpToolCatalog", "McpToolExecutor", "IMcpTool", "McpToolDiscovery", "McpJsonSchema",
            "SereinFlowMcpMutationGate", "McpToolMutationGate", "SereinFlowMcpAuditService"
        };
        Assert.DoesNotContain(business.GetTypes(), type => duplicatedNames.Contains(type.Name));
        Assert.Equal(typeof(McpToolPolicy), typeof(SereinFlowMcpPolicy).BaseType);
        Assert.DoesNotContain(framework.GetTypes(), type => typeof(Attribute).IsAssignableFrom(type));
        Assert.All(business.GetTypes().Where(type => type.Name.EndsWith("Attribute", StringComparison.Ordinal)
                && type.Namespace is "SereinFlow.Mcp" or "SereinFlow.Mcp.Framework"),
            type => Assert.False(type.IsPublic));
        Assert.Contains(typeof(SereinFlowMcpBackend).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(McpToolCatalog));
        Assert.Contains(typeof(SereinFlowMcpBackend).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(McpToolExecutor));
    }
}
