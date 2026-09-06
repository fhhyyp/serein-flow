using SereinFlow.Mcp;
using Microsoft.Extensions.Configuration;

namespace SereinFlow.Mcp.Tests;

public sealed class McpHttpTransportTests
{
    [Fact]
    public void ParsesAndRetainsTheConfiguredMcpCorsOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SereinFlow:Mcp:Http:AllowedOrigins"] = "https://trusted.example, https://console.example, invalid-origin",
            })
            .Build();

        var options = SereinFlowMcpOptions.FromConfiguration(configuration);

        Assert.Equal(
            ["https://trusted.example", "https://console.example"],
            options.AllowedOrigins);
    }

    [Fact]
    public void SessionIsBoundToTheAuthenticatingPrincipal()
    {
        var sessions = new McpHttpSessionRegistry();
        var sessionId = sessions.Create("key-a");

        Assert.True(sessions.Validate(sessionId, "key-a"));
        Assert.False(sessions.Validate(sessionId, "key-b"));
        Assert.False(sessions.Remove(sessionId, "key-b"));
        Assert.True(sessions.Remove(sessionId, "key-a"));
        Assert.False(sessions.Validate(sessionId, "key-a"));
    }

    [Fact]
    public void LimiterEnforcesGlobalConcurrencyAndPerPrincipalRate()
    {
        using var limiter = new McpRequestLimiter(maxConcurrentRequests: 1, maxRequestsPerMinute: 2);

        Assert.True(limiter.TryAcquire("key-a", out var first));
        Assert.NotNull(first);
        Assert.False(limiter.TryAcquire("key-b", out var blockedByConcurrency));
        Assert.Null(blockedByConcurrency);

        first!.Dispose();
        Assert.True(limiter.TryAcquire("key-b", out var second));
        Assert.NotNull(second);
        second!.Dispose();

        Assert.True(limiter.TryAcquire("key-a", out var rateFirst));
        Assert.NotNull(rateFirst);
        rateFirst!.Dispose();
        Assert.False(limiter.TryAcquire("key-a", out var blockedByRate));
        Assert.Null(blockedByRate);
    }

    [Fact]
    public async Task DispatcherReturnsBoundedResponseForOversizedPayload()
    {
        var server = new SereinFlowMcpServer(new FakeBackend(), maxRequestBytes: 32, maxResponseBytes: 256);

        var response = await server.HandleRequestAsync(new string('x', 33));

        Assert.NotNull(response);
        Assert.Contains(McpProtocolErrorCodes.RequestTooLarge.ToString(System.Globalization.CultureInfo.InvariantCulture), response, StringComparison.Ordinal);
    }

    private sealed class FakeBackend : ISereinFlowMcpBackend
    {
        public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceDescriptor>>([]);

        public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpResourceTemplateDescriptor>>([]);

        public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpPromptDescriptor>>([]);

        public Task<McpPromptResult> GetPromptAsync(string name, System.Text.Json.JsonElement arguments, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpToolDescriptor>>([]);

        public Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<McpToolCallResult> CallToolAsync(string name, System.Text.Json.JsonElement arguments, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
