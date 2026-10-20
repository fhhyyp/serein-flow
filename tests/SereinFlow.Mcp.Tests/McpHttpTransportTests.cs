using SereinFlow.Mcp;
using Microsoft.Extensions.Configuration;

namespace SereinFlow.Mcp.Tests;
public sealed class McpHttpTransportTests
{
    [Fact]
    public void ParsesAndRetainsTheConfiguredMcpCorsOrigins()
    {
        var configurationBuilder = new ConfigurationBuilder();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:Mcp:Http:AllowedOrigins"] = "https://trusted.example, https://console.example, invalid-origin",
        };
        var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection(dictionary);
        var configuration = addInMemoryCollectionResult.Build();
        var options = SereinFlowMcpOptions.FromConfiguration(configuration);
        Assert.Equal(["https://trusted.example", "https://console.example"], options.AllowedOrigins);
    }

    [Fact]
    public void SessionIsBoundToTheAuthenticatingPrincipal()
    {
        var sessions = new McpHttpSessionRegistry();
        var sessionId = sessions.Create("key-a");
        var validateResult = sessions.Validate(sessionId, "key-a");
        Assert.True(validateResult);
        var validateResult1 = sessions.Validate(sessionId, "key-b");
        Assert.False(validateResult1);
        var removeResult = sessions.Remove(sessionId, "key-b");
        Assert.False(removeResult);
        var removeResult2 = sessions.Remove(sessionId, "key-a");
        Assert.True(removeResult2);
        var validateResult3 = sessions.Validate(sessionId, "key-a");
        Assert.False(validateResult3);
    }

    [Fact]
    public void LimiterEnforcesGlobalConcurrencyAndPerPrincipalRate()
    {
        using var limiter = new McpRequestLimiter(maxConcurrentRequests: 1, maxRequestsPerMinute: 2);
        var tryAcquireResult = limiter.TryAcquire("key-a", out var first);
        Assert.True(tryAcquireResult);
        Assert.NotNull(first);
        var tryAcquireResult4 = limiter.TryAcquire("key-b", out var blockedByConcurrency);
        Assert.False(tryAcquireResult4);
        Assert.Null(blockedByConcurrency);
        first!.Dispose();
        var tryAcquireResult5 = limiter.TryAcquire("key-b", out var second);
        Assert.True(tryAcquireResult5);
        Assert.NotNull(second);
        second!.Dispose();
        var tryAcquireResult6 = limiter.TryAcquire("key-a", out var rateFirst);
        Assert.True(tryAcquireResult6);
        Assert.NotNull(rateFirst);
        rateFirst!.Dispose();
        var tryAcquireResult7 = limiter.TryAcquire("key-a", out var blockedByRate);
        Assert.False(tryAcquireResult7);
        Assert.Null(blockedByRate);
    }

    [Fact]
    public async Task DispatcherReturnsBoundedResponseForOversizedPayload()
    {
        var fakeBackend = new FakeBackend();
        var server = new SereinFlowMcpServer(fakeBackend, maxRequestBytes: 32, maxResponseBytes: 256);
        var stringValue = new string ('x', 33);
        var response = await server.HandleRequestAsync(stringValue);
        Assert.NotNull(response);
        var toStringResult = McpProtocolErrorCodes.RequestTooLarge.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(toStringResult, response, StringComparison.Ordinal);
    }

    private sealed class FakeBackend : ISereinFlowMcpBackend
    {
        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            var fromResultResult = Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([]);
            return fromResultResult;
        }

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
        {
            var fromResultResult8 = Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([]);
            return fromResultResult8;
        }

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            var fromResultResult9 = Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([]);
            return fromResultResult9;
        }

        public Task<McpPromptResult> GetPromptAsync(string name, System.Text.Json.JsonElement arguments, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var fromResultResult10 = Task.FromResult<IReadOnlyList<McpToolDescriptor>>([]);
            return fromResultResult10;
        }

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<McpToolCallResult> CallToolAsync(string name, System.Text.Json.JsonElement arguments, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
