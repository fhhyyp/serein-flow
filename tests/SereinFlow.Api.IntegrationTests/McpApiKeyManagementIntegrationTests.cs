using SereinFlow.Security.ApiKeys;
using System.Net.Http;
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
    private static readonly SereinFlowPermission[] ProjectReadPermission = [SereinFlowPermission.ProjectRead];
    private readonly ApiFactory _factory;
    public McpApiKeyManagementIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WebConsoleCanInitializeAndManageApiKeys()
    {
        using var client = _factory.CreateClient();
        var setup = await client.PostAsync("/api/security/api-keys/initial-admin", content: null);
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        var readAsStringAsyncResult = await setup.Content.ReadAsStringAsync();
        using var setupDocument = JsonDocument.Parse(readAsStringAsyncResult);
        var getPropertyResult1 = setupDocument.RootElement.GetProperty("secret");
        var administratorSecret = getPropertyResult1.GetString();
        var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(administratorSecret);
        Assert.False(isNullOrWhiteSpaceResult);
        var repeatedSetup = await client.PostAsync("/api/security/api-keys/initial-admin", content: null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedSetup.StatusCode);
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/security/api-keys");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administratorSecret);
        var list = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var readAsStringAsyncResult2 = await list.Content.ReadAsStringAsync();
        using (var listDocument = JsonDocument.Parse(readAsStringAsyncResult2))
        {
            var enumerateArrayResult = listDocument.RootElement.EnumerateArray();
            Assert.Single(enumerateArrayResult);
            var getPropertyResult = listDocument.RootElement[0].GetProperty("isAdministrator");
            var getBooleanResult = getPropertyResult.GetBoolean();
            Assert.True(getBooleanResult);
            var tryGetPropertyResult = listDocument.RootElement[0].TryGetProperty("secret", out _);
            Assert.False(tryGetPropertyResult);
        }

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/security/api-keys")
        {
            Content = resolveStringContent()
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administratorSecret);
        var created = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var readAsStringAsyncResult3 = await created.Content.ReadAsStringAsync();
        using var createdDocument = JsonDocument.Parse(readAsStringAsyncResult3);
        var getPropertyResult4 = createdDocument.RootElement.GetProperty("secret");
        var getStringResult = getPropertyResult4.GetString();
        var isNullOrWhiteSpaceResult5 = string.IsNullOrWhiteSpace(getStringResult);
        Assert.False(isNullOrWhiteSpaceResult5);
        HttpContent resolveStringContent()
        {
            var createMcpApiKeyRequestDto = new CreateApiKeyRequest(null, "integration-client", ProjectReadPermission, null, IsAdministrator: true);
            var serializeResult = JsonSerializer.Serialize(createMcpApiKeyRequestDto);
            var stringContent = new StringContent(serializeResult, Encoding.UTF8, "application/json");
            return stringContent;
        }
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly TemporaryDirectory _storage = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("SereinFlow:Security:Mode", "ApiKey");
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IStartupFilter, LoopbackTestStartupFilter>(services);
            });
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.UseSetting("SereinFlow:DataRoot", _storage.Path);
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                var dictionary = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.Path,
                    ["SereinFlow:Security:Mode"] = "ApiKey",
                    ["SereinFlow:ApiDocumentation:Enabled"] = "true",
                };
                configuration.AddInMemoryCollection(dictionary);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _storage.Dispose();
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            var getTempPathResult = System.IO.Path.GetTempPath();
            var evaluatedValue = $"sereinflow-api-key-integration-{Guid.NewGuid():N}";
            Path = System.IO.Path.Combine(getTempPathResult, evaluatedValue);
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

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
