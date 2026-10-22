using SereinFlow.Application;
using SereinFlow.AspNetCore;
using SereinFlow.Domain;
using SereinFlow.Mcp;
using SereinFlow.Mcp.Framework;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.ArchitectureTests;

public sealed class SecurityIsolationTests
{
    [Fact]
    public void CoreAssembliesDoNotReferenceDefaultApiKeyImplementation()
    {
        Type[] markers = [typeof(SereinFlowActor), typeof(SereinFlowResource), typeof(Project), typeof(SereinFlowMcpBackend), typeof(McpToolCatalog), typeof(SereinFlowHostBuilder), typeof(SereinFlow.Infrastructure.Configuration.SereinFlowStorageOptions)];
        foreach (var marker in markers)
        {
            var references = marker.Assembly.GetReferencedAssemblies();
            Assert.DoesNotContain(references, reference => reference.Name == "SereinFlow.Security.ApiKeys");
        }
    }
}
