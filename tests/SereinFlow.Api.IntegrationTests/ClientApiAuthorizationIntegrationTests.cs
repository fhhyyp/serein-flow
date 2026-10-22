using SereinFlow.Security.ApiKeys;
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
    {
        _factory = factory;
    }

    [Fact]
    public async Task RunReadRequiresBearerKeyWhenRestAuthenticationIsEnabled()
    {
        using var client = _factory.CreateClient();
        var runId = Guid.NewGuid();
        var unauthorized = await client.GetAsync($"/api/runs/{runId:D}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        var selectResult = unauthorized.Headers.WwwAuthenticate.Select(static value => value.Scheme);
        Assert.Contains("Bearer", selectResult);
    }

    [Fact]
    public async Task SdkBearerKeyReachesTheProtectedRunEndpoint()
    {
        using var client = _factory.CreateClient();
        var sereinFlowClientOptions = new SereinFlowClientOptions
        {
            BaseAddress = new Uri("http://localhost/"),
            BearerTokenProvider = _ =>
            {
                var value1 = ValueTask.FromResult<string?>(ApiFactory.BootstrapKey);
                return value1;
            },
        };
        using var sdk = new SereinFlowClient(client, sereinFlowClientOptions);
        var exception = await Assert.ThrowsAsync<SereinFlowClientException>(() =>
        {
            var newGuidResult = Guid.NewGuid();
            var getAsyncResult = sdk.Runs.GetAsync(newGuidResult);
            return getAsyncResult;
        });
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
                var dictionary = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.Path,
                    ["SereinFlow:Security:ApiKeys:BootstrapAdminKey"] = BootstrapKey,
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
            var evaluatedValue = $"sereinflow-client-auth-integration-{Guid.NewGuid():N}";
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
