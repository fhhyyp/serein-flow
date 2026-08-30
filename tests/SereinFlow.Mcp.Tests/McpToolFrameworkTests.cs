using System.Text.Json;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpToolFrameworkTests
{
    [Fact]
    public void CatalogPreservesDeclaredOrderAndRejectsDuplicateNames()
    {
        var first = CreateTool("first", McpToolExecutionKind.Read);
        var second = CreateTool("second", McpToolExecutionKind.Read);

        var catalog = new McpToolCatalog([first, second]);

        Assert.Equal(["first", "second"], catalog.Descriptors.Select(static item => item.Name));
        Assert.True(catalog.TryGet("second", out var resolved));
        Assert.Same(second, resolved);
        Assert.Throws<ArgumentException>(() => new McpToolCatalog([first, CreateTool("first", McpToolExecutionKind.Read)]));
    }

    [Fact]
    public void ReadToolsCannotRequireAnIdempotencyKey()
    {
        var descriptor = new McpToolDescriptor(
            "read",
            "Read tool",
            JsonSerializer.SerializeToElement(new { type = "object" }));

        Assert.Throws<ArgumentException>(() => new McpToolDefinition(
            descriptor,
            McpToolExecutionKind.Read,
            static (_, _, _) => Task.FromResult<object?>(new { accepted = true }),
            requiresIdempotencyKey: true));
    }

    private static McpToolDefinition CreateTool(string name, McpToolExecutionKind executionKind)
        => new(
            new McpToolDescriptor(name, name, JsonSerializer.SerializeToElement(new { type = "object" })),
            executionKind,
            static (_, _, _) => Task.FromResult<object?>(new { accepted = true }));
}
