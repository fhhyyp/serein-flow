using Microsoft.Extensions.Configuration;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpAiGuidanceTests
{
    [Fact]
    public void DefaultsToTheCapabilityIndexAndFocusedSereinLangModule()
    {
        var options = McpAiGuidanceOptions.FromConfiguration(
            new ConfigurationBuilder().Build());

        Assert.Equal("mcp/sereinlang-skill.md", options.SereinLangFilePath);
        Assert.Equal(
            "mcp/sereinlang-syntax-skill.md",
            options.ModuleFilePaths["sereinlang.syntax"]);
        Assert.Equal(
            "mcp/sereinflow-workpieces-skill.md",
            options.ModuleFilePaths["sereinflow.workpieces"]);
    }

    [Fact]
    public void AllowsFocusedModulePathOverrides()
    {
        var options = McpAiGuidanceOptions.FromConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:Mcp:AiGuidance:Modules:sereinlang.syntax"] = "custom/lang.md"
                })
                .Build());

        Assert.Equal("custom/lang.md", options.ModuleFilePaths["sereinlang.syntax"]);
    }

    [Fact]
    public void RejectsInvalidFocusedModulePathOverrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SereinFlow:Mcp:AiGuidance:Modules:sereinlang.syntax"] = "../outside.md"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            McpAiGuidanceOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void RejectsUnknownFocusedModuleKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SereinFlow:Mcp:AiGuidance:Modules:unknown"] = "custom/unknown.md"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            McpAiGuidanceOptions.FromConfiguration(configuration));
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
            var provider = new McpAiGuidanceProvider(
                new McpAiGuidanceOptions { FilePath = "skill.md", MaxBytes = 4096 },
                root.FullName);

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
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "index.md"), "# index");
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "flow.md"), "# flow-v1");
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "lang.md"), "# lang-v1");
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "library.md"), "# library-v1");

            var provider = new McpAiGuidanceProvider(
                new McpAiGuidanceOptions
                {
                    FilePath = "index.md",
                    SereinFlowFilePath = "flow.md",
                    SereinLangFilePath = "lang.md",
                    LibraryPackageFilePath = "library.md",
                    ModuleFilePaths = new Dictionary<string, string>
                    {
                        ["sereinlang.syntax"] = "lang-module.md",
                        ["library.upgrade"] = "library-module.md"
                    },
                    MaxBytes = 4096
                },
                root.FullName);

            await File.WriteAllTextAsync(Path.Combine(root.FullName, "lang-module.md"), "# lang-module");
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "library-module.md"), "# library-module");

            var index = await provider.ReadAsync(McpAiGuidance.ResourceUri, CancellationToken.None);
            var lang = await provider.ReadAsync(McpAiGuidance.SereinLangResourceUri, CancellationToken.None);
            var library = await provider.ReadAsync(McpAiGuidance.LibraryPackageResourceUri, CancellationToken.None);
            var langModule = await provider.ReadAsync(McpAiGuidance.SereinLangSyntaxResourceUri, CancellationToken.None);

            Assert.Equal("# index", index.Value);
            Assert.Equal("# lang-v1", lang.Value);
            Assert.Equal("# library-v1", library.Value);
            Assert.Equal("# lang-module", langModule.Value);

            await File.WriteAllTextAsync(Path.Combine(root.FullName, "lang.md"), "# lang-v2");
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
        Assert.Throws<InvalidOperationException>(() => new McpAiGuidanceProvider(
            new McpAiGuidanceOptions { FilePath = "..\\outside.md" },
            Path.GetTempPath()));

        Assert.Throws<InvalidOperationException>(() => new McpAiGuidanceProvider(
            new McpAiGuidanceOptions { FilePath = Path.Combine(Path.GetTempPath(), "outside.md") },
            Path.GetTempPath()));

        Assert.Throws<InvalidOperationException>(() => new McpAiGuidanceProvider(
            new McpAiGuidanceOptions
            {
                ModuleFilePaths = new Dictionary<string, string>
                {
                    ["sereinlang.syntax"] = "..\\outside.md"
                }
            },
            Path.GetTempPath()));
    }

    [Fact]
    public async Task LibraryUpgradePromptUsesTheLibraryPackageGuidance()
    {
        var root = Directory.CreateTempSubdirectory("sereinflow-mcp-upgrade-prompt-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "library.md"), "# library upgrade guidance");
            var provider = new McpAiGuidanceProvider(
                new McpAiGuidanceOptions
                {
                    FilePath = "index.md",
                    SereinFlowFilePath = "flow.md",
                    SereinLangFilePath = "lang.md",
                    LibraryPackageFilePath = "library.md",
                    ModuleFilePaths = new Dictionary<string, string>
                    {
                        ["library.upgrade"] = "library.md"
                    },
                    MaxBytes = 4096
                },
                root.FullName);

            var result = await McpPromptCatalog.GetAsync(
                "sereinflow.upgrade-library",
                System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Upgrade image library" }),
                provider,
                CancellationToken.None);

            Assert.Equal("SereinFlow project library upgrade workflow", result.Description);
            Assert.Contains("# library upgrade guidance", result.Messages.Single().Content.Text, StringComparison.Ordinal);
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
                ["library.build"] = "build.md",
                ["library.zip"] = "zip.md",
                ["library.metadata"] = "metadata.md",
                ["library.import"] = "import.md"
            };
            foreach (var (key, path) in modulePaths)
                await File.WriteAllTextAsync(Path.Combine(root.FullName, path), $"#{key}");

            var provider = new McpAiGuidanceProvider(
                new McpAiGuidanceOptions { ModuleFilePaths = modulePaths, MaxBytes = 4096 },
                root.FullName);

            var result = await McpPromptCatalog.GetAsync(
                "sereinflow.package-library",
                System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Package a library" }),
                provider,
                CancellationToken.None);
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
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "runtime.md"), "# runtime guidance");
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "workpieces.md"), "# workpiece guidance");
            var provider = new McpAiGuidanceProvider(
                new McpAiGuidanceOptions
                {
                    ModuleFilePaths = new Dictionary<string, string>
                    {
                        ["sereinflow.runtime"] = "runtime.md",
                        ["sereinflow.workpieces"] = "workpieces.md"
                    },
                    MaxBytes = 4096
                },
                root.FullName);

            var result = await McpPromptCatalog.GetAsync(
                "sereinflow.debug-run",
                System.Text.Json.JsonSerializer.SerializeToElement(new { request = "Inspect uploaded images" }),
                provider,
                CancellationToken.None);
            var text = result.Messages.Single().Content.Text;

            Assert.Contains("# runtime guidance", text, StringComparison.Ordinal);
            Assert.Contains("# workpiece guidance", text, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
