using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.McpServer;

namespace SereinFlow.McpServer.Tests;

public sealed class McpProtocolTests
{
    [Fact]
    public async Task ServesInitializationCatalogAndReadOnlyCallsOverStdio()
    {
        var backend = new FakeBackend();
        var server = new SereinFlowMcpServer(backend);
        var input = new StringReader(string.Join('\n',
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\"}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"resources/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://projects\"}}",
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"example\",\"arguments\":{\"value\":42}}}"));
        var output = new StringWriter();

        await server.RunAsync(input, output);

        var responses = output.ToString()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => JsonDocument.Parse(line))
            .ToArray();
        Assert.Equal(5, responses.Length);

        var initialize = responses[0].RootElement;
        Assert.Equal("2.0", initialize.GetProperty("jsonrpc").GetString());
        Assert.Equal("2025-06-18", initialize.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.False(initialize.TryGetProperty("error", out _));
        Assert.True(initialize.GetProperty("result").GetProperty("capabilities").GetProperty("tools").ValueKind == JsonValueKind.Object);

        var tools = responses[1].RootElement.GetProperty("result").GetProperty("tools");
        Assert.Equal("example", tools[0].GetProperty("name").GetString());

        var resources = responses[2].RootElement.GetProperty("result").GetProperty("resources");
        Assert.Equal("sereinflow://projects", resources[0].GetProperty("uri").GetString());

        var resourceText = responses[3].RootElement
            .GetProperty("result")
            .GetProperty("contents")[0]
            .GetProperty("text")
            .GetString();
        Assert.Contains("project", resourceText, StringComparison.Ordinal);

        var toolText = responses[4].RootElement
            .GetProperty("result")
            .GetProperty("structuredContent")
            .GetProperty("accepted")
            .GetBoolean();
        Assert.True(toolText);
        Assert.Equal(1, backend.CallCount);

        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task ReturnsJsonRpcErrorsForUnknownMethodsAndInvalidParameters()
    {
        var server = new SereinFlowMcpServer(new FakeBackend());
        var input = new StringReader(string.Join('\n',
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"unknown\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{}}"));
        var output = new StringWriter();

        await server.RunAsync(input, output);

        using var document = JsonDocument.Parse(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal(-32601, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("result", out _));
        using var second = JsonDocument.Parse(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)[1]);
        Assert.Equal(-32602, second.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.False(second.RootElement.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task DispatcherAddsRequestIdToSharedContextAndRestoresTransportContext()
    {
        var accessor = new McpRequestContextAccessor();
        var principal = new McpPrincipal("stdio-key", null, new HashSet<McpPermissionDto>(), IsLocal: true);
        accessor.Current = new McpRequestContext(principal, "stdio");
        var backend = new ContextBackend(accessor);
        var server = new SereinFlowMcpServer(backend, requestContextAccessor: accessor);

        await server.HandleRequestAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"tools/call\",\"params\":{\"name\":\"example\"}}");

        Assert.Equal("42", backend.RequestId);
        Assert.Same(principal, accessor.Current!.Principal);
        Assert.Equal("stdio", accessor.Current.Transport);
        Assert.Null(accessor.Current.RequestId);
    }

    private sealed class FakeBackend : ISereinFlowMcpBackend
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([
                new("sereinflow://projects", "projects", "Projects")]);

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([
                new("sereinflow://projects/{projectId}", "project", "Project")]);

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpToolDescriptor>>([
                new("example", "Example", JsonSerializer.SerializeToElement(new { type = "object" }))]);

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
            => Task.FromResult(new McpResourceReadResult(uri, new { project = "demo" }));

        public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new McpToolCallResult(new { accepted = true }));
        }
    }

    private sealed class ContextBackend(IMcpRequestContextAccessor accessor) : ISereinFlowMcpBackend
    {
        public string? RequestId { get; private set; }

        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([]);

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([]);

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpToolDescriptor>>([]);

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            RequestId = accessor.Current?.RequestId;
            return Task.FromResult(new McpToolCallResult(new { accepted = true }));
        }
    }
}
