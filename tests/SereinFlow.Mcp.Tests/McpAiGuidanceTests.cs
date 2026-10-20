using Microsoft.Extensions.Configuration;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;
public sealed class McpAiGuidanceTests
{
    [Fact]
    public void DefaultsToTheCapabilityIndexAndFocusedSereinLangModule()
    {
        var configurationBuilder = new ConfigurationBuilder();
        var buildResult = configurationBuilder.Build();
        var options = McpAiGuidanceOptions.FromConfiguration(buildResult);
        Assert.Equal("mcp/sereinlang-skill.md", options.SereinLangFilePath);
        Assert.Equal("mcp/sereinlang-syntax-skill.md", options.ModuleFilePaths["sereinlang.syntax"]);
        Assert.Equal("mcp/sereinflow-workpieces-skill.md", options.ModuleFilePaths["sereinflow.workpieces"]);
        Assert.Equal("mcp/sereinflow-ui-ux-skill.md", options.ModuleFilePaths["sereinflow.ui-ux"]);
    }

    [Fact]
    public void EveryTypedGuidanceIdResolvesToOneRegisteredResource()
    {
        var selectResult = McpAiGuidance.AllResources.Select(static resource => resource.Uri);
        var registeredUris = selectResult.ToHashSet(StringComparer.Ordinal);
        foreach (var id in Enum.GetValues<McpAiGuidanceId>())
        {
            var getUriResult = McpAiGuidance.GetUri(id);
            Assert.Contains(getUriResult, registeredUris);
        }

        var evaluatedValue = Enum.GetValues<McpAiGuidanceId>().Length;
        var selectResult1 = McpAiGuidance.AllResources.Select(static resource => resource.Uri);
        var distinctResult = selectResult1.Distinct(StringComparer.Ordinal);
        var countResult = distinctResult.Count();
        Assert.Equal(evaluatedValue, countResult);
    }

    [Fact]
    public void AllowsFocusedModulePathOverrides()
    {
        var configurationBuilder2 = new ConfigurationBuilder();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:Mcp:AiGuidance:Modules:sereinlang.syntax"] = "custom/lang.md"
        };
        var addInMemoryCollectionResult = configurationBuilder2.AddInMemoryCollection(dictionary);
        var buildResult3 = addInMemoryCollectionResult.Build();
        var options = McpAiGuidanceOptions.FromConfiguration(buildResult3);
        Assert.Equal("custom/lang.md", options.ModuleFilePaths["sereinlang.syntax"]);
    }

    [Fact]
    public void RejectsInvalidFocusedModulePathOverrides()
    {
        var configurationBuilder4 = new ConfigurationBuilder();
        var dictionary5 = new Dictionary<string, string?>
        {
            ["SereinFlow:Mcp:AiGuidance:Modules:sereinlang.syntax"] = "../outside.md"
        };
        var addInMemoryCollectionResult6 = configurationBuilder4.AddInMemoryCollection(dictionary5);
        var configuration = addInMemoryCollectionResult6.Build();
        Assert.Throws<InvalidOperationException>(() =>
        {
            var fromConfigurationResult = McpAiGuidanceOptions.FromConfiguration(configuration);
            return fromConfigurationResult;
        });
    }

    [Fact]
    public void RejectsUnknownFocusedModuleKeys()
    {
        var configurationBuilder8 = new ConfigurationBuilder();
        var dictionary9 = new Dictionary<string, string?>
        {
            ["SereinFlow:Mcp:AiGuidance:Modules:unknown"] = "custom/unknown.md"
        };
        var addInMemoryCollectionResult10 = configurationBuilder8.AddInMemoryCollection(dictionary9);
        var configuration = addInMemoryCollectionResult10.Build();
        Assert.Throws<InvalidOperationException>(() =>
        {
            var fromConfigurationResult7 = McpAiGuidanceOptions.FromConfiguration(configuration);
            return fromConfigurationResult7;
        });
    }

    [Fact]
    public async Task RuntimeConfigurationCanReplaceGuidanceUriAndContent()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-dynamic-guidance-");
        try
        {
            var combineResult = Path.Combine(root.FullName, "dynamic-v1.md");
            await File.WriteAllTextAsync(combineResult, "# dynamic-v1");
            var combineResult11 = Path.Combine(root.FullName, "dynamic-v2.md");
            await File.WriteAllTextAsync(combineResult11, "# dynamic-v2");
            var configurationBuilder12 = new ConfigurationBuilder();
            var dictionary13 = new Dictionary<string, string?>
            {
                ["SereinFlow:Mcp:AiGuidance:Resources:ai.guide:Uri"] = "sereinflow://ai/runtime-guide-v1",
                ["SereinFlow:Mcp:AiGuidance:Resources:ai.guide:FilePath"] = "dynamic-v1.md"
            };
            var addInMemoryCollectionResult14 = configurationBuilder12.AddInMemoryCollection(dictionary13);
            var configuration = addInMemoryCollectionResult14.Build();
            var provider = new McpAiGuidanceProvider(configuration, root.FullName);
            var getUriResult15 = provider.GetUri(McpAiGuidanceId.Guide);
            Assert.Equal("sereinflow://ai/runtime-guide-v1", getUriResult15);
            var evaluatedValue16 = (await provider.ReadAsync(CancellationToken.None)).Value;
            Assert.Equal("# dynamic-v1", evaluatedValue16);
            configuration["SereinFlow:Mcp:AiGuidance:Resources:ai.guide:Uri"] = "sereinflow://ai/runtime-guide-v2";
            configuration["SereinFlow:Mcp:AiGuidance:Resources:ai.guide:FilePath"] = "dynamic-v2.md";
            var getUriResult17 = provider.GetUri(McpAiGuidanceId.Guide);
            Assert.Equal("sereinflow://ai/runtime-guide-v2", getUriResult17);
            var isGuidanceUriResult = provider.IsGuidanceUri("sereinflow://ai/runtime-guide-v2");
            Assert.True(isGuidanceUriResult);
            var evaluatedValue18 = (await provider.ReadAsync("sereinflow://ai/runtime-guide-v2", CancellationToken.None)).Value;
            Assert.Equal("# dynamic-v2", evaluatedValue18);
            var isGuidanceUriResult19 = provider.IsGuidanceUri("sereinflow://ai/runtime-guide-v1");
            Assert.False(isGuidanceUriResult19);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadsTheConfiguredServerFileAtRequestTime()
    {
        Assert.Equal("sereinflow://ai/guide", McpAiGuidance.ResourceUri);
        Assert.Equal("text/markdown", McpAiGuidance.MimeType);
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-guidance-");
        try
        {
            var filePath = Path.Combine(root.FullName, "skill.md");
            await File.WriteAllTextAsync(filePath, "# guide-v1");
            var mcpAiGuidanceOptions = new McpAiGuidanceOptions
            {
                FilePath = "skill.md",
                MaxBytes = 4096
            };
            var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions, root.FullName);
            var first = await provider.ReadAsync(CancellationToken.None);
            Assert.Equal("# guide-v1", first.Value);
            Assert.Equal("text/markdown", first.MimeType);
            await File.WriteAllTextAsync(filePath, "# guide-v2 loaded from server");
            var second = await provider.ReadAsync(CancellationToken.None);
            Assert.Equal("# guide-v2 loaded from server", second.Value);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadsOnlyTheRequestedCapabilityFile()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-skills-");
        try
        {
            var combineResult20 = Path.Combine(root.FullName, "index.md");
            await File.WriteAllTextAsync(combineResult20, "# index");
            var combineResult21 = Path.Combine(root.FullName, FlowErrorCodes.Md);
            await File.WriteAllTextAsync(combineResult21, "# flow-v1");
            var combineResult22 = Path.Combine(root.FullName, "lang.md");
            await File.WriteAllTextAsync(combineResult22, "# lang-v1");
            var combineResult23 = Path.Combine(root.FullName, LibraryErrorCodes.Md);
            await File.WriteAllTextAsync(combineResult23, "# library-v1");
            var mcpAiGuidanceOptions24 = new McpAiGuidanceOptions
            {
                FilePath = "index.md",
                SereinFlowFilePath = FlowErrorCodes.Md,
                SereinLangFilePath = "lang.md",
                LibraryPackageFilePath = LibraryErrorCodes.Md,
                ModuleFilePaths = new Dictionary<string, string>
                {
                    ["sereinlang.syntax"] = "lang-module.md",
                    ["library.upgrade"] = "library-module.md"
                },
                MaxBytes = 4096
            };
            var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions24, root.FullName);
            var combineResult25 = Path.Combine(root.FullName, "lang-module.md");
            await File.WriteAllTextAsync(combineResult25, "# lang-module");
            var combineResult26 = Path.Combine(root.FullName, "library-module.md");
            await File.WriteAllTextAsync(combineResult26, "# library-module");
            var index = await provider.ReadAsync(McpAiGuidance.ResourceUri, CancellationToken.None);
            var lang = await provider.ReadAsync(McpAiGuidance.SereinLangResourceUri, CancellationToken.None);
            var library = await provider.ReadAsync(McpAiGuidance.LibraryPackageResourceUri, CancellationToken.None);
            var langModule = await provider.ReadAsync(McpAiGuidance.SereinLangSyntaxResourceUri, CancellationToken.None);
            Assert.Equal("# index", index.Value);
            Assert.Equal("# lang-v1", lang.Value);
            Assert.Equal("# library-v1", library.Value);
            Assert.Equal("# lang-module", langModule.Value);
            var combineResult27 = Path.Combine(root.FullName, "lang.md");
            await File.WriteAllTextAsync(combineResult27, "# lang-v2");
            var updatedLang = await provider.ReadAsync(McpAiGuidance.SereinLangResourceUri, CancellationToken.None);
            Assert.Equal("# lang-v2", updatedLang.Value);
            Assert.Equal("sereinflow://ai/skills/sereinlang", updatedLang.Uri);
            Assert.DoesNotContain("# lang", (string)index.Value);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void RejectsPathsThatCouldEscapeTheServerContentRoot()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var mcpAiGuidanceOptions28 = new McpAiGuidanceOptions
            {
                FilePath = "..\\outside.md"
            };
            var getTempPathResult = Path.GetTempPath();
            var mcpAiGuidanceProvider = new McpAiGuidanceProvider(mcpAiGuidanceOptions28, getTempPathResult);
            return mcpAiGuidanceProvider;
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var mcpAiGuidanceOptions31 = new McpAiGuidanceOptions
            {
                FilePath = resolveCombineResult()
            };
            var getTempPathResult32 = Path.GetTempPath();
            var mcpAiGuidanceProvider33 = new McpAiGuidanceProvider(mcpAiGuidanceOptions31, getTempPathResult32);
            return mcpAiGuidanceProvider33;
            string resolveCombineResult()
            {
                var getTempPathResult29 = Path.GetTempPath();
                var combineResult30 = Path.Combine(getTempPathResult29, "outside.md");
                return combineResult30;
            }
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var mcpAiGuidanceOptions34 = new McpAiGuidanceOptions
            {
                ModuleFilePaths = new Dictionary<string, string>
                {
                    ["sereinlang.syntax"] = "..\\outside.md"
                }
            };
            var getTempPathResult35 = Path.GetTempPath();
            var mcpAiGuidanceProvider36 = new McpAiGuidanceProvider(mcpAiGuidanceOptions34, getTempPathResult35);
            return mcpAiGuidanceProvider36;
        });
    }

    [Fact]
    public async Task LibraryUpgradePromptUsesTheLibraryPackageGuidance()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-upgrade-prompt-");
        try
        {
            var combineResult37 = Path.Combine(root.FullName, LibraryErrorCodes.Md);
            await File.WriteAllTextAsync(combineResult37, "# library upgrade guidance");
            var mcpAiGuidanceOptions38 = new McpAiGuidanceOptions
            {
                FilePath = "index.md",
                SereinFlowFilePath = FlowErrorCodes.Md,
                SereinLangFilePath = "lang.md",
                LibraryPackageFilePath = LibraryErrorCodes.Md,
                ModuleFilePaths = new Dictionary<string, string>
                {
                    ["library.upgrade"] = LibraryErrorCodes.Md
                },
                MaxBytes = 4096
            };
            var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions38, root.FullName);
            var serializeToElementResult = System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Upgrade image library" });
            var result = await McpPromptCatalog.GetAsync("sereinflow.upgrade-library", serializeToElementResult, provider, CancellationToken.None);
            Assert.Equal("SereinFlow project library upgrade workflow", result.Description);
            var evaluatedValue39 = result.Messages.Single().Content.Text;
            Assert.Contains("# library upgrade guidance", evaluatedValue39, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PackagePromptLoadsOnlyItsFocusedLibraryModules()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-package-prompt-");
        try
        {
            var modulePaths = new Dictionary<string, string>
            {
                [LibraryErrorCodes.Build] = "build.md",
                [LibraryErrorCodes.Zip] = "zip.md",
                [LibraryErrorCodes.Metadata] = "metadata.md",
                [LibraryErrorCodes.Import] = "import.md"
            };
            foreach (var deconstructedEntry in modulePaths)
            {
                var key = deconstructedEntry.Key;
                var path = deconstructedEntry.Value;
                var combineResult1 = Path.Combine(root.FullName, path);
                await File.WriteAllTextAsync(combineResult1, $"#{key}");
            }

            var mcpAiGuidanceOptions40 = new McpAiGuidanceOptions
            {
                ModuleFilePaths = modulePaths,
                MaxBytes = 4096
            };
            var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions40, root.FullName);
            var serializeToElementResult41 = System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Package a library" });
            var result = await McpPromptCatalog.GetAsync("sereinflow.package-library", serializeToElementResult41, provider, CancellationToken.None);
            var text = result.Messages.Single().Content.Text;
            Assert.Contains("#library.build", text, StringComparison.Ordinal);
            Assert.Contains("#library.zip", text, StringComparison.Ordinal);
            Assert.Contains("#library.metadata", text, StringComparison.Ordinal);
            Assert.DoesNotContain("#library.import", text, StringComparison.Ordinal);
            Assert.DoesNotContain("#library.upgrade", text, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DebugPromptLoadsRuntimeAndWorkpieceGuidance()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-workpiece-prompt-");
        try
        {
            var combineResult42 = Path.Combine(root.FullName, "runtime.md");
            await File.WriteAllTextAsync(combineResult42, "# runtime guidance");
            var combineResult43 = Path.Combine(root.FullName, "workpieces.md");
            await File.WriteAllTextAsync(combineResult43, "# workpiece guidance");
            var mcpAiGuidanceOptions44 = new McpAiGuidanceOptions
            {
                ModuleFilePaths = new Dictionary<string, string>
                {
                    ["sereinflow.runtime"] = "runtime.md",
                    ["sereinflow.workpieces"] = "workpieces.md"
                },
                MaxBytes = 4096
            };
            var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions44, root.FullName);
            var serializeToElementResult45 = System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Inspect uploaded images" });
            var result = await McpPromptCatalog.GetAsync("sereinflow.debug-run", serializeToElementResult45, provider, CancellationToken.None);
            var text = result.Messages.Single().Content.Text;
            Assert.Contains("# runtime guidance", text, StringComparison.Ordinal);
            Assert.Contains("# workpiece guidance", text, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task EditFlowPromptLoadsFlowAndUiUxGuidance()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-flow-ui-ux-prompt-");
        try
        {
            var combineResult46 = Path.Combine(root.FullName, "flows.md");
            await File.WriteAllTextAsync(combineResult46, "# flow guidance");
            var combineResult47 = Path.Combine(root.FullName, "ui-ux.md");
            await File.WriteAllTextAsync(combineResult47, "# ui ux guidance");
            var mcpAiGuidanceOptions48 = new McpAiGuidanceOptions
            {
                ModuleFilePaths = new Dictionary<string, string>
                {
                    ["sereinflow.flows"] = "flows.md",
                    ["sereinflow.ui-ux"] = "ui-ux.md"
                },
                MaxBytes = 4096
            };
            var provider = new McpAiGuidanceProvider(mcpAiGuidanceOptions48, root.FullName);
            var serializeToElementResult49 = System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Lay out a flow" });
            var result = await McpPromptCatalog.GetAsync("sereinflow.edit-flow", serializeToElementResult49, provider, CancellationToken.None);
            var text = result.Messages.Single().Content.Text;
            Assert.Contains("# flow guidance", text, StringComparison.Ordinal);
            Assert.Contains("# ui ux guidance", text, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
