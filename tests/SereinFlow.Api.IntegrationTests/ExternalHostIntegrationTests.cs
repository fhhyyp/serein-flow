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
        Assert.Throws<InvalidOperationException>(() => services.AddSereinFlow(_ => { }));
        services.AddSereinFlow(options => options.UseWebApi());
        Assert.Throws<InvalidOperationException>(() => services.AddSereinFlow(options => options.UseMcp()));
    }

    [Fact]
    public async Task RegistrationDefersStorageAndRunnerValidationUntilStartup()
    {
        using var contentRoot = new TemporaryDirectory();
        var dataRoot = Path.Combine(contentRoot.Path, "data");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot.Path,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = dataRoot,
            ["SereinFlow:WorkerRunnerPath"] = Path.Combine(contentRoot.Path, "missing-runner.dll"),
        });
        builder.Services.AddSereinFlow(options => options.UseWebApi());
        Assert.False(Directory.Exists(dataRoot));

        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        Assert.False(Directory.Exists(dataRoot));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("WorkerRunnerPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConflictingHostRouteFailsBeforeListening()
    {
        using var contentRoot = new TemporaryDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot.Path,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = contentRoot.Path,
            ["SereinFlow:WorkerRunnerPath"] = FindRunnerPath(),
        });
        builder.Services.AddSereinFlow(options => options.UseWebApi());

        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        app.MapGet("/api/projects", () => "host conflict");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("SereinFlow route conflict", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StricterHostUploadLimitFailsBeforeListening()
    {
        using var contentRoot = new TemporaryDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot.Path,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = contentRoot.Path,
            ["SereinFlow:WorkerRunnerPath"] = FindRunnerPath(),
        });
        builder.Services.AddSereinFlow(options => options.UseWebApi());
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1_000_000);

        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("MaxRequestBodySize", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CleanHostMapsOnlySelectedSurfaces(bool webApi, bool mcp)
    {
        using var dataRoot = new TemporaryDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = dataRoot.Path,
        });
        builder.WebHost.UseTestServer();
        if (webApi)
            builder.Services.AddControllers().AddApplicationPart(typeof(HostProbeController).Assembly);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = dataRoot.Path,
            ["SereinFlow:WorkerRunnerPath"] = FindRunnerPath(),
            ["SereinFlow:Mcp:BootstrapAdminKey"] = "external-host-test-key",
        });
        builder.Services.AddSereinFlow(options =>
        {
            if (webApi)
                options.UseWebApi(api =>
                {
                    api.EnableSwagger = true;
                    api.AllowedOrigins = ["https://trusted.example"];
                });
            if (mcp)
                options.UseMcp();
        });

        await using var app = builder.Build();
        app.UseSereinFlow();
        app.MapSereinFlow();
        Assert.Throws<InvalidOperationException>(() => app.UseSereinFlow());
        Assert.Throws<InvalidOperationException>(() => app.MapSereinFlow());
        await app.StartAsync();
        using var client = app.GetTestClient();

        Assert.Equal(webApi ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            (await client.GetAsync("/healthz")).StatusCode);
        Assert.Equal(webApi ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound,
            (await client.GetAsync("/api/projects")).StatusCode);
        if (webApi)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/projects")).StatusCode);
            using var crossOrigin = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
            crossOrigin.Headers.Add("Origin", "https://untrusted.example");
            using var crossOriginResponse = await client.SendAsync(crossOrigin);
            Assert.Equal(HttpStatusCode.OK, crossOriginResponse.StatusCode);
            Assert.False(crossOriginResponse.Headers.Contains("Access-Control-Allow-Origin"));
            using var trustedOrigin = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
            trustedOrigin.Headers.Add("Origin", "https://trusted.example");
            using var trustedResponse = await client.SendAsync(trustedOrigin);
            Assert.Equal(HttpStatusCode.OK, trustedResponse.StatusCode);
            Assert.Equal("https://trusted.example",
                Assert.Single(trustedResponse.Headers.GetValues("Access-Control-Allow-Origin")));
            client.DefaultRequestHeaders.Authorization = null;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/host-probe")).StatusCode);
            var openApi = await client.GetStringAsync("/openapi/v1.json");
            Assert.Contains("/api/projects", openApi, StringComparison.Ordinal);
            Assert.Contains("/host-probe", openApi, StringComparison.Ordinal);
            Assert.DoesNotContain("\"/mcp\"", openApi, StringComparison.Ordinal);
            Assert.DoesNotContain("/hubs/runs", openApi, StringComparison.Ordinal);
        }
        Assert.Equal(mcp ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound,
            (await client.GetAsync("/mcp")).StatusCode);
        foreach (var hub in new[] { "/hubs/runs/negotiate?negotiateVersion=1", "/hubs/workspace/negotiate?negotiateVersion=1" })
            Assert.Equal(webApi ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                (await client.PostAsync(hub, content: null)).StatusCode);

        if (mcp)
        {
            using var initialize = CreateMcpRequest("initialize", 1);
            initialize.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
            using var initialized = await client.SendAsync(initialize);
            Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
            var session = Assert.Single(initialized.Headers.GetValues("Mcp-Session-Id"));

            foreach (var method in new[] { "tools/list", "resources/list" })
            {
                using var listing = CreateMcpRequest(method, 2);
                listing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
                listing.Headers.Add("Mcp-Session-Id", session);
                using var listed = await client.SendAsync(listing);
                Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            }

            using var readGuide = CreateMcpRequest("resources/read", 3,
                new { uri = "sereinflow://ai/guide" });
            readGuide.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-host-test-key");
            readGuide.Headers.Add("Mcp-Session-Id", session);
            using var read = await client.SendAsync(readGuide);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Contains("sereinflow", await read.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static HttpRequestMessage CreateMcpRequest(string method, int id, object? parameters = null)
        => new(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters ?? new { } }),
                Encoding.UTF8,
                "application/json"),
        };

    private static string FindRunnerPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "SereinFlow.Worker.Runner", "bin",
                "Debug", "net10.0", "SereinFlow.Worker.Runner.dll");
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Build the Worker Runner before running external host tests.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"sereinflow-external-host-{Guid.NewGuid():N}");

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // SQLite may still release file handles after host disposal on Windows.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

[ApiController]
[Route("host-probe")]
public sealed class HostProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { owner = "host" });
}
