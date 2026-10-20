using System;
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
        var referencedAssemblies = typeof(AssemblyMarker).Assembly.GetReferencedAssemblies()
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
        var findRepositoryRootResult = FindRepositoryRoot();
        var enumerateFilesResult = Directory.EnumerateFiles(findRepositoryRootResult, "*.csproj", SearchOption.AllDirectories);
        var whereResult = enumerateFilesResult.Where(static path =>
        {
            var evaluatedValue = !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue;
        });
        var sourceProjectFiles = whereResult.Where(static path =>
        {
            var evaluatedValue1 = !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue1;
        });
        foreach (var sourceProjectFile in sourceProjectFiles)
        {
            var project = XDocument.Load(sourceProjectFile);
            var selectResult3 = project.Descendants().SelectMany(static element =>
            {
                var attributesResult = element.Attributes("Include");
                var attributesResult2 = element.Attributes("Project");
                var concatResult = attributesResult.Concat(attributesResult2);
                return concatResult;
            }).Select(static attribute => attribute.Value).Select(static value =>
            {
                var getFileNameWithoutExtensionResult = Path.GetFileNameWithoutExtension(value);
                return getFileNameWithoutExtensionResult;
            });
            var references = selectResult3.ToArray();
            Assert.DoesNotContain(retiredComponent, references, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void McpHasOneLibraryAndNoRetiredExecutableHost()
    {
        var root = FindRepositoryRoot();
        var enumerateFilesResult8 = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories);
        var whereResult9 = enumerateFilesResult8.Where(static path =>
        {
            var evaluatedValue4 = !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue4;
        });
        var whereResult10 = whereResult9.Where(static path =>
        {
            var evaluatedValue5 = !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue5;
        });
        var sourceProjectFiles = whereResult10.ToArray();
        Assert.Single(sourceProjectFiles, path =>
        {
            var getFileNameResult = Path.GetFileName(path);
            var equalsResult = string.Equals(getFileNameResult, "SereinFlow.Mcp.csproj", StringComparison.OrdinalIgnoreCase);
            return equalsResult;
        });
        Assert.DoesNotContain(sourceProjectFiles, path =>
        {
            var containsResult = path.Contains("SereinFlow.McpServer", StringComparison.OrdinalIgnoreCase);
            return containsResult;
        });
        var combineResult = Path.Combine(root, "src");
        var enumerateFilesResult11 = Directory.EnumerateFiles(combineResult, "*SereinFlowMcpBackend.cs", SearchOption.AllDirectories);
        var whereResult12 = enumerateFilesResult11.Where(static path =>
        {
            var evaluatedValue6 = !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue6;
        });
        var whereResult13 = whereResult12.Where(static path =>
        {
            var evaluatedValue7 = !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue7;
        });
        var backendFiles = whereResult13.ToArray();
        Assert.Single(backendFiles);
        var combineResult14 = Path.Combine(root, "plugins", "sereinflow-ai-toolkit", ".mcp.json");
        var pluginConfig = File.ReadAllText(combineResult14);
        Assert.DoesNotContain("DatabasePath", pluginConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("LibraryDirectory", pluginConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("McpServer", pluginConfig, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8188/mcp", pluginConfig, StringComparison.Ordinal);
        Assert.Contains("\"bearer_token_env_var\": \"SEREINFLOW_MCP_API_KEY\"", pluginConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void McpBackendFacadeIsNotSplitAcrossPartialClasses()
    {
        var findRepositoryRootResult19 = FindRepositoryRoot();
        var mcpRoot = Path.Combine(findRepositoryRootResult19, "src", "SereinFlow.Mcp");
        var enumerateFilesResult20 = Directory.EnumerateFiles(mcpRoot, "*.cs", SearchOption.AllDirectories);
        var whereResult21 = enumerateFilesResult20.Where(static path =>
        {
            var evaluatedValue15 = !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue15;
        });
        var whereResult22 = whereResult21.Where(static path =>
        {
            var evaluatedValue16 = !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue16;
        });
        var sourceFiles = whereResult22.ToArray();
        Assert.DoesNotContain(sourceFiles, path =>
        {
            var getFileNameWithoutExtensionResult17 = Path.GetFileNameWithoutExtension(path);
            var startsWithResult = getFileNameWithoutExtensionResult17.StartsWith("SereinFlowMcpBackend.", StringComparison.Ordinal);
            return startsWithResult;
        });
        Assert.DoesNotContain(sourceFiles, path =>
        {
            var readAllTextResult = File.ReadAllText(path);
            var containsResult18 = readAllTextResult.Contains("partial class SereinFlowMcpBackend", StringComparison.Ordinal);
            return containsResult18;
        });
    }

    [Fact]
    public void McpResourceUriLiteralsAreKeptInTheirProtocolCatalogs()
    {
        var findRepositoryRootResult30 = FindRepositoryRoot();
        var mcpRoot = Path.Combine(findRepositoryRootResult30, "src", "SereinFlow.Mcp");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "McpResourceUris.cs",
            "McpAiGuidance.cs"
        };
        var enumerateFilesResult31 = Directory.EnumerateFiles(mcpRoot, "*.cs", SearchOption.AllDirectories);
        var whereResult32 = enumerateFilesResult31.Where(static path =>
        {
            var evaluatedValue23 = !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue23;
        });
        var whereResult33 = whereResult32.Where(static path =>
        {
            var evaluatedValue24 = !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue24;
        });
        var whereResult34 = whereResult33.Where(path =>
        {
            var evaluatedValue27 = !resolveContainsResult();
            return evaluatedValue27;
            bool resolveContainsResult()
            {
                var getFileNameResult25 = Path.GetFileName(path);
                var containsResult26 = allowedFiles.Contains(getFileNameResult25);
                return containsResult26;
            }
        });
        var whereResult35 = whereResult34.Where(path =>
        {
            var readAllTextResult28 = File.ReadAllText(path);
            var containsResult29 = readAllTextResult28.Contains("sereinflow://", StringComparison.Ordinal);
            return containsResult29;
        });
        var selectResult36 = whereResult35.Select(path =>
        {
            var getRelativePathResult = Path.GetRelativePath(mcpRoot, path);
            return getRelativePathResult;
        });
        var offenders = selectResult36.ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void WorkpieceHttpLinkLiteralHasOneOwner()
    {
        var findRepositoryRootResult42 = FindRepositoryRoot();
        var sourceRoot = Path.Combine(findRepositoryRootResult42, "src");
        var enumerateFilesResult43 = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        var whereResult44 = enumerateFilesResult43.Where(static path =>
        {
            var evaluatedValue37 = !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue37;
        });
        var whereResult45 = whereResult44.Where(static path =>
        {
            var evaluatedValue38 = !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue38;
        });
        var whereResult46 = whereResult45.Where(path =>
        {
            var readAllTextResult39 = File.ReadAllText(path);
            var containsResult40 = readAllTextResult39.Contains("/api/runs/{runId:D}/workpieces/", StringComparison.Ordinal);
            return containsResult40;
        });
        var selectResult47 = whereResult46.Select(path =>
        {
            var getRelativePathResult41 = Path.GetRelativePath(sourceRoot, path);
            return getRelativePathResult41;
        });
        var owners = selectResult47.ToArray();
        ReadOnlySpan<string> evaluatedValue48 = [Path.Combine("SereinFlow.Contracts", "ApiUris.cs")];
        Assert.Equal(evaluatedValue48, owners);
    }

    [Fact]
    public void McpToolImplementationsAreKeptInTheToolsFolder()
    {
        var findRepositoryRootResult52 = FindRepositoryRoot();
        var mcpRoot = Path.Combine(findRepositoryRootResult52, "src", "SereinFlow.Mcp");
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
            var combineResult49 = Path.Combine(toolsRoot, fileName);
            var existsResult = File.Exists(combineResult49);
            Assert.True(existsResult, $"MCP tool implementation must be located in Tools: {fileName}");
            var combineResult50 = Path.Combine(mcpRoot, fileName);
            var existsResult51 = File.Exists(combineResult50);
            Assert.False(existsResult51, $"MCP tool implementation must not be located in the MCP root: {fileName}");
        });
    }

    [Fact]
    public void RestSurfaceUsesControllersWithoutMinimalApiMappingFiles()
    {
        var root = FindRepositoryRoot();
        var apiRoot = Path.Combine(root, "src", "SereinFlow.AspNetCore");
        var mappingFiles = Directory.EnumerateFiles(apiRoot, "*EndpointMapping.cs", SearchOption.AllDirectories);
        Assert.Empty(mappingFiles);
        var combineResult54 = Path.Combine(apiRoot, "Controllers");
        var enumerateFilesResult55 = Directory.EnumerateFiles(combineResult54, "*Controller.cs", SearchOption.TopDirectoryOnly);
        var whereResult56 = enumerateFilesResult55.Where(path =>
        {
            var evaluatedValue53 = !path.EndsWith("ApiControllerBase.cs", StringComparison.OrdinalIgnoreCase);
            return evaluatedValue53;
        });
        var controllerFiles = whereResult56.ToArray();
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
            if (sourceFile.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) || sourceFile.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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
            var combineResult57 = Path.Combine(current.FullName, "SereinFlow.sln");
            if (File.Exists(combineResult57))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the SereinFlow repository root.");
    }
}
