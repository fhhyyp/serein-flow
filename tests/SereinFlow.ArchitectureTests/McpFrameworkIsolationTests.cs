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
        var methods = typeof(SereinFlowMcpBackend).Assembly.GetTypes().SelectMany(type =>
        {
            var value1 = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            return value1;
        }).Where(method =>
        {
            var value2 = method.GetCustomAttributes().OfType<ISereinFlowMcpToolMetadata>().Any();
            return value2;
        }).ToArray();
        Assert.Equal(46, methods.Length);
        Assert.All(methods, method =>
        {
            var argument4 = method.GetParameters();
            Assert.DoesNotContain(argument4, parameter => parameter.ParameterType == typeof(JsonElement));
        });
        Assert.All(methods, method =>
        {
            Assert.False(method.IsStatic);
            var parameterTypes = method.GetParameters().Select(static parameter => parameter.ParameterType);
            Assert.DoesNotContain(typeof(IServiceProvider), parameterTypes);
            Assert.DoesNotContain(typeof(Microsoft.Extensions.DependencyInjection.IServiceScope), parameterTypes);
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
        });
    }

    [Fact]
    public void BusinessHostUsesTheSharedToolModelAndExecutor()
    {
        var business = typeof(SereinFlowMcpBackend).Assembly;
        var removedContext = business.GetType("SereinFlow.Mcp.Integration.SereinFlowMcpToolContext");
        Assert.Null(removedContext);
        var framework = typeof(McpToolCatalog).Assembly;
        Assert.NotEqual(business, framework);
        var duplicatedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "McpToolDescriptor",
            "McpToolDefinition",
            "McpToolCatalog",
            "McpToolExecutor",
            "IMcpTool",
            "McpToolDiscovery",
            "McpJsonSchema",
            "SereinFlowMcpMutationGate",
            "McpToolMutationGate",
            "SereinFlowMcpAuditService"
        };
        {
            var actual1 = business.GetTypes();
            Assert.DoesNotContain(actual1, type =>
            {
                var duplicated = duplicatedNames.Contains(type.Name);
                return duplicated;
            });
        }

        Assert.Equal(typeof(McpToolPolicy), typeof(SereinFlowMcpPolicy).BaseType);
        {
            var actual2 = framework.GetTypes();
            Assert.DoesNotContain(actual2, type =>
            {
                var attribute = typeof(Attribute).IsAssignableFrom(type);
                return attribute;
            });
        }

        {
            var actual3 = business.GetTypes().Where(type =>
            {
                var value5 = type.Name.EndsWith("Attribute", StringComparison.Ordinal) && type.Namespace is "SereinFlow.Mcp" or "SereinFlow.Mcp.Framework";
                return value5;
            });
            Assert.All(actual3, type =>
            {
                Assert.False(type.IsPublic);
            });
        }

        {
            var actual4 = typeof(SereinFlowMcpBackend).GetConstructors().Single().GetParameters();
            Assert.Contains(actual4, parameter => parameter.ParameterType == typeof(McpToolCatalog));
        }

        {
            var actual5 = typeof(SereinFlowMcpBackend).GetConstructors().Single().GetParameters();
            Assert.Contains(actual5, parameter => parameter.ParameterType == typeof(McpToolExecutor));
        }
    }
}
