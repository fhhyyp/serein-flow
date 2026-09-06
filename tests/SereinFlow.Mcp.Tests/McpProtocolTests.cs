using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

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
            "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://ai/guide\"}}",
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://ai/skills/sereinlang\"}}",
            "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://projects\"}}",
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"prompts/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"prompts/get\",\"params\":{\"name\":\"sereinflow.inspect\",\"arguments\":{\"request\":\"show projects\"}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"tools/call\",\"params\":{\"name\":\"example\",\"arguments\":{\"value\":42}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":10,\"method\":\"resources/templates/list\"}"));
        var output = new StringWriter();

        await server.RunAsync(input, output);

        var responses = output.ToString()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => JsonDocument.Parse(line))
            .ToArray();
        Assert.Equal(10, responses.Length);

        var initialize = responses[0].RootElement;
        Assert.Equal("2.0", initialize.GetProperty("jsonrpc").GetString());
        Assert.Equal("2025-06-18", initialize.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.False(initialize.TryGetProperty("error", out _));
        var capabilities = initialize.GetProperty("result").GetProperty("capabilities");
        Assert.Equal(JsonValueKind.Object, capabilities.GetProperty("tools").ValueKind);
        Assert.Equal(JsonValueKind.Object, capabilities.GetProperty("resources").ValueKind);
        Assert.Equal(JsonValueKind.Object, capabilities.GetProperty("prompts").ValueKind);
        Assert.False(capabilities.GetProperty("resources").GetProperty("subscribe").GetBoolean());
        Assert.False(capabilities.GetProperty("resources").GetProperty("listChanged").GetBoolean());
        Assert.False(capabilities.GetProperty("tools").GetProperty("listChanged").GetBoolean());
        Assert.False(capabilities.GetProperty("prompts").GetProperty("listChanged").GetBoolean());
        var instructions = initialize.GetProperty("result").GetProperty("instructions").GetString();
        Assert.Contains("sereinflow://ai/guide", instructions, StringComparison.Ordinal);
        Assert.Contains("sereinflow://ai/skills/sereinlang", instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("# SereinFlow", instructions, StringComparison.Ordinal);

        var tools = responses[1].RootElement.GetProperty("result").GetProperty("tools");
        Assert.Equal("example", tools[0].GetProperty("name").GetString());

        var resources = responses[2].RootElement.GetProperty("result").GetProperty("resources");
        Assert.Equal("sereinflow://ai/guide", resources[0].GetProperty("uri").GetString());
        Assert.Equal("sereinflow://ai/skills/sereinflow", resources[1].GetProperty("uri").GetString());
        Assert.Equal("sereinflow://ai/skills/sereinlang", resources[2].GetProperty("uri").GetString());
        Assert.Equal("sereinflow://ai/skills/sereinflow-library-package", resources[3].GetProperty("uri").GetString());

        var guideText = responses[3].RootElement
            .GetProperty("result")
            .GetProperty("contents")[0]
            .GetProperty("text")
            .GetString();
        Assert.StartsWith("# SereinFlow MCP AI Guide", guideText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"# SereinFlow MCP AI Guide", guideText, StringComparison.Ordinal);

        var skillText = responses[4].RootElement
            .GetProperty("result")
            .GetProperty("contents")[0]
            .GetProperty("text")
            .GetString();
        Assert.StartsWith("# SereinLang MCP Skill", skillText, StringComparison.Ordinal);
        Assert.DoesNotContain("# SereinFlow MCP Skill", skillText, StringComparison.Ordinal);

        var resourceText = responses[5].RootElement
            .GetProperty("result")
            .GetProperty("contents")[0]
            .GetProperty("text")
            .GetString();
        Assert.Contains("project", resourceText, StringComparison.Ordinal);

        var prompts = responses[6].RootElement.GetProperty("result").GetProperty("prompts");
        Assert.Equal("sereinflow.inspect", prompts[0].GetProperty("name").GetString());

        var promptText = responses[7].RootElement
            .GetProperty("result")
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetProperty("text")
            .GetString();
        Assert.Contains("show projects", promptText, StringComparison.Ordinal);

        var toolText = responses[8].RootElement
            .GetProperty("result")
            .GetProperty("structuredContent")
            .GetProperty("accepted")
            .GetBoolean();
        Assert.True(toolText);
        Assert.Equal(1, backend.CallCount);

        var templates = responses[9].RootElement
            .GetProperty("result")
            .GetProperty("resourceTemplates");
        Assert.Equal("sereinflow://projects/{projectId}", templates[0].GetProperty("uriTemplate").GetString());

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
        Assert.Equal(McpProtocolErrorCodes.MethodNotFound, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("result", out _));
        using var second = JsonDocument.Parse(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)[1]);
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, second.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.False(second.RootElement.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task InternalErrorsReturnAStableDiagnosticIdWithoutExceptionDetails()
    {
        var server = new SereinFlowMcpServer(new ThrowingBackend());

        var response = await server.HandleRequestAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"boom\"}}");

        using var document = JsonDocument.Parse(response!);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(McpProtocolErrorCodes.InternalError, error.GetProperty("code").GetInt32());
        Assert.Equal("The MCP request failed internally.", error.GetProperty("message").GetString());
        var data = error.GetProperty("data");
        Assert.Equal(McpErrorCodes.InternalError, data.GetProperty("code").GetString());
        Assert.Matches("^[0-9a-f]{32}$", data.GetProperty("diagnosticId").GetString());
        Assert.DoesNotContain("secret", response!, StringComparison.OrdinalIgnoreCase);
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
                new("sereinflow://ai/guide", "ai-guide", "AI guide", "text/markdown"),
                new("sereinflow://ai/skills/sereinflow", "sereinflow", "SereinFlow skill", "text/markdown"),
                new("sereinflow://ai/skills/sereinlang", "sereinlang", "SereinLang skill", "text/markdown"),
                new("sereinflow://ai/skills/sereinflow-library-package", "sereinflow-library-package", "Library skill", "text/markdown"),
                new("sereinflow://projects", "projects", "Projects")]);

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([
                new("sereinflow://projects/{projectId}", "project", "Project")]);

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([
                new("sereinflow.inspect", "Inspect", [new("request", "Request")])]);

        public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
            => Task.FromResult(new McpPromptResult(
                "Inspection",
                [new McpPromptMessage("user", new McpPromptContent("text", $"Inspect: {arguments.GetProperty("request").GetString()}"))]));

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpToolDescriptor>>([
                new("example", "Example", JsonSerializer.SerializeToElement(new { type = "object" }))]);

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
            => Task.FromResult(
                uri switch
                {
                    "sereinflow://ai/guide" => new McpResourceReadResult(uri, "# SereinFlow MCP AI Guide\n", "text/markdown"),
                    "sereinflow://ai/skills/sereinlang" => new McpResourceReadResult(uri, "# SereinLang MCP Skill\n", "text/markdown"),
                    _ => new McpResourceReadResult(uri, new { project = "demo" })
                });

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

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([]);

        public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
            => throw new NotSupportedException();

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

    private sealed class ThrowingBackend : ISereinFlowMcpBackend
    {
        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([]);

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([]);

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([]);

        public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpToolDescriptor>>([]);

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
            => throw new InvalidOperationException("secret backend failure");

        public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
            => throw new InvalidOperationException("secret backend failure");
    }
}
