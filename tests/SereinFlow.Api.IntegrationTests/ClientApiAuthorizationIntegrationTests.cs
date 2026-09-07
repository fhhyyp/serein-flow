using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SereinFlow.Api;
using SereinFlow.Client;

namespace SereinFlow.Api.IntegrationTests;

public sealed class ClientApiAuthorizationIntegrationTests : IClassFixture<ClientApiAuthorizationIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClientApiAuthorizationIntegrationTests(ApiFactory factory)
        => _factory = factory;

    [Fact]
    public async Task RunReadRequiresBearerKeyWhenRestAuthenticationIsEnabled()
    {
        using var client = _factory.CreateClient();
        var runId = Guid.NewGuid();

        var unauthorized = await client.GetAsync($"/api/runs/{runId:D}");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Contains("Bearer", unauthorized.Headers.WwwAuthenticate.Select(static value => value.Scheme));
    }

    [Fact]
    public async Task SdkBearerKeyReachesTheProtectedRunEndpoint()
    {
        using var client = _factory.CreateClient();
        using var sdk = new SereinFlowClient(
            client,
            new SereinFlowClientOptions
            {
                BaseAddress = new Uri("http://localhost/"),
                ApiKey = ApiFactory.BootstrapKey,
            });

        var exception = await Assert.ThrowsAsync<SereinFlowClientException>(() =>
            sdk.Runs.GetAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)exception.StatusCode);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string BootstrapKey = "sdk-integration-bootstrap-key";
        private readonly TemporaryDirectory _storage = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.Path,
                    ["SereinFlow:Mcp:BootstrapAdminKey"] = BootstrapKey,
                    ["SereinFlow:Api:RequireAuthentication"] = "true",
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
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sereinflow-client-auth-integration-{Guid.NewGuid():N}");
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
