using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SereinFlow.Api;
using SereinFlow.Contracts;

namespace SereinFlow.Api.IntegrationTests;

public sealed class McpApiKeyManagementIntegrationTests : IClassFixture<McpApiKeyManagementIntegrationTests.ApiFactory>
{
    private static readonly McpPermissionDto[] ProjectReadPermission = [McpPermissionDto.ProjectRead];
    private readonly ApiFactory _factory;

    public McpApiKeyManagementIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WebConsoleCanInitializeAndManageApiKeys()
    {
        using var client = _factory.CreateClient();

        var setup = await client.PostAsync("/api/environment/settings/mcp-keys/setup", content: null);
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        using var setupDocument = JsonDocument.Parse(await setup.Content.ReadAsStringAsync());
        var administratorSecret = setupDocument.RootElement.GetProperty("secret").GetString();
        Assert.False(string.IsNullOrWhiteSpace(administratorSecret));

        var repeatedSetup = await client.PostAsync("/api/environment/settings/mcp-keys/setup", content: null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedSetup.StatusCode);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/environment/settings/mcp-keys");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administratorSecret);
        var list = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            Assert.Single(listDocument.RootElement.EnumerateArray());
            Assert.True(listDocument.RootElement[0].GetProperty("isAdministrator").GetBoolean());
            Assert.False(listDocument.RootElement[0].TryGetProperty("secret", out _));
        }

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/environment/settings/mcp-keys")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new CreateMcpApiKeyRequestDto(
                    null,
                    "integration-client",
                    ProjectReadPermission,
                    null,
                    IsAdministrator: true)),
                Encoding.UTF8,
                "application/json"),
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administratorSecret);
        var created = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(createdDocument.RootElement.GetProperty("secret").GetString()));
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly TemporaryDirectory _storage = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.UseSetting("SereinFlow:DataRoot", _storage.Path);
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.Path,
                    ["SereinFlow:ApiDocumentation:Enabled"] = "true",
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _storage.Dispose();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sereinflow-api-key-integration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
                return;

            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
