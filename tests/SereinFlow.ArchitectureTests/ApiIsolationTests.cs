using System.Reflection;
using System.Xml.Linq;
using SereinFlow.Api;

namespace SereinFlow.ArchitectureTests;

public sealed class ApiIsolationTests
{
    [Theory]
    [InlineData("SereinFlow.Runtime")]
    [InlineData("SereinFlow.ScriptAdapter")]
    [InlineData("ScriptLang")]
    [InlineData("Serein.Script")]
    public void ApiDoesNotStaticallyReferenceUntrustedExecutionComponents(string forbiddenAssemblyName)
    {
        var referencedAssemblies = typeof(AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name);

        Assert.DoesNotContain(forbiddenAssemblyName, referencedAssemblies);
    }

    [Theory]
    [InlineData("Serein.Library.NodeGenerator")]
    [InlineData("Serein.Proto.HttpApi")]
    [InlineData("Serein.Proto.Modbus")]
    [InlineData("Serein.Proto.WebSocket")]
    [InlineData("Serein.Workbench.Avalonia")]
    [InlineData("Serein.CollaborationSync")]
    [InlineData("Serein.Script")]
    [InlineData("LegacyAdapter")]
    public void SolutionSourceProjectsDoNotReferenceRetiredComponents(string retiredComponent)
    {
        var sourceProjectFiles = Directory
            .EnumerateFiles(FindRepositoryRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));

        foreach (var sourceProjectFile in sourceProjectFiles)
        {
            var project = XDocument.Load(sourceProjectFile);
            var references = project
                .Descendants()
                .SelectMany(static element => element.Attributes("Include").Concat(element.Attributes("Project")))
                .Select(static attribute => attribute.Value)
                .Select(static value => Path.GetFileNameWithoutExtension(value))
                .ToArray();

            Assert.DoesNotContain(retiredComponent, references, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void McpHasOneLibraryAndNoRetiredExecutableHost()
    {
        var root = FindRepositoryRoot();
        var sourceProjectFiles = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Single(sourceProjectFiles, path =>
            string.Equals(Path.GetFileName(path), "SereinFlow.Mcp.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sourceProjectFiles, path =>
            path.Contains("SereinFlow.McpServer", StringComparison.OrdinalIgnoreCase));

        var backendFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*SereinFlowMcpBackend.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Single(backendFiles);

        var pluginConfig = File.ReadAllText(Path.Combine(root, "plugins", "sereinflow-ai-toolkit", ".mcp.json"));
        Assert.DoesNotContain("DatabasePath", pluginConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("LibraryDirectory", pluginConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("McpServer", pluginConfig, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:5178/mcp", pluginConfig, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "SereinFlow.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the SereinFlow repository root.");
    }
}
