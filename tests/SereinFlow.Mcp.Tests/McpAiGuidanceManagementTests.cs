using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpAiGuidanceManagementTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("sereinflow-skill-management-");

    [Fact]
    public async Task ListsAndReadsEveryEmbeddedSkillWithoutCreatingFiles()
    {
        var provider = new McpAiGuidanceProvider(new McpAiGuidanceOptions(), _root.FullName);
        using var management = new McpAiGuidanceManagementService(provider);
        var resources = management.List();
        Assert.Equal(McpAiGuidance.AllResources.Count, resources.Count);
        foreach (var resource in resources)
        {
            var content = await management.ReadAsync(resource.Key);
            Assert.Equal(McpAiGuidanceSourceDto.Embedded, content.Resource.Source);
            Assert.False(string.IsNullOrWhiteSpace(content.Content));
            Assert.Equal(content.Content, (await provider.ReadAsync(resource.Uri, CancellationToken.None)).Value);
        }
        Assert.Empty(_root.EnumerateFileSystemInfos());
    }

    [Fact]
    public async Task CustomPathCanSavePersistAndRestoreTheEmbeddedDefault()
    {
        var options = new McpAiGuidanceOptions { SereinLangFilePath = "custom/language.md" };
        var provider = new McpAiGuidanceProvider(options, _root.FullName);
        using var management = new McpAiGuidanceManagementService(provider);
        var original = await management.ReadAsync("sereinlang");
        var unchanged = await management.ReadAsync("sereinflow");
        var saved = await management.SaveAsync("sereinlang", new("# 自定义技能\n保持工具契约。", original.Revision));

        Assert.Equal(McpAiGuidanceSourceDto.Disk, saved.Resource.Source);
        Assert.NotEqual(original.Revision, saved.Revision);
        Assert.Equal(saved.Content, await File.ReadAllTextAsync(Path.Combine(_root.FullName, "custom/language.md")));
        var restartedProvider = new McpAiGuidanceProvider(options, _root.FullName);
        Assert.Equal(saved.Content, (await restartedProvider.ReadAsync(McpAiGuidanceId.SereinLang, CancellationToken.None)).Value);
        Assert.Equal(unchanged, await management.ReadAsync("sereinflow"));
        Assert.Empty(_root.EnumerateFiles("*.tmp", SearchOption.AllDirectories));

        var reset = await management.ResetAsync("sereinlang", new(saved.Revision));
        Assert.Equal(original, reset);
        Assert.False(File.Exists(Path.Combine(_root.FullName, "custom/language.md")));
        Assert.Equal(original.Content, (await provider.ReadAsync(McpAiGuidanceId.SereinLang, CancellationToken.None)).Value);
    }

    [Fact]
    public async Task RejectsStaleSaveAndResetWithoutLosingSavedContent()
    {
        using var management = CreateManagement();
        var original = await management.ReadAsync("sereinlang");
        var saved = await management.SaveAsync("sereinlang", new("# first edit", original.Revision));
        var saveError = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.SaveAsync("sereinlang", new("# stale edit", original.Revision)));
        var resetError = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.ResetAsync("sereinlang", new(original.Revision)));
        Assert.Equal(McpProtocolErrorCodes.Conflict, saveError.Code);
        Assert.Equal(McpProtocolErrorCodes.Conflict, resetError.Code);
        Assert.Equal(saved, await management.ReadAsync("sereinlang"));
    }

    [Fact]
    public async Task ConcurrentEditsUsingTheSameRevisionHaveOneWinner()
    {
        using var management = CreateManagement();
        var original = await management.ReadAsync("sereinlang");
        var results = await Task.WhenAll(AttemptSave("# edit one"), AttemptSave("# edit two"));
        Assert.Single(results, result => result == 0);
        Assert.Single(results, result => result == McpProtocolErrorCodes.Conflict);
        Assert.Empty(_root.EnumerateFiles("*.tmp", SearchOption.AllDirectories));

        async Task<int> AttemptSave(string content)
        {
            try
            {
                await management.SaveAsync("sereinlang", new(content, original.Revision));
                return 0;
            }
            catch (McpProtocolException exception)
            {
                return exception.Code;
            }
        }
    }

    [Fact]
    public async Task ExternalChangesAndConfigurationChangesInvalidateTheRevision()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var provider = new McpAiGuidanceProvider(configuration, _root.FullName);
        using var management = new McpAiGuidanceManagementService(provider);
        var original = await management.ReadAsync("sereinlang");
        Directory.CreateDirectory(Path.Combine(_root.FullName, "mcp"));
        await File.WriteAllTextAsync(Path.Combine(_root.FullName, "mcp/sereinlang-skill.md"), "# external change");
        var externalError = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.SaveAsync("sereinlang", new("# stale", original.Revision)));
        Assert.Equal(McpProtocolErrorCodes.Conflict, externalError.Code);

        var current = await management.ReadAsync("sereinlang");
        configuration["SereinFlow:Mcp:AiGuidance:Resources:sereinlang:FilePath"] = "custom/changed.md";
        var configError = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.ResetAsync("sereinlang", new(current.Revision)));
        Assert.Equal(McpProtocolErrorCodes.Conflict, configError.Code);
        Assert.Equal("# external change", await File.ReadAllTextAsync(Path.Combine(_root.FullName, "mcp/sereinlang-skill.md")));
        Assert.False(Directory.Exists(Path.Combine(_root.FullName, "custom")));
    }

    [Theory]
    [InlineData("", McpProtocolErrorCodes.InvalidParams)]
    [InlineData(" \r\n ", McpProtocolErrorCodes.InvalidParams)]
    [InlineData("中文内容", McpProtocolErrorCodes.RequestTooLarge)]
    public async Task InvalidContentDoesNotOverwriteTheExistingFile(string content, int expectedCode)
    {
        await File.WriteAllTextAsync(Path.Combine(_root.FullName, "language.md"), "# original");
        using var management = new McpAiGuidanceManagementService(new McpAiGuidanceProvider(
            new McpAiGuidanceOptions { SereinLangFilePath = "language.md", MaxBytes = 10 }, _root.FullName));
        var original = await management.ReadAsync("sereinlang");
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.SaveAsync("sereinlang", new(content, original.Revision)));
        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(original, await management.ReadAsync("sereinlang"));
    }

    [Fact]
    public async Task CannotSelectAnArbitraryFileOrOmitTheRevision()
    {
        using var management = CreateManagement();
        var unknown = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.SaveAsync("../outside.md", new("# outside", "revision")));
        Assert.Equal(McpProtocolErrorCodes.ResourceNotFound, unknown.Code);
        var missingRevision = await Assert.ThrowsAsync<McpProtocolException>(() =>
            management.SaveAsync("sereinlang", new("# content", "")));
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, missingRevision.Code);
        Assert.Empty(_root.EnumerateFileSystemInfos());
    }

    [Fact]
    public async Task InvalidDiskContentDoesNotSilentlyFallBackToDefaults()
    {
        await File.WriteAllTextAsync(Path.Combine(_root.FullName, "language.md"), " ");
        var provider = new McpAiGuidanceProvider(
            new McpAiGuidanceOptions { SereinLangFilePath = "language.md" }, _root.FullName);
        var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
            provider.ReadAsync(McpAiGuidanceId.SereinLang, CancellationToken.None));
        Assert.Equal(McpProtocolErrorCodes.ResourceNotFound, error.Code);
    }

    [Fact]
    public async Task RejectsWritesThroughDirectoryLinks()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root.FullName, "target"));
        var link = Path.Combine(_root.FullName, "linked");
        if (OperatingSystem.IsWindows())
        {
            // Directory junctions do not require the Windows symlink privilege.
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target.FullName })
                startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo)!;
            await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target.FullName);
        }

        try
        {
            using var management = new McpAiGuidanceManagementService(new McpAiGuidanceProvider(
                new McpAiGuidanceOptions { SereinLangFilePath = "linked/language.md" }, _root.FullName));
            var original = await management.ReadAsync("sereinlang");
            var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
                management.SaveAsync("sereinlang", new("# redirected write", original.Revision)));
            Assert.Equal(McpProtocolErrorCodes.PermissionDenied, error.Code);
            Assert.Empty(target.EnumerateFileSystemInfos());
        }
        finally
        {
            // Remove the link before its target; never recursively traverse it during cleanup.
            Directory.Delete(link);
        }
    }

    private McpAiGuidanceManagementService CreateManagement()
        => new(new McpAiGuidanceProvider(new McpAiGuidanceOptions(), _root.FullName));

    public void Dispose() => _root.Delete(recursive: true);
}
