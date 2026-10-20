using Microsoft.Extensions.Configuration;
using SereinFlow.Infrastructure.Configuration;

namespace SereinFlow.Infrastructure.Tests;
public sealed class SereinFlowStorageOptionsTests
{
    [Fact]
    public void ResolvesDefaultStorageLayoutUnderTheConfiguredDataRoot()
    {
        using var directory = new TemporaryDirectory();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = "server-data",
        };
        var configuration = CreateConfiguration(dictionary);
        var options = SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path);
        var combineResult = Path.Combine(directory.Path, "server-data");
        var expectedRoot = Path.GetFullPath(combineResult);
        Assert.Equal(expectedRoot, options.DataRoot);
        var combineResult1 = Path.Combine(expectedRoot, "sereinflow.db");
        Assert.Equal(combineResult1, options.DatabasePath);
        var combineResult2 = Path.Combine(expectedRoot, "libraries");
        Assert.Equal(combineResult2, options.LibraryDirectory);
        var combineResult3 = Path.Combine(expectedRoot, "script-artifacts");
        Assert.Equal(combineResult3, options.ScriptArtifactRoot);
        var combineResult4 = Path.Combine(expectedRoot, "mcp-staging");
        Assert.Equal(combineResult4, options.McpPackageStagingDirectory);
        var combineResult5 = Path.Combine(expectedRoot, "workpieces");
        Assert.Equal(combineResult5, options.WorkpieceDirectory);
        var existsResult = Directory.Exists(options.DataRoot);
        Assert.True(existsResult);
        var existsResult6 = Directory.Exists(options.LibraryDirectory);
        Assert.True(existsResult6);
        var existsResult7 = Directory.Exists(options.ScriptArtifactRoot);
        Assert.True(existsResult7);
        var existsResult8 = Directory.Exists(options.McpPackageStagingDirectory);
        Assert.True(existsResult8);
        var existsResult9 = Directory.Exists(options.WorkpieceDirectory);
        Assert.True(existsResult9);
    }

    [Theory]
    [InlineData("SereinFlow:DatabasePath")]
    [InlineData("SereinFlow:LibraryDirectory")]
    [InlineData("SereinFlow:ScriptArtifactRoot")]
    [InlineData("SereinFlow:Mcp:PackageStagingDirectory")]
    public void RejectsLegacyPathSettingsUntilDataRootIsExplicitlyConfigured(string legacyKey)
    {
        using var directory = new TemporaryDirectory();
        var dictionary10 = new Dictionary<string, string?>
        {
            [legacyKey] = Path.Combine(directory.Path, "legacy-location"),
        };
        var configuration = CreateConfiguration(dictionary10);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var fromConfigurationResult = SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path);
            return fromConfigurationResult;
        });
        Assert.Equal("Legacy SereinFlow path settings require an explicit SereinFlow:DataRoot migration setting.", exception.Message);
        Assert.DoesNotContain(directory.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IgnoresLegacyPathSettingsAfterTheServerConfiguresDataRoot()
    {
        using var directory = new TemporaryDirectory();
        var dictionary11 = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = "authoritative-data",
            ["SereinFlow:DatabasePath"] = Path.Combine(directory.Path, "legacy.db"),
            ["SereinFlow:LibraryDirectory"] = Path.Combine(directory.Path, "legacy-libraries"),
        };
        var configuration = CreateConfiguration(dictionary11);
        var options = SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path);
        var combineResult12 = Path.Combine(options.DataRoot, "sereinflow.db");
        Assert.Equal(combineResult12, options.DatabasePath);
        var combineResult13 = Path.Combine(options.DataRoot, "libraries");
        Assert.Equal(combineResult13, options.LibraryDirectory);
    }

    [Theory]
    [InlineData("SereinFlow:DatabaseFileName", "../outside.db")]
    [InlineData("SereinFlow:LibraryDirectoryName", "nested/libraries")]
    [InlineData("SereinFlow:ScriptArtifactDirectoryName", ".")]
    [InlineData("SereinFlow:McpStagingDirectoryName", "..")]
    [InlineData("SereinFlow:WorkpieceDirectoryName", "nested/workpieces")]
    public void RejectsChildStorageNamesThatCanEscapeDataRoot(string key, string value)
    {
        using var directory = new TemporaryDirectory();
        var dictionary15 = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = "data",
            [key] = value,
        };
        var configuration = CreateConfiguration(dictionary15);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var fromConfigurationResult14 = SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path);
            return fromConfigurationResult14;
        });
        Assert.Contains("must be a single relative name", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        var configurationBuilder = new ConfigurationBuilder();
        var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection(values);
        var buildResult = addInMemoryCollectionResult.Build();
        return buildResult;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            var getTempPathResult = System.IO.Path.GetTempPath();
            var evaluatedValue = $"sereinflow-storage-tests-{Guid.NewGuid():N}";
            Path = System.IO.Path.Combine(getTempPathResult, evaluatedValue);
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
