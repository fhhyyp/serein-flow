using Microsoft.Extensions.Configuration;
using SereinFlow.Infrastructure.Configuration;

namespace SereinFlow.Infrastructure.Tests;

public sealed class SereinFlowStorageOptionsTests
{
    [Fact]
    public void ResolvesDefaultStorageLayoutUnderTheConfiguredDataRoot()
    {
        using var directory = new TemporaryDirectory();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = "server-data",
        });

        var options = SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path);

        var expectedRoot = Path.GetFullPath(Path.Combine(directory.Path, "server-data"));
        Assert.Equal(expectedRoot, options.DataRoot);
        Assert.Equal(Path.Combine(expectedRoot, "sereinflow.db"), options.DatabasePath);
        Assert.Equal(Path.Combine(expectedRoot, "libraries"), options.LibraryDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "script-artifacts"), options.ScriptArtifactRoot);
        Assert.Equal(Path.Combine(expectedRoot, "mcp-staging"), options.McpPackageStagingDirectory);
        Assert.True(Directory.Exists(options.DataRoot));
        Assert.True(Directory.Exists(options.LibraryDirectory));
        Assert.True(Directory.Exists(options.ScriptArtifactRoot));
        Assert.True(Directory.Exists(options.McpPackageStagingDirectory));
    }

    [Theory]
    [InlineData("SereinFlow:DatabasePath")]
    [InlineData("SereinFlow:LibraryDirectory")]
    [InlineData("SereinFlow:ScriptArtifactRoot")]
    [InlineData("SereinFlow:Mcp:PackageStagingDirectory")]
    public void RejectsLegacyPathSettingsUntilDataRootIsExplicitlyConfigured(string legacyKey)
    {
        using var directory = new TemporaryDirectory();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [legacyKey] = Path.Combine(directory.Path, "legacy-location"),
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path));

        Assert.Equal(
            "Legacy SereinFlow path settings require an explicit SereinFlow:DataRoot migration setting.",
            exception.Message);
        Assert.DoesNotContain(directory.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IgnoresLegacyPathSettingsAfterTheServerConfiguresDataRoot()
    {
        using var directory = new TemporaryDirectory();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = "authoritative-data",
            ["SereinFlow:DatabasePath"] = Path.Combine(directory.Path, "legacy.db"),
            ["SereinFlow:LibraryDirectory"] = Path.Combine(directory.Path, "legacy-libraries"),
        });

        var options = SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path);

        Assert.Equal(Path.Combine(options.DataRoot, "sereinflow.db"), options.DatabasePath);
        Assert.Equal(Path.Combine(options.DataRoot, "libraries"), options.LibraryDirectory);
    }

    [Theory]
    [InlineData("SereinFlow:DatabaseFileName", "../outside.db")]
    [InlineData("SereinFlow:LibraryDirectoryName", "nested/libraries")]
    [InlineData("SereinFlow:ScriptArtifactDirectoryName", ".")]
    [InlineData("SereinFlow:McpStagingDirectoryName", "..")]
    public void RejectsChildStorageNamesThatCanEscapeDataRoot(string key, string value)
    {
        using var directory = new TemporaryDirectory();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = "data",
            [key] = value,
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SereinFlowStorageOptions.FromConfiguration(configuration, directory.Path));

        Assert.Contains("must be a single relative name", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sereinflow-storage-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
                return;

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
