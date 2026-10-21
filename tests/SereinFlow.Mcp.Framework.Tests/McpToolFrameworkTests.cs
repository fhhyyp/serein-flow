using System.Text.Json;
using SereinFlow.Mcp.Framework;

namespace SereinFlow.Mcp.Framework.Tests;

public sealed class McpToolFrameworkTests
{
    [Fact]
    public void CatalogPreservesDeclaredOrderAndRejectsDuplicateNames()
    {
        var first = CreateTool("first");
        var second = CreateTool("second");
        var catalog = new McpToolCatalog([first, second]);
        Assert.Equal(["first", "second"], catalog.Descriptors.Select(item => item.Name));
        Assert.True(catalog.TryGet("second", out var resolved));
        Assert.Same(second, resolved);
        Assert.Throws<ArgumentException>(() => new McpToolCatalog([first, CreateTool("first")]));
    }

    [Fact]
    public void ReadToolsCannotRequireAnIdempotencyKey()
    {
        var descriptor = new McpToolDescriptor("read", "Read tool", JsonSerializer.SerializeToElement(new { type = "object" }));
        var metadata = new McpToolAttribute("read") { RequiresIdempotencyKey = true };
        Assert.Throws<ArgumentException>(() => new McpToolDefinition(descriptor, metadata,
            static (_, _, _) => Task.FromResult<object?>(new { accepted = true })));
    }

    private static McpToolDefinition CreateTool(string name) => new(
        new McpToolDescriptor(name, name, JsonSerializer.SerializeToElement(new { type = "object" })),
        new McpToolAttribute(name), static (_, _, _) => Task.FromResult<object?>(new { accepted = true }));
}
