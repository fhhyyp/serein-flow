using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework.Tests;

public sealed class McpPropertyBindingTests
{
    [Fact]
    public async Task GeneratedBinderPreservesDefaultsTrimsTextAndRetainsExplicitJsonNull()
    {
        var response = await CallAsync("""{"name":"  sample  ","payload":null}""");
        Assert.Equal("sample", response.Name);
        Assert.Equal(25, response.Take);
        Assert.Equal(JsonValueKind.Null, response.Payload.ValueKind);
        Assert.Null(response.ProjectId);
    }

    [Theory]
    [InlineData("{\"name\":\"sample\"}", "payload.required")]
    [InlineData("{\"name\":\"sample\",\"payload\":{},\"take\":null}", null)]
    [InlineData("{\"name\":\" \",\"payload\":{}}", null)]
    [InlineData("{\"name\":\"sample\",\"payload\":{},\"projectId\":null}", null)]
    public async Task GeneratedBinderRejectsMissingPayloadAndInvalidScalarInputs(string json, string? expectedCode)
    {
        var exception = await Assert.ThrowsAsync<McpParameterBindingException>(() => CallAsync(json));
        Assert.Equal(expectedCode, exception.ErrorCode);
    }

    [Fact]
    public async Task HostBinderCanNormalizeAnExistingWireContract()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMcpRequestBinder<NormalizedRequest>, AliasBinder>();
        using var provider = services.BuildServiceProvider();
        var catalog = McpToolDiscovery.CreateCatalog(new Generated.McpToolRegistry(), toolTypes: [typeof(NormalizedTools)]);
        Assert.True(catalog.TryGet("normalized", out var tool));
        var result = await tool!.ExecuteAsync(new McpToolCallContext(provider), JsonSerializer.SerializeToElement(new { legacyName = "legacy" }));
        Assert.Equal("legacy", result);
    }

    private static async Task<Parameters> CallAsync(string json)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var catalog = McpToolDiscovery.CreateCatalog(new Generated.McpToolRegistry(), toolTypes: [typeof(Tools)]);
        Assert.Contains(catalog.Descriptors.Single().InputSchema.GetProperty("required").EnumerateArray(), item => item.GetString() == "payload");
        Assert.True(catalog.TryGet("parameters", out var tool));
        using var document = JsonDocument.Parse(json);
        return Assert.IsType<Parameters>(await tool!.ExecuteAsync(new McpToolCallContext(provider), document.RootElement));
    }

    [McpBindProperties]
    public sealed class Parameters
    {
        [McpParameter(Required = true, Trim = true)]
        public string Name { get; set; } = string.Empty;
        public int Take { get; set; } = 25;
        public Guid? ProjectId { get; set; }
        [McpParameter(Required = true, AllowNull = true, ErrorCode = "payload.required")]
        public JsonElement Payload { get; set; }
    }

    public static class Tools
    {
        [McpTool("parameters", Description = "Bind typed parameters.")]
        public static Task<Parameters> Read(Parameters request) => Task.FromResult(request);
    }

    public sealed record NormalizedRequest(string Name);
    public sealed class AliasBinder : IMcpRequestBinder<NormalizedRequest>
    {
        public NormalizedRequest Bind(JsonElement arguments, JsonSerializerOptions options)
            => new(arguments.GetProperty("legacyName").GetString()!);
    }
    public static class NormalizedTools
    {
        [McpTool("normalized", Description = "Read normalized wire parameters.")]
        public static Task<string> Read(NormalizedRequest request) => Task.FromResult(request.Name);
    }
}
