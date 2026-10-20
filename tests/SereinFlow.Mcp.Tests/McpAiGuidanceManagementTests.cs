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
        var mcpAiGuidanceOptions = new McpAiGuidanceOptions();
        var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions, _root.FullName);
        using var management = new McpAiGuidanceManagementService(provider);
        var resources = management.List();
        Assert.Equal(McpAiGuidance.AllResources.Count, resources.Count);
        foreach (var resource in resources)
        {
            var content = await management.ReadAsync(resource.Key);
            Assert.Equal(McpAiGuidanceSourceDto.Embedded, content.Resource.Source);
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(content.Content);
            Assert.False(isNullOrWhiteSpaceResult);
            var evaluatedValue = (await provider.ReadAsync(resource.Uri, CancellationToken.None)).Value;
            Assert.Equal(content.Content, evaluatedValue);
        }

        var enumerateFileSystemInfosResult = _root.EnumerateFileSystemInfos();
        Assert.Empty(enumerateFileSystemInfosResult);
    }

    [Fact]
    public async Task CustomPathCanSavePersistAndRestoreTheEmbeddedDefault()
    {
        var options = new McpAiGuidanceOptions
        {
            SereinLangFilePath = "custom/language.md"
        };
        var provider = new McpAiGuidanceProvider(options, _root.FullName);
        using var management = new McpAiGuidanceManagementService(provider);
        var original = await management.ReadAsync("sereinlang");
        var unchanged = await management.ReadAsync("sereinflow");
        global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue1 = new("# 自定义技能\n保持工具契约。", original.Revision);
        var saved = await management.SaveAsync("sereinlang", evaluatedValue1);
        Assert.Equal(McpAiGuidanceSourceDto.Disk, saved.Resource.Source);
        Assert.NotEqual(original.Revision, saved.Revision);
        var combineResult = Path.Combine(_root.FullName, "custom/language.md");
        var readAllTextAsyncResult = await File.ReadAllTextAsync(combineResult);
        Assert.Equal(saved.Content, readAllTextAsyncResult);
        var restartedProvider = new McpAiGuidanceProvider(options, _root.FullName);
        var evaluatedValue2 = (await restartedProvider.ReadAsync(McpAiGuidanceId.SereinLang, CancellationToken.None)).Value;
        Assert.Equal(saved.Content, evaluatedValue2);
        var readAsyncResult = await management.ReadAsync("sereinflow");
        Assert.Equal(unchanged, readAsyncResult);
        var enumerateFilesResult = _root.EnumerateFiles("*.tmp", SearchOption.AllDirectories);
        Assert.Empty(enumerateFilesResult);
        global::SereinFlow.Contracts.ResetMcpAiGuidanceRequestDto evaluatedValue3 = new(saved.Revision);
        var reset = await management.ResetAsync("sereinlang", evaluatedValue3);
        Assert.Equal(original, reset);
        var combineResult4 = Path.Combine(_root.FullName, "custom/language.md");
        var existsResult = File.Exists(combineResult4);
        Assert.False(existsResult);
        var evaluatedValue5 = (await provider.ReadAsync(McpAiGuidanceId.SereinLang, CancellationToken.None)).Value;
        Assert.Equal(original.Content, evaluatedValue5);
    }

    [Fact]
    public async Task RejectsStaleSaveAndResetWithoutLosingSavedContent()
    {
        using var management = CreateManagement();
        var original = await management.ReadAsync("sereinlang");
        global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue8 = new("# first edit", original.Revision);
        var saved = await management.SaveAsync("sereinlang", evaluatedValue8);
        var saveError = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue6 = new("# stale edit", original.Revision);
            var saveAsyncResult = management.SaveAsync("sereinlang", evaluatedValue6);
            return saveAsyncResult;
        });
        var resetError = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.ResetMcpAiGuidanceRequestDto evaluatedValue7 = new(original.Revision);
            var resetAsyncResult = management.ResetAsync("sereinlang", evaluatedValue7);
            return resetAsyncResult;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, saveError.Code);
        Assert.Equal(McpProtocolErrorCodes.Conflict, resetError.Code);
        var readAsyncResult9 = await management.ReadAsync("sereinlang");
        Assert.Equal(saved, readAsyncResult9);
    }

    [Fact]
    public async Task ConcurrentEditsUsingTheSameRevisionHaveOneWinner()
    {
        using var management = CreateManagement();
        var original = await management.ReadAsync("sereinlang");
        var attemptSaveResult = AttemptSave("# edit one");
        var attemptSaveResult11 = AttemptSave("# edit two");
        var results = await Task.WhenAll(attemptSaveResult, attemptSaveResult11);
        Assert.Single(results, result => result == 0);
        Assert.Single(results, result => result == McpProtocolErrorCodes.Conflict);
        var enumerateFilesResult12 = _root.EnumerateFiles("*.tmp", SearchOption.AllDirectories);
        Assert.Empty(enumerateFilesResult12);
        async Task<int> AttemptSave(string content)
        {
            try
            {
                global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue10 = new(content, original.Revision);
                await management.SaveAsync("sereinlang", evaluatedValue10);
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
        var configurationBuilder = new ConfigurationBuilder();
        var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection();
        var configuration = addInMemoryCollectionResult.Build();
        var provider = new McpAiGuidanceProvider(configuration, _root.FullName);
        using var management = new McpAiGuidanceManagementService(provider);
        var original = await management.ReadAsync("sereinlang");
        var combineResult17 = Path.Combine(_root.FullName, "mcp");
        Directory.CreateDirectory(combineResult17);
        var combineResult18 = Path.Combine(_root.FullName, "mcp/sereinlang-skill.md");
        await File.WriteAllTextAsync(combineResult18, "# external change");
        var externalError = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue13 = new("# stale", original.Revision);
            var saveAsyncResult14 = management.SaveAsync("sereinlang", evaluatedValue13);
            return saveAsyncResult14;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, externalError.Code);
        var current = await management.ReadAsync("sereinlang");
        configuration["SereinFlow:Mcp:AiGuidance:Resources:sereinlang:FilePath"] = "custom/changed.md";
        var configError = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.ResetMcpAiGuidanceRequestDto evaluatedValue15 = new(current.Revision);
            var resetAsyncResult16 = management.ResetAsync("sereinlang", evaluatedValue15);
            return resetAsyncResult16;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, configError.Code);
        var combineResult19 = Path.Combine(_root.FullName, "mcp/sereinlang-skill.md");
        var readAllTextAsyncResult20 = await File.ReadAllTextAsync(combineResult19);
        Assert.Equal("# external change", readAllTextAsyncResult20);
        var combineResult21 = Path.Combine(_root.FullName, "custom");
        var existsResult22 = Directory.Exists(combineResult21);
        Assert.False(existsResult22);
    }

    [Theory]
    [InlineData("", McpProtocolErrorCodes.InvalidParams)]
    [InlineData(" \r\n ", McpProtocolErrorCodes.InvalidParams)]
    [InlineData("中文内容", McpProtocolErrorCodes.RequestTooLarge)]
    public async Task InvalidContentDoesNotOverwriteTheExistingFile(string content, int expectedCode)
    {
        var combineResult25 = Path.Combine(_root.FullName, "language.md");
        await File.WriteAllTextAsync(combineResult25, "# original");
        var mcpAiGuidanceOptions26 = new McpAiGuidanceOptions
        {
            SereinLangFilePath = "language.md",
            MaxBytes = 10
        };
        var mcpAiGuidanceProvider = new McpAiGuidanceProvider(mcpAiGuidanceOptions26, _root.FullName);
        using var management = new McpAiGuidanceManagementService(mcpAiGuidanceProvider);
        var original = await management.ReadAsync("sereinlang");
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue23 = new(content, original.Revision);
            var saveAsyncResult24 = management.SaveAsync("sereinlang", evaluatedValue23);
            return saveAsyncResult24;
        });
        Assert.Equal(expectedCode, exception.Code);
        var readAsyncResult27 = await management.ReadAsync("sereinlang");
        Assert.Equal(original, readAsyncResult27);
    }

    [Fact]
    public async Task CannotSelectAnArbitraryFileOrOmitTheRevision()
    {
        using var management = CreateManagement();
        var unknown = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue28 = new("# outside", "revision");
            var saveAsyncResult29 = management.SaveAsync("../outside.md", evaluatedValue28);
            return saveAsyncResult29;
        });
        Assert.Equal(McpProtocolErrorCodes.ResourceNotFound, unknown.Code);
        var missingRevision = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue30 = new("# content", "");
            var saveAsyncResult31 = management.SaveAsync("sereinlang", evaluatedValue30);
            return saveAsyncResult31;
        });
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, missingRevision.Code);
        var enumerateFileSystemInfosResult32 = _root.EnumerateFileSystemInfos();
        Assert.Empty(enumerateFileSystemInfosResult32);
    }

    [Fact]
    public async Task InvalidDiskContentDoesNotSilentlyFallBackToDefaults()
    {
        var combineResult34 = Path.Combine(_root.FullName, "language.md");
        await File.WriteAllTextAsync(combineResult34, " ");
        var mcpAiGuidanceOptions35 = new McpAiGuidanceOptions
        {
            SereinLangFilePath = "language.md"
        };
        var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions35, _root.FullName);
        var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var readAsyncResult33 = provider.ReadAsync(McpAiGuidanceId.SereinLang, CancellationToken.None);
            return readAsyncResult33;
        });
        Assert.Equal(McpProtocolErrorCodes.ResourceNotFound, error.Code);
    }

    [Fact]
    public async Task RejectsWritesThroughDirectoryLinks()
    {
        var combineResult41 = Path.Combine(_root.FullName, "target");
        var target = Directory.CreateDirectory(combineResult41);
        var link = Path.Combine(_root.FullName, "linked");
        if (OperatingSystem.IsWindows())
        {
            // Directory junctions do not require the Windows symlink privilege.
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            foreach (var argument in new[]
            {
                "/c",
                "mklink",
                "/J",
                link,
                target.FullName
            }

            )
            {
                startInfo.ArgumentList.Add(argument);
            }

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
            var mcpAiGuidanceOptions38 = new McpAiGuidanceOptions
            {
                SereinLangFilePath = "linked/language.md"
            };
            var mcpAiGuidanceProvider39 = new McpAiGuidanceProvider(mcpAiGuidanceOptions38, _root.FullName);
            using var management = new McpAiGuidanceManagementService(mcpAiGuidanceProvider39);
            var original = await management.ReadAsync("sereinlang");
            var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
            {
                global::SereinFlow.Contracts.SaveMcpAiGuidanceRequestDto evaluatedValue36 = new("# redirected write", original.Revision);
                var saveAsyncResult37 = management.SaveAsync("sereinlang", evaluatedValue36);
                return saveAsyncResult37;
            });
            Assert.Equal(McpProtocolErrorCodes.PermissionDenied, error.Code);
            var enumerateFileSystemInfosResult40 = target.EnumerateFileSystemInfos();
            Assert.Empty(enumerateFileSystemInfosResult40);
        }
        finally
        {
            // Remove the link before its target; never recursively traverse it during cleanup.
            Directory.Delete(link);
        }
    }

    private McpAiGuidanceManagementService CreateManagement()
    {
        var mcpAiGuidanceOptions42 = new McpAiGuidanceOptions();
        var mcpAiGuidanceProvider43 = new McpAiGuidanceProvider(mcpAiGuidanceOptions42, _root.FullName);
        global::SereinFlow.Mcp.McpAiGuidanceManagementService evaluatedValue44 = new(mcpAiGuidanceProvider43);
        return evaluatedValue44;
    }

    public void Dispose()
    {
        _root.Delete(recursive: true);
    }
}
