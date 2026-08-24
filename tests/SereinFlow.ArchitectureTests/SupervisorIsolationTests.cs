using System.Reflection;
using SereinFlow.Worker.Supervisor;

namespace SereinFlow.ArchitectureTests;

public sealed class SupervisorIsolationTests
{
    [Theory]
    [InlineData("SereinFlow.Runtime")]
    [InlineData("SereinFlow.ScriptAdapter")]
    [InlineData("ScriptLang")]
    [InlineData("Serein.Script")]
    public void SupervisorDoesNotStaticallyReferenceUntrustedExecutionComponents(string forbiddenAssemblyName)
    {
        var referencedAssemblies = typeof(WorkerSupervisor)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name);

        Assert.DoesNotContain(forbiddenAssemblyName, referencedAssemblies);
    }
}
