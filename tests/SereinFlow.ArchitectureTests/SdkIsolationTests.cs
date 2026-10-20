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
        var selectResult = project.Descendants("ProjectReference").Select(static element =>
        {
            var evaluatedValue = (string?)element.Attribute("Include");
            var getFileNameWithoutExtensionResult = Path.GetFileNameWithoutExtension(evaluatedValue);
            return getFileNameWithoutExtensionResult;
        });
        var whereResult = selectResult.Where(static value => value is not null);
        var castResult = whereResult.Cast<string>();
        var projectReferences = castResult.ToArray();
        Assert.Equal(["SereinFlow.Contracts"], projectReferences);
        var descendantsResult1 = project.Descendants("PackageReference");
        Assert.Empty(descendantsResult1);
    }

    [Theory]
    [InlineData("src/SereinFlow.Mcp/SereinFlow.Mcp.csproj")]
    [InlineData("src/SereinFlow.Library/SereinFlow.Library.csproj")]
    public void McpAndLibraryDoNotReferenceSdk(string relativeProjectPath)
    {
        var root = FindRepositoryRoot();
        var combineResult = Path.Combine(root, relativeProjectPath);
        var project = XDocument.Load(combineResult);
        var references = project.Descendants().SelectMany(static element =>
        {
            var attributesResult = element.Attributes("Include");
            var attributesResult2 = element.Attributes("Project");
            var concatResult = attributesResult.Concat(attributesResult2);
            return concatResult;
        }).Select(static attribute =>
        {
            var getFileNameWithoutExtensionResult3 = Path.GetFileNameWithoutExtension(attribute.Value);
            return getFileNameWithoutExtensionResult3;
        });
        Assert.DoesNotContain("SereinFlow.SDK", references, StringComparer.Ordinal);
        Assert.DoesNotContain("SereinFlow.SDK.DependencyInjection", references, StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            var combineResult5 = Path.Combine(current.FullName, "SereinFlow.sln");
            if (File.Exists(combineResult5))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the SereinFlow repository root.");
    }
}
