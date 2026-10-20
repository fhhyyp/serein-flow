using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.AspNetCore;

namespace SereinFlow.Api.IntegrationTests;
public sealed class ExternalHostIntegrationTests
{
    [Fact]
    public void DuplicateRegistrationAndEmptySurfaceAreRejected()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSereinFlowResult = services.AddSereinFlow(_ =>
            {
            });
            return addSereinFlowResult;
        });
        services.AddSereinFlow(options =>
        {
            options.UseWebApi();
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var addSereinFlowResult1 = services.AddSereinFlow(options =>
            {
                options.UseMcp();
            });
            return addSereinFlowResult1;
        });
    }

    [Fact]
    public async Task RegistrationDefersStorageAndRunnerValidationUntilStartup()
    {
        using var contentRoot = new TemporaryDirectory();
        var dataRoot = Path.Combine(contentRoot.Path, "data");
        var webApplicationOptions = new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot.Path,
        };
        var builder = WebApplication.CreateBuilder(webApplicationOptions);
        builder.WebHost.UseTestServer();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = dataRoot,
            ["SereinFlow:WorkerRunnerPath"] = Path.Combine(contentRoot.Path, "missing-runner.dll"),
        };
        builder.Configuration.AddInMemoryCollection(dictionary);
        builder.Services.AddSereinFlow(options =>
        {
            options.UseWebApi();
        });
        var existsResult = Directory.Exists(dataRoot);
        Assert.False(existsResult);
        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        var existsResult2 = Directory.Exists(dataRoot);
        Assert.False(existsResult2);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            var startAsyncResult = app.StartAsync();
            return startAsyncResult;
        });
        Assert.Contains("WorkerRunnerPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConflictingHostRouteFailsBeforeListening()
    {
        using var contentRoot = new TemporaryDirectory();
        var webApplicationOptions4 = new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot.Path,
        };
        var builder = WebApplication.CreateBuilder(webApplicationOptions4);
        builder.WebHost.UseTestServer();
        var dictionary5 = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = contentRoot.Path,
            ["SereinFlow:WorkerRunnerPath"] = FindRunnerPath(),
        };
        builder.Configuration.AddInMemoryCollection(dictionary5);
        builder.Services.AddSereinFlow(options =>
        {
            options.UseWebApi();
        });
        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        app.MapGet("/api/projects", () => "host conflict");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            var startAsyncResult3 = app.StartAsync();
            return startAsyncResult3;
        });
        Assert.Contains("SereinFlow route conflict", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StricterHostUploadLimitFailsBeforeListening()
    {
        using var contentRoot = new TemporaryDirectory();
        var webApplicationOptions7 = new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot.Path,
        };
        var builder = WebApplication.CreateBuilder(webApplicationOptions7);
        builder.WebHost.UseTestServer();
        var dictionary8 = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = contentRoot.Path,
            ["SereinFlow:WorkerRunnerPath"] = FindRunnerPath(),
        };
        builder.Configuration.AddInMemoryCollection(dictionary8);
        builder.Services.AddSereinFlow(options =>
        {
            options.UseWebApi();
        });
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1_000_000);
        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            var startAsyncResult6 = app.StartAsync();
            return startAsyncResult6;
        });
        Assert.Contains("MaxRequestBodySize", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CleanHostMapsOnlySelectedSurfaces(bool webApi, bool mcp)
    {
        using var dataRoot = new TemporaryDirectory();
        var webApplicationOptions12 = new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = dataRoot.Path,
        };
        var builder = WebApplication.CreateBuilder(webApplicationOptions12);
        builder.WebHost.UseTestServer();
        if (webApi)
        {
            var addControllersResult = builder.Services.AddControllers();
            addControllersResult.AddApplicationPart(typeof(HostProbeController).Assembly);
        }

        var dictionary13 = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = dataRoot.Path,
            ["SereinFlow:WorkerRunnerPath"] = FindRunnerPath(),
            ["SereinFlow:Mcp:BootstrapAdminKey"] = "external-host-test-key",
        };
        builder.Configuration.AddInMemoryCollection(dictionary13);
        builder.Services.AddSereinFlow(options =>
        {
            if (webApi)
            {
                options.UseWebApi(api =>
                {
                    api.EnableSwagger = true;
                    api.AllowedOrigins = ["https://trusted.example"];
                });
            }

            if (mcp)
            {
                options.UseMcp();
            }
        });
        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        Assert.Throws<InvalidOperationException>(() =>
        {
            var useSereinFlowResult = app.UseSereinFlow();
            return useSereinFlowResult;
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var mapSereinFlowResult = app.MapSereinFlow();
            return mapSereinFlowResult;
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        var evaluatedValue14 = (await client.GetAsync("/healthz")).StatusCode;
        Assert.Equal(webApi ? HttpStatusCode.OK : HttpStatusCode.NotFound, evaluatedValue14);
        var evaluatedValue15 = (await client.GetAsync("/api/projects")).StatusCode;
        Assert.Equal(webApi ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound, evaluatedValue15);
        if (webApi)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
            var evaluatedValue = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.OK, evaluatedValue);
            using var crossOrigin = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
            crossOrigin.Headers.Add("Origin", "https://untrusted.example");
            using var crossOriginResponse = await client.SendAsync(crossOrigin);
            Assert.Equal(HttpStatusCode.OK, crossOriginResponse.StatusCode);
            var containsResult = crossOriginResponse.Headers.Contains("Access-Control-Allow-Origin");
            Assert.False(containsResult);
            using var trustedOrigin = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
            trustedOrigin.Headers.Add("Origin", "https://trusted.example");
            using var trustedResponse = await client.SendAsync(trustedOrigin);
            Assert.Equal(HttpStatusCode.OK, trustedResponse.StatusCode);
            var getValuesResult = trustedResponse.Headers.GetValues("Access-Control-Allow-Origin");
            var singleResult = Assert.Single(getValuesResult);
            Assert.Equal("https://trusted.example", singleResult);
            client.DefaultRequestHeaders.Authorization = null;
            var evaluatedValue9 = (await client.GetAsync("/host-probe")).StatusCode;
            Assert.Equal(HttpStatusCode.OK, evaluatedValue9);
            var openApi = await client.GetStringAsync("/openapi/v1.json");
            Assert.Contains("/api/projects", openApi, StringComparison.Ordinal);
            Assert.Contains("/host-probe", openApi, StringComparison.Ordinal);
            Assert.DoesNotContain("\"/mcp\"", openApi, StringComparison.Ordinal);
            Assert.DoesNotContain("/hubs/runs", openApi, StringComparison.Ordinal);
        }

        var evaluatedValue16 = (await client.GetAsync("/mcp")).StatusCode;
        Assert.Equal(mcp ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound, evaluatedValue16);
        foreach (var hub in new[]
        {
            "/hubs/runs/negotiate?negotiateVersion=1",
            "/hubs/workspace/negotiate?negotiateVersion=1"
        }

        )
        {
            var evaluatedValue10 = (await client.PostAsync(hub, content: null)).StatusCode;
            Assert.Equal(webApi ? HttpStatusCode.OK : HttpStatusCode.NotFound, evaluatedValue10);
        }

        if (mcp)
        {
            using var initialize = CreateMcpRequest("initialize", 1);
            initialize.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
            using var initialized = await client.SendAsync(initialize);
            Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
            var getValuesResult11 = initialized.Headers.GetValues("Mcp-Session-Id");
            var session = Assert.Single(getValuesResult11);
            foreach (var method in new[]
            {
                "tools/list",
                "resources/list"
            }

            )
            {
                using var listing = CreateMcpRequest(method, 2);
                listing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
                listing.Headers.Add("Mcp-Session-Id", session);
                using var listed = await client.SendAsync(listing);
                Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            }

            using var readGuide = CreateMcpRequest("resources/read", 3, new { uri = "sereinflow://ai/guide" });
            readGuide.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
            readGuide.Headers.Add("Mcp-Session-Id", session);
            using var read = await client.SendAsync(readGuide);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var readAsStringAsyncResult = await read.Content.ReadAsStringAsync();
            Assert.Contains("sereinflow", readAsStringAsyncResult, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static HttpRequestMessage CreateMcpRequest(string method, int id, object? parameters = null)
    {
        HttpRequestMessage evaluatedValue1 = new(HttpMethod.Post, "/mcp")
        {
            Content = resolveStringContent()
        };
        return evaluatedValue1;
        HttpContent resolveStringContent()
        {
            var serializeResult = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters ?? new { } });
            var stringContent = new StringContent(serializeResult, Encoding.UTF8, "application/json");
            return stringContent;
        }
    }

    private static string FindRunnerPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "SereinFlow.Worker.Runner", "bin", "Debug", "net10.0", "SereinFlow.Worker.Runner.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Build the Worker Runner before running external host tests.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = createPathInitialValue();

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            // SQLite may still release file handles after host disposal on Windows.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string createPathInitialValue()
        {
            var getTempPathResult = System.IO.Path.GetTempPath();
            var evaluatedValue2 = $"sereinflow-external-host-{Guid.NewGuid():N}";
            string pathInitialValue = System.IO.Path.Combine(getTempPathResult, evaluatedValue2);
            return pathInitialValue;
        }
    }
}

[ApiController]
[Route("host-probe")]
public sealed class HostProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var okResult = Ok(new { owner = "host" });
        return okResult;
    }
}
