using SereinFlow.Application;

namespace SereinFlow.Application.Tests;
public sealed class McpPackageStagingServiceTests
{
    [Fact]
    public async Task DeletedStagedPackageCannotBeReadAgain()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue = $"sereinflow-mcp-staging-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult, evaluatedValue);
        try
        {
            using var staging = new McpPackageStagingService(root, maxBytes: 1024);
            await using var source = new MemoryStream([0x50, 0x4b, 0x03, 0x04]);
            var staged = await staging.StageAsync(source);
            staging.Delete(staged.Path);
            var exception = Assert.Throws<FileNotFoundException>(() =>
            {
                var openReadResult = staging.OpenRead(staged.Path);
                return openReadResult;
            });
            Assert.Contains("no longer available", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StagingUsesTheSharedRuntimeUploadLimit()
    {
        var getTempPathResult1 = Path.GetTempPath();
        var evaluatedValue2 = $"sereinflow-mcp-staging-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult1, evaluatedValue2);
        try
        {
            var settings = new FileUploadSettings();
            settings.Configure(FileUploadLimits.MinimumMaxFileSizeBytes);
            using var staging = new McpPackageStagingService(root, maxBytes: 8 * 1024 * 1024, fileUploadSettings: settings);
            await using var source = new MemoryStream(new byte[FileUploadLimits.MinimumMaxFileSizeBytes + 1]);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                var stageAsyncResult = staging.StageAsync(source);
                return stageAsyncResult;
            });
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
