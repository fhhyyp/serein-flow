using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Api.IntegrationTests;

public sealed class AnonymousAccessIntegrationTests
{
    [Fact]
    public async Task DefaultHostAllowsRestReadsAndWritesWithoutCredentials()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await projects.AddAsync(Project.Create("first"));
        await projects.AddAsync(Project.Create("second"));

        using var capabilities = await client.GetAsync("/api/security/capabilities");
        Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
        using var configuration = JsonDocument.Parse(await capabilities.Content.ReadAsStringAsync());
        Assert.Equal("None", configuration.RootElement.GetProperty("mode").GetString());
        Assert.False(configuration.RootElement.GetProperty("apiKeyManagement").GetBoolean());
        Assert.False(configuration.RootElement.GetProperty("initialSetupRequired").GetBoolean());
        var listed = await client.GetFromJsonAsync<ProjectWorkspaceDto[]>("/api/projects");
        Assert.Equal(2, listed!.Length);
        using var settings = await client.GetAsync("/api/environment/settings");
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        var body = await settings.Content.ReadFromJsonAsync<JsonElement>();
        using var saved = await client.PutAsJsonAsync("/api/environment/settings", body);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.False(saved.Headers.Contains("Set-Cookie"));
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.Null(scope.ServiceProvider.GetService<SereinFlow.Security.ApiKeys.ApiKeyService>());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/security/api-keys")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/security/api-keys/initial-admin", null)).StatusCode);
    }

    [Fact]
    public async Task DefaultHostAllowsMcpInitializationAndDiscoveryWithoutCredentials()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        using var initialized = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" });
        Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
        var session = Assert.Single(initialized.Headers.GetValues("Mcp-Session-Id"));
        client.DefaultRequestHeaders.Add("Mcp-Session-Id", session);
        foreach (var method in new[] { "tools/list", "resources/list", "prompts/list" })
        {
            using var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 2, method });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(document.RootElement.TryGetProperty("result", out _));
            Assert.False(document.RootElement.TryGetProperty("error", out _));
        }
        using var guide = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = 3, method = "resources/read", @params = new { uri = "sereinflow://ai/guide" }
        });
        Assert.Equal(HttpStatusCode.OK, guide.StatusCode);
        using var guideDocument = JsonDocument.Parse(await guide.Content.ReadAsStringAsync());
        Assert.True(guideDocument.RootElement.TryGetProperty("result", out _));
        using var projects = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = 4, method = "tools/call",
            @params = new { name = "sereinflow_list_projects", arguments = new { } }
        });
        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);
        using var projectDocument = JsonDocument.Parse(await projects.Content.ReadAsStringAsync());
        Assert.True(projectDocument.RootElement.TryGetProperty("result", out var result));
        Assert.False(projectDocument.RootElement.TryGetProperty("error", out _));
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/environment/settings/mcp-skills")).StatusCode);
    }

    [Theory]
    [InlineData("runs")]
    [InlineData("workspace")]
    public async Task DefaultHostAllowsHubNegotiationWithoutCredentials(string hub)
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/hubs/{hub}/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "sereinflow-anonymous-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _root,
                    ["Logging:EventLog:LogLevel:Default"] = "None"
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_root))
            {
                try
                {
                    Directory.Delete(_root, recursive: true);
                }
                catch (IOException)
                {
                    // SQLite connection pooling can retain the temporary file briefly.
                }
            }
        }
    }
}
