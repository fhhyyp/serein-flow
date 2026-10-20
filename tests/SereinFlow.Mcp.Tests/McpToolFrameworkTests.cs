using System.Collections.Generic;
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
        var selectResult = catalog.Descriptors.Select(static item => item.Name);
        Assert.Equal(["first", "second"], selectResult);
        var tryGetResult = catalog.TryGet("second", out var resolved);
        Assert.True(tryGetResult);
        Assert.Same(second, resolved);
        Assert.Throws<ArgumentException>(() =>
        {
            IEnumerable<IMcpTool> evaluatedValue = [first, CreateTool("first", McpToolExecutionKind.Read)];
            var mcpToolCatalog = new McpToolCatalog(evaluatedValue);
            return mcpToolCatalog;
        });
    }

    [Fact]
    public void ReadToolsCannotRequireAnIdempotencyKey()
    {
        var serializeToElementResult = JsonSerializer.SerializeToElement(new { type = "object" });
        var descriptor = new McpToolDescriptor("read", "Read tool", serializeToElementResult);
        Assert.Throws<ArgumentException>(() =>
        {
            var mcpToolDefinition = new McpToolDefinition(descriptor, McpToolExecutionKind.Read, static (_, _, _) =>
            {
                var fromResultResult = Task.FromResult<object?>(new { accepted = true });
                return fromResultResult;
            }, requiresIdempotencyKey: true);
            return mcpToolDefinition;
        });
    }

    private static McpToolDefinition CreateTool(string name, McpToolExecutionKind executionKind)
    {
        var serializeToElementResult2 = JsonSerializer.SerializeToElement(new { type = "object" });
        var mcpToolDescriptor = new McpToolDescriptor(name, name, serializeToElementResult2);
        McpToolDefinition evaluatedValue3 = new(mcpToolDescriptor, executionKind, static (_, _, _) =>
        {
            var fromResultResult1 = Task.FromResult<object?>(new { accepted = true });
            return fromResultResult1;
        });
        return evaluatedValue3;
    }
}
