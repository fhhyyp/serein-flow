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
        Assert.Contains("http://127.0.0.1:8188/mcp", pluginConfig, StringComparison.Ordinal);
        Assert.Contains("\"bearer_token_env_var\": \"SEREINFLOW_MCP_API_KEY\"", pluginConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void McpBackendFacadeIsNotSplitAcrossPartialClasses()
    {
        var mcpRoot = Path.Combine(FindRepositoryRoot(), "src", "SereinFlow.Mcp");
        var sourceFiles = Directory
            .EnumerateFiles(mcpRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(sourceFiles, path =>
            Path.GetFileNameWithoutExtension(path).StartsWith("SereinFlowMcpBackend.", StringComparison.Ordinal));
        Assert.DoesNotContain(sourceFiles, path =>
            File.ReadAllText(path).Contains("partial class SereinFlowMcpBackend", StringComparison.Ordinal));
    }

    [Fact]
    public void McpToolImplementationsAreKeptInTheToolsFolder()
    {
        var mcpRoot = Path.Combine(FindRepositoryRoot(), "src", "SereinFlow.Mcp");
        var toolsRoot = Path.Combine(mcpRoot, "Tools");
        var toolFiles = new[]
        {
            "McpApiKeyToolHandlers.cs",
            "McpFlowToolHandlers.cs",
            "McpLibraryToolHandlers.cs",
            "McpProjectToolHandlers.cs",
            "McpReadModelToolHandlers.cs",
            "McpPreviewPayloadReaders.cs",
            "McpStoredPreviewPayloads.cs",
            "McpDebugToolHandlers.cs",
            "McpToolAuthorization.cs",
            "McpToolSchemas.cs",
            "McpToolSupport.cs",
            "SereinFlowMcpToolCatalogFactory.cs"
        };

        Assert.All(toolFiles, fileName =>
        {
            Assert.True(File.Exists(Path.Combine(toolsRoot, fileName)),
                $"MCP tool implementation must be located in Tools: {fileName}");
            Assert.False(File.Exists(Path.Combine(mcpRoot, fileName)),
                $"MCP tool implementation must not be located in the MCP root: {fileName}");
        });
    }

    [Fact]
    public void RestSurfaceUsesControllersWithoutMinimalApiMappingFiles()
    {
        var root = FindRepositoryRoot();
        var apiRoot = Path.Combine(root, "src", "SereinFlow.Api");
        var mappingFiles = Directory.EnumerateFiles(apiRoot, "*EndpointMapping.cs", SearchOption.AllDirectories);
        Assert.Empty(mappingFiles);

        var controllerFiles = Directory
            .EnumerateFiles(Path.Combine(apiRoot, "Controllers"), "*Controller.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith("ApiControllerBase.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(controllerFiles);
        foreach (var controllerFile in controllerFiles)
        {
            var source = File.ReadAllText(controllerFile);
            Assert.Contains("ControllerBase", source, StringComparison.Ordinal);
            Assert.Contains("[Route(", source, StringComparison.Ordinal);
            Assert.Contains("[Http", source, StringComparison.Ordinal);
        }

        var apiSources = Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories);
        foreach (var sourceFile in apiSources)
        {
            if (sourceFile.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                || sourceFile.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
                continue;

            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain(".MapGet(\"/api/", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".MapPost(\"/api/", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".MapPut(\"/api/", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".MapPatch(\"/api/", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".MapDelete(\"/api/", source, StringComparison.Ordinal);
        }
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
