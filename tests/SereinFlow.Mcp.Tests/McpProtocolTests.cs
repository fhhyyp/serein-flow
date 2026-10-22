using System.Collections.Generic;
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
        var joinResult = string.Join('\n', "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\"}}", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}", "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"resources/list\"}", "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://ai/guide\"}}", "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://ai/skills/sereinlang\"}}", "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"resources/read\",\"params\":{\"uri\":\"sereinflow://projects\"}}", "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"prompts/list\"}", "{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"prompts/get\",\"params\":{\"name\":\"sereinflow.inspect\",\"arguments\":{\"request\":\"show projects\"}}}", "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"tools/call\",\"params\":{\"name\":\"example\",\"arguments\":{\"value\":42}}}", "{\"jsonrpc\":\"2.0\",\"id\":10,\"method\":\"resources/templates/list\"}");
        var input = new StringReader(joinResult);
        var output = new StringWriter();
        await server.RunAsync(input, output);
        var toStringResult = output.ToString();
        var splitResult = toStringResult.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var selectResult = splitResult.Select(static line =>
        {
            var parseResult = JsonDocument.Parse(line);
            return parseResult;
        });
        var responses = selectResult.ToArray();
        Assert.Equal(10, responses.Length);
        var initialize = responses[0].RootElement;
        var getPropertyResult = initialize.GetProperty("jsonrpc");
        var getStringResult = getPropertyResult.GetString();
        Assert.Equal("2.0", getStringResult);
        var getPropertyResult1 = initialize.GetProperty("result");
        var getPropertyResult2 = getPropertyResult1.GetProperty("protocolVersion");
        var getStringResult3 = getPropertyResult2.GetString();
        Assert.Equal("2025-06-18", getStringResult3);
        var tryGetPropertyResult = initialize.TryGetProperty("error", out _);
        Assert.False(tryGetPropertyResult);
        var getPropertyResult4 = initialize.GetProperty("result");
        var capabilities = getPropertyResult4.GetProperty("capabilities");
        var evaluatedValue = capabilities.GetProperty("tools").ValueKind;
        Assert.Equal(JsonValueKind.Object, evaluatedValue);
        var evaluatedValue5 = capabilities.GetProperty("resources").ValueKind;
        Assert.Equal(JsonValueKind.Object, evaluatedValue5);
        var evaluatedValue6 = capabilities.GetProperty("prompts").ValueKind;
        Assert.Equal(JsonValueKind.Object, evaluatedValue6);
        var getPropertyResult7 = capabilities.GetProperty("resources");
        var getPropertyResult8 = getPropertyResult7.GetProperty("subscribe");
        var getBooleanResult = getPropertyResult8.GetBoolean();
        Assert.False(getBooleanResult);
        var getPropertyResult9 = capabilities.GetProperty("resources");
        var getPropertyResult10 = getPropertyResult9.GetProperty("listChanged");
        var getBooleanResult11 = getPropertyResult10.GetBoolean();
        Assert.False(getBooleanResult11);
        var getPropertyResult12 = capabilities.GetProperty("tools");
        var getPropertyResult13 = getPropertyResult12.GetProperty("listChanged");
        var getBooleanResult14 = getPropertyResult13.GetBoolean();
        Assert.False(getBooleanResult14);
        var getPropertyResult15 = capabilities.GetProperty("prompts");
        var getPropertyResult16 = getPropertyResult15.GetProperty("listChanged");
        var getBooleanResult17 = getPropertyResult16.GetBoolean();
        Assert.False(getBooleanResult17);
        var getPropertyResult18 = initialize.GetProperty("result");
        var getPropertyResult19 = getPropertyResult18.GetProperty("instructions");
        var instructions = getPropertyResult19.GetString();
        Assert.Contains("sereinflow://ai/guide", instructions, StringComparison.Ordinal);
        Assert.Contains("sereinflow://ai/skills/sereinlang", instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("# SereinFlow", instructions, StringComparison.Ordinal);
        var getPropertyResult20 = responses[1].RootElement.GetProperty("result");
        var tools = getPropertyResult20.GetProperty("tools");
        var getPropertyResult21 = tools[0].GetProperty("name");
        var getStringResult22 = getPropertyResult21.GetString();
        Assert.Equal("example", getStringResult22);
        var getPropertyResult23 = responses[2].RootElement.GetProperty("result");
        var resources = getPropertyResult23.GetProperty("resources");
        var getPropertyResult24 = resources[0].GetProperty("uri");
        var getStringResult25 = getPropertyResult24.GetString();
        Assert.Equal("sereinflow://ai/guide", getStringResult25);
        var getPropertyResult26 = resources[1].GetProperty("uri");
        var getStringResult27 = getPropertyResult26.GetString();
        Assert.Equal("sereinflow://ai/skills/sereinflow", getStringResult27);
        var getPropertyResult28 = resources[2].GetProperty("uri");
        var getStringResult29 = getPropertyResult28.GetString();
        Assert.Equal("sereinflow://ai/skills/sereinlang", getStringResult29);
        var getPropertyResult30 = resources[3].GetProperty("uri");
        var getStringResult31 = getPropertyResult30.GetString();
        Assert.Equal("sereinflow://ai/skills/sereinflow-library-package", getStringResult31);
        var evaluatedValue34 = resolveGetPropertyResult()[0];
        var getPropertyResult35 = evaluatedValue34.GetProperty("text");
        var guideText = getPropertyResult35.GetString();
        Assert.StartsWith("# SereinFlow MCP AI Guide", guideText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"# SereinFlow MCP AI Guide", guideText, StringComparison.Ordinal);
        var evaluatedValue39 = resolveGetPropertyResult36()[0];
        var getPropertyResult40 = evaluatedValue39.GetProperty("text");
        var skillText = getPropertyResult40.GetString();
        Assert.StartsWith("# SereinLang MCP Skill", skillText, StringComparison.Ordinal);
        Assert.DoesNotContain("# SereinFlow MCP Skill", skillText, StringComparison.Ordinal);
        var evaluatedValue44 = resolveGetPropertyResult41()[0];
        var getPropertyResult45 = evaluatedValue44.GetProperty("text");
        var resourceText = getPropertyResult45.GetString();
        Assert.Contains("project", resourceText, StringComparison.Ordinal);
        var getPropertyResult46 = responses[6].RootElement.GetProperty("result");
        var prompts = getPropertyResult46.GetProperty("prompts");
        var getPropertyResult47 = prompts[0].GetProperty("name");
        var getStringResult48 = getPropertyResult47.GetString();
        Assert.Equal("sereinflow.inspect", getStringResult48);
        var evaluatedValue52 = resolveGetPropertyResult49()[0];
        var getPropertyResult53 = evaluatedValue52.GetProperty("content");
        var getPropertyResult54 = getPropertyResult53.GetProperty("text");
        var promptText = getPropertyResult54.GetString();
        Assert.Contains("show projects", promptText, StringComparison.Ordinal);
        var getPropertyResult55 = responses[8].RootElement.GetProperty("result");
        var getPropertyResult56 = getPropertyResult55.GetProperty("structuredContent");
        var getPropertyResult57 = getPropertyResult56.GetProperty("accepted");
        var toolText = getPropertyResult57.GetBoolean();
        Assert.True(toolText);
        Assert.Equal(1, backend.CallCount);
        var getPropertyResult58 = responses[9].RootElement.GetProperty("result");
        var templates = getPropertyResult58.GetProperty("resourceTemplates");
        var getPropertyResult59 = templates[0].GetProperty("uriTemplate");
        var getStringResult60 = getPropertyResult59.GetString();
        Assert.Equal("sereinflow://projects/{projectId}", getStringResult60);
        foreach (var response in responses)
        {
            response.Dispose();
        }

        JsonElement resolveGetPropertyResult()
        {
            var getPropertyResult32 = responses[3].RootElement.GetProperty("result");
            var getPropertyResult33 = getPropertyResult32.GetProperty("contents");
            return getPropertyResult33;
        }

        JsonElement resolveGetPropertyResult36()
        {
            var getPropertyResult37 = responses[4].RootElement.GetProperty("result");
            var getPropertyResult38 = getPropertyResult37.GetProperty("contents");
            return getPropertyResult38;
        }

        JsonElement resolveGetPropertyResult41()
        {
            var getPropertyResult42 = responses[5].RootElement.GetProperty("result");
            var getPropertyResult43 = getPropertyResult42.GetProperty("contents");
            return getPropertyResult43;
        }

        JsonElement resolveGetPropertyResult49()
        {
            var getPropertyResult50 = responses[7].RootElement.GetProperty("result");
            var getPropertyResult51 = getPropertyResult50.GetProperty("messages");
            return getPropertyResult51;
        }
    }

    [Fact]
    public async Task ReturnsJsonRpcErrorsForUnknownMethodsAndInvalidParameters()
    {
        var fakeBackend = new FakeBackend();
        var server = new SereinFlowMcpServer(fakeBackend);
        var joinResult61 = string.Join('\n', "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"unknown\"}", "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{}}");
        var input = new StringReader(joinResult61);
        var output = new StringWriter();
        await server.RunAsync(input, output);
        var evaluatedValue64 = resolveSplitResult()[0];
        using var document = JsonDocument.Parse(evaluatedValue64);
        var getPropertyResult65 = document.RootElement.GetProperty("error");
        var getPropertyResult66 = getPropertyResult65.GetProperty("code");
        var getInt32Result = getPropertyResult66.GetInt32();
        Assert.Equal(McpProtocolErrorCodes.MethodNotFound, getInt32Result);
        var tryGetPropertyResult67 = document.RootElement.TryGetProperty("result", out _);
        Assert.False(tryGetPropertyResult67);
        var evaluatedValue71 = resolveSplitResult68()[1];
        using var second = JsonDocument.Parse(evaluatedValue71);
        var getPropertyResult72 = second.RootElement.GetProperty("error");
        var getPropertyResult73 = getPropertyResult72.GetProperty("code");
        var getInt32Result74 = getPropertyResult73.GetInt32();
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, getInt32Result74);
        var tryGetPropertyResult75 = second.RootElement.TryGetProperty("result", out _);
        Assert.False(tryGetPropertyResult75);
        string[] resolveSplitResult()
        {
            var toStringResult62 = output.ToString();
            var splitResult63 = toStringResult62.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return splitResult63;
        }

        string[] resolveSplitResult68()
        {
            var toStringResult69 = output.ToString();
            var splitResult70 = toStringResult69.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return splitResult70;
        }
    }

    [Fact]
    public async Task InternalErrorsReturnAStableDiagnosticIdWithoutExceptionDetails()
    {
        var throwingBackend = new ThrowingBackend();
        var server = new SereinFlowMcpServer(throwingBackend);
        var response = await server.HandleRequestAsync("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"boom\"}}");
        using var document = JsonDocument.Parse(response!);
        var error = document.RootElement.GetProperty("error");
        var getPropertyResult76 = error.GetProperty("code");
        var getInt32Result77 = getPropertyResult76.GetInt32();
        Assert.Equal(McpProtocolErrorCodes.InternalError, getInt32Result77);
        var getPropertyResult78 = error.GetProperty("message");
        var getStringResult79 = getPropertyResult78.GetString();
        Assert.Equal("The MCP request failed internally. MCP 请求发生内部错误。", getStringResult79);
        var data = error.GetProperty("data");
        var getPropertyResult80 = data.GetProperty("code");
        var getStringResult81 = getPropertyResult80.GetString();
        Assert.Equal(McpErrorCodes.InternalError, getStringResult81);
        var getPropertyResult82 = data.GetProperty("diagnosticId");
        var getStringResult83 = getPropertyResult82.GetString();
        Assert.Matches("^[0-9a-f]{32}$", getStringResult83);
        Assert.DoesNotContain("secret", response!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatcherAddsRequestIdToSharedContextAndRestoresTransportContext()
    {
        var accessor = new McpRequestContextAccessor();
        var hashSet = new HashSet<McpPermissionDto>();
        var principal = new McpPrincipal("stdio-key", null, hashSet, IsLocal: true);
        accessor.Current = new McpRequestContext(principal, "stdio");
        var backend = new ContextBackend(accessor);
        var server = new SereinFlowMcpServer(backend, requestContextAccessor: accessor);
        await server.HandleRequestAsync("{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"tools/call\",\"params\":{\"name\":\"example\"}}");
        Assert.Equal("42", backend.RequestId);
        Assert.Same(principal, accessor.Current!.Principal);
        Assert.Equal("stdio", accessor.Current.Transport);
        Assert.Null(accessor.Current.RequestId);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("9007199254740993")]
    [InlineData("\"request-中文\"")]
    [InlineData("null")]
    public async Task NullResultsRetainTheOriginalIdAndExcludeErrors(string idJson)
    {
        var backend = new FakeBackend();
        var server = new SereinFlowMcpServer(backend);
        var request = $"{{\"jsonrpc\":\"2.0\",\"id\":{idJson},\"method\":\"shutdown\"}}";
        var response = await server.HandleRequestAsync(request);
        Assert.NotNull(response);
        using var actual = JsonDocument.Parse(response);
        var expectedJson = $"{{\"jsonrpc\":\"2.0\",\"id\":{idJson},\"result\":null}}";
        using var expected = JsonDocument.Parse(expectedJson);
        var matches = JsonElement.DeepEquals(expected.RootElement, actual.RootElement);
        Assert.True(matches, response);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("9007199254740993")]
    [InlineData("\"request-中文\"")]
    [InlineData("null")]
    public async Task ErrorsRetainTheOriginalIdAndExplicitNullData(string idJson)
    {
        var backend = new FakeBackend();
        var server = new SereinFlowMcpServer(backend);
        var request = $"{{\"jsonrpc\":\"2.0\",\"id\":{idJson},\"method\":\"unknown\"}}";
        var response = await server.HandleRequestAsync(request);
        Assert.NotNull(response);
        using var actual = JsonDocument.Parse(response);
        var expectedJson = $"{{\"jsonrpc\":\"2.0\",\"id\":{idJson},\"error\":{{\"code\":-32601,\"message\":\"MCP method 'unknown' is not supported. 不支持此 MCP 方法。\",\"data\":null}}}}";
        using var expected = JsonDocument.Parse(expectedJson);
        var matches = JsonElement.DeepEquals(expected.RootElement, actual.RootElement);
        Assert.True(matches, response);
    }

    [Fact]
    public async Task SizeLimitRetainsUtf8ByteBoundaryAndCompactErrorShape()
    {
        var backend = new FakeBackend();
        var server = new SereinFlowMcpServer(backend);
        const string request = "{\"jsonrpc\":\"2.0\",\"id\":\"中文\",\"method\":\"ping\"}";
        var response = await server.HandleRequestAsync(request);
        Assert.NotNull(response);
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(response);
        var exactLimitServer = new SereinFlowMcpServer(backend, maxResponseBytes: byteCount);
        var exactResponse = await exactLimitServer.HandleRequestAsync(request);
        Assert.Equal(response, exactResponse);
        var smallerLimitServer = new SereinFlowMcpServer(backend, maxResponseBytes: byteCount - 1);
        var limitedResponse = await smallerLimitServer.HandleRequestAsync(request);
        Assert.NotNull(limitedResponse);
        using var actual = JsonDocument.Parse(limitedResponse);
        const string expectedJson = "{\"jsonrpc\":\"2.0\",\"id\":\"中文\",\"error\":{\"code\":-32013,\"message\":\"The MCP response exceeds the configured size limit. MCP 响应超过配置的大小限制。\"}}";
        using var expected = JsonDocument.Parse(expectedJson);
        var matches = JsonElement.DeepEquals(expected.RootElement, actual.RootElement);
        Assert.True(matches, limitedResponse);
    }

    [Theory]
    [InlineData("ping")]
    [InlineData("unknown")]
    [InlineData("shutdown")]
    public async Task NotificationsDoNotProduceAnEnvelope(string method)
    {
        var backend = new FakeBackend();
        var server = new SereinFlowMcpServer(backend);
        var request = $"{{\"jsonrpc\":\"2.0\",\"method\":\"{method}\"}}";
        var response = await server.HandleRequestAsync(request);
        Assert.Null(response);
    }

    private sealed class FakeBackend : ISereinFlowMcpBackend
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<McpResourceDescriptor> evaluatedValue84 = [new("sereinflow://ai/guide", "ai-guide", "AI guide", "text/markdown"), new("sereinflow://ai/skills/sereinflow", "sereinflow", "SereinFlow skill", "text/markdown"), new("sereinflow://ai/skills/sereinlang", "sereinlang", "SereinLang skill", "text/markdown"), new("sereinflow://ai/skills/sereinflow-library-package", "sereinflow-library-package", "Library skill", "text/markdown"), new("sereinflow://projects", "projects", "Projects")];
            var fromResultResult = Task.FromResult<IReadOnlyList<McpResourceDescriptor>>(evaluatedValue84);
            return fromResultResult;
        }

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<McpResourceTemplateDescriptor> evaluatedValue85 = [new("sereinflow://projects/{projectId}", "project", "Project")];
            var fromResultResult86 = Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>(evaluatedValue85);
            return fromResultResult86;
        }

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<McpPromptDescriptor> evaluatedValue89 = [resolveevaluatedValue()];
            var fromResultResult90 = Task.FromResult<IReadOnlyList<McpPromptDescriptor>>(evaluatedValue89);
            return fromResultResult90;
            McpPromptDescriptor resolveevaluatedValue()
            {
                IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue87 = [new("request", "Request")];
                McpPromptDescriptor evaluatedValue88 = new("sereinflow.inspect", "Inspect", evaluatedValue87);
                return evaluatedValue88;
            }
        }

        public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            IReadOnlyList<McpPromptMessage> evaluatedValue94 = [resolveMcpPromptMessage()];
            var mcpPromptResult = new McpPromptResult("Inspection", evaluatedValue94);
            var fromResultResult95 = Task.FromResult(mcpPromptResult);
            return fromResultResult95;
            McpPromptMessage resolveMcpPromptMessage()
            {
                var evaluatedValue93 = $"Inspect: {resolveGetStringResult()}";
                var mcpPromptContent = new McpPromptContent("text", evaluatedValue93);
                var mcpPromptMessage = new McpPromptMessage("user", mcpPromptContent);
                return mcpPromptMessage;
                string? resolveGetStringResult()
                {
                    var getPropertyResult91 = arguments.GetProperty("request");
                    var getStringResult92 = getPropertyResult91.GetString();
                    return getStringResult92;
                }
            }
        }

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<McpToolDescriptor> evaluatedValue98 = [resolveevaluatedValue96()];
            var fromResultResult99 = Task.FromResult<IReadOnlyList<McpToolDescriptor>>(evaluatedValue98);
            return fromResultResult99;
            McpToolDescriptor resolveevaluatedValue96()
            {
                var serializeToElementResult = JsonSerializer.SerializeToElement(new { type = "object" });
                McpToolDescriptor evaluatedValue97 = new("example", "Example", serializeToElementResult);
                return evaluatedValue97;
            }
        }

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            McpResourceReadResult evaluatedValue100 = uri switch
            {
                "sereinflow://ai/guide" => new McpResourceReadResult(uri, "# SereinFlow MCP AI Guide\n", "text/markdown"),
                "sereinflow://ai/skills/sereinlang" => new McpResourceReadResult(uri, "# SereinLang MCP Skill\n", "text/markdown"),
                _ => new McpResourceReadResult(uri, new { project = "demo" })};
            var fromResultResult101 = Task.FromResult(evaluatedValue100);
            return fromResultResult101;
        }

        public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            CallCount++;
            var mcpToolCallResult = new McpToolCallResult(new { accepted = true });
            var fromResultResult102 = Task.FromResult(mcpToolCallResult);
            return fromResultResult102;
        }
    }

    private sealed class ContextBackend(IMcpRequestContextAccessor accessor) : ISereinFlowMcpBackend
    {
        public string? RequestId { get; private set; }

        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            var fromResultResult103 = Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([]);
            return fromResultResult103;
        }

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
        {
            var fromResultResult104 = Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([]);
            return fromResultResult104;
        }

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            var fromResultResult105 = Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([]);
            return fromResultResult105;
        }

        public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var fromResultResult106 = Task.FromResult<IReadOnlyList<McpToolDescriptor>>([]);
            return fromResultResult106;
        }

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            RequestId = accessor.Current?.RequestId;
            var mcpToolCallResult107 = new McpToolCallResult(new { accepted = true });
            var fromResultResult108 = Task.FromResult(mcpToolCallResult107);
            return fromResultResult108;
        }
    }

    private sealed class ThrowingBackend : ISereinFlowMcpBackend
    {
        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            var fromResultResult109 = Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([]);
            return fromResultResult109;
        }

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
        {
            var fromResultResult110 = Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([]);
            return fromResultResult110;
        }

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            var fromResultResult111 = Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([]);
            return fromResultResult111;
        }

        public Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var fromResultResult112 = Task.FromResult<IReadOnlyList<McpToolDescriptor>>([]);
            return fromResultResult112;
        }

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("secret backend failure");
        }

        public Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("secret backend failure");
        }
    }
}
