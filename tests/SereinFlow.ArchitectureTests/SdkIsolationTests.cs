using System.Xml.Linq;

namespace SereinFlow.ArchitectureTests;

public sealed class SdkIsolationTests
{
    [Fact]
    public void CoreSdkReferencesOnlyContracts()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "SereinFlow.SDK", "SereinFlow.SDK.csproj");
        var project = XDocument.Load(projectPath);
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(static element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include")))
            .Where(static value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.Equal(["SereinFlow.Contracts"], projectReferences);
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Theory]
    [InlineData("src/SereinFlow.Mcp/SereinFlow.Mcp.csproj")]
    [InlineData("src/SereinFlow.Library/SereinFlow.Library.csproj")]
    public void McpAndLibraryDoNotReferenceSdk(string relativeProjectPath)
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, relativeProjectPath));
        var references = project
            .Descendants()
            .SelectMany(static element => element.Attributes("Include").Concat(element.Attributes("Project")))
            .Select(static attribute => Path.GetFileNameWithoutExtension(attribute.Value));

        Assert.DoesNotContain("SereinFlow.SDK", references, StringComparer.Ordinal);
        Assert.DoesNotContain("SereinFlow.SDK.DependencyInjection", references, StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "SereinFlow.sln")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the SereinFlow repository root.");
    }
}
