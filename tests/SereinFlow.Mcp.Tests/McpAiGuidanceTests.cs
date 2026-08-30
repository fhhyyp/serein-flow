using Microsoft.Extensions.Configuration;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpAiGuidanceTests
{
    [Fact]
    public void DefaultsToTheSingleAuthoritativeSereinLangSkillFile()
    {
        var options = McpAiGuidanceOptions.FromConfiguration(
            new ConfigurationBuilder().Build());

        Assert.Equal("mcp/sereinlang-skill.md", options.SereinLangFilePath);
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
                    MaxBytes = 4096
                },
                root.FullName);

            var index = await provider.ReadAsync(McpAiGuidance.ResourceUri, CancellationToken.None);
            var lang = await provider.ReadAsync(McpAiGuidance.SereinLangResourceUri, CancellationToken.None);
            var library = await provider.ReadAsync(McpAiGuidance.LibraryPackageResourceUri, CancellationToken.None);

            Assert.Equal("# index", index.Value);
            Assert.Equal("# lang-v1", lang.Value);
            Assert.Equal("# library-v1", library.Value);

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
    }
}
