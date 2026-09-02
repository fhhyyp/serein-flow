using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SereinFlow.Api;

namespace SereinFlow.Api.IntegrationTests;

public sealed class McpHostIntegrationTests : IClassFixture<McpHostIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public McpHostIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WebHostServesApiAndAuthenticatedMcpWithTheDedicatedCorsPolicy()
    {
        using var client = _factory.CreateClient();

        var health = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var unauthenticatedRequest = CreateMcpRequest("initialize", 1);
        var unauthenticated = await client.SendAsync(unauthenticatedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Contains("Bearer", unauthenticated.Headers.WwwAuthenticate.Select(static value => value.Scheme));
        using (var unauthenticatedDocument = JsonDocument.Parse(await unauthenticated.Content.ReadAsStringAsync()))
        {
            Assert.Equal("mcp.unauthenticated", unauthenticatedDocument.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        using var initializeRequest = CreateMcpRequest("initialize", 2);
        initializeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        var initialize = await client.SendAsync(initializeRequest);
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        Assert.True(initialize.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues));
        var sessionId = Assert.Single(sessionValues);
        using (var initializeDocument = JsonDocument.Parse(await initialize.Content.ReadAsStringAsync()))
        {
            Assert.Equal("sereinflow", initializeDocument.RootElement
                .GetProperty("result")
                .GetProperty("serverInfo")
                .GetProperty("name")
                .GetString());
        }

        using var toolsRequest = CreateMcpRequest("tools/list", 3);
        toolsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        toolsRequest.Headers.Add("Mcp-Session-Id", sessionId);
        var tools = await client.SendAsync(toolsRequest);
        Assert.Equal(HttpStatusCode.OK, tools.StatusCode);
        using (var toolsDocument = JsonDocument.Parse(await tools.Content.ReadAsStringAsync()))
        {
            Assert.Contains(
                toolsDocument.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray(),
                tool => tool.GetProperty("name").GetString() == "sereinflow_create_library_node_template");
        }

        using var rejectedOriginRequest = CreateMcpRequest("initialize", 4);
        rejectedOriginRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        rejectedOriginRequest.Headers.Add("Origin", "https://untrusted.example");
        var rejectedOrigin = await client.SendAsync(rejectedOriginRequest);
        Assert.Equal(HttpStatusCode.OK, rejectedOrigin.StatusCode);
        Assert.False(rejectedOrigin.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task WebHostExposesControllerOpenApiWithoutProtocolTransports()
    {
        using var client = _factory.CreateClient();

        var swagger = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("swagger-ui", await swagger.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        using var document = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/projects", out _));
        Assert.True(paths.TryGetProperty("/api/projects/{projectId}/flows/{flowId}", out _));
        Assert.True(paths.TryGetProperty("/api/runs/{runId}", out _));
        Assert.True(paths.TryGetProperty("/api/runs/{runId}/messages/{topic}", out _));
        Assert.False(paths.TryGetProperty("/mcp", out _));
        Assert.False(paths.TryGetProperty("/hubs/runs", out _));
        Assert.False(paths.EnumerateObject().Any(path => path.Name.EndsWith("/events/stream", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task StdioModeRequiresAnExplicitKeyAndOnlyWritesJsonRpcToStandardOutput()
    {
        using var reservedEndpoint = new TcpListener(IPAddress.Loopback, 0);
        reservedEndpoint.Start();
        var endpoint = (IPEndPoint)reservedEndpoint.LocalEndpoint;
        using var directory = new TemporaryDirectory();
        const string apiKey = "stdio-integration-key";

        using var process = StartStdioProcess(directory.Path, apiKey, endpoint.Port);
        var standardError = process.StandardError.ReadToEndAsync();

        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { },
        }));
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { },
        }));
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "resources/list",
            @params = new { },
        }));

        var initializeLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        var toolsLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        var resourcesLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(string.IsNullOrWhiteSpace(initializeLine));
        Assert.False(string.IsNullOrWhiteSpace(toolsLine));
        Assert.False(string.IsNullOrWhiteSpace(resourcesLine));
        using (var initialize = JsonDocument.Parse(initializeLine))
        {
            Assert.Equal("2.0", initialize.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal("sereinflow", initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        }
        using (var tools = JsonDocument.Parse(toolsLine))
        {
            Assert.Contains(
                tools.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray(),
                tool => tool.GetProperty("name").GetString() == "sereinflow_list_projects");
            Assert.Contains(
                tools.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray(),
                tool => tool.GetProperty("name").GetString() == "sereinflow_start_debug_session");
            Assert.Contains(
                tools.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray(),
                tool => tool.GetProperty("name").GetString() == "sereinflow_step_debug");
            Assert.Contains(
                tools.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray(),
                tool => tool.GetProperty("name").GetString() == "sereinflow_stop_debug");
        }
        using (var resources = JsonDocument.Parse(resourcesLine))
        {
            Assert.Contains(
                resources.RootElement.GetProperty("result").GetProperty("resources").EnumerateArray(),
                resource => resource.GetProperty("uri").GetString() == "sereinflow://runs");
            Assert.Contains(
                resources.RootElement.GetProperty("result").GetProperty("resources").EnumerateArray(),
                resource => resource.GetProperty("uri").GetString() == "sereinflow://debug-sessions");
        }

        process.StandardInput.Close();
        using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(exitTimeout.Token);
        var diagnostics = await standardError;

        Assert.Equal(0, process.ExitCode);
        Assert.DoesNotContain("Now listening", diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StdioModeFailsBeforeServingWhenTheExplicitKeyIsMissing()
    {
        using var directory = new TemporaryDirectory();
        using var process = StartStdioProcess(directory.Path, null, null);
        var standardError = process.StandardError.ReadToEndAsync();

        using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(exitTimeout.Token);
        var diagnostics = await standardError;

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("SereinFlow:Mcp:Stdio:ApiKey must be configured explicitly", diagnostics, StringComparison.Ordinal);
        Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync());
    }

    private static HttpRequestMessage CreateMcpRequest(string method, int id)
        => new(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = new { } }),
                Encoding.UTF8,
                "application/json"),
        };

    private static Process StartStdioProcess(string dataRoot, string? apiKey, int? reservedPort)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
        };
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("--mcp-stdio");
        startInfo.Environment["SereinFlow__DataRoot"] = dataRoot;
        startInfo.Environment["SereinFlow__Mcp__BootstrapAdminKey"] = "stdio-integration-key";
        if (apiKey is not null)
            startInfo.Environment["SereinFlow__Mcp__Stdio__ApiKey"] = apiKey;
        if (reservedPort is not null)
            startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{reservedPort.Value}";

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The SereinFlow API stdio process could not be started.");
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly TemporaryDirectory _storage = new();

        public const string BootstrapKey = "web-host-integration-key";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.UseSetting("SereinFlow:DataRoot", _storage.Path);
            builder.UseSetting("SereinFlow:Mcp:BootstrapAdminKey", BootstrapKey);
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.Path,
                    ["SereinFlow:Mcp:BootstrapAdminKey"] = BootstrapKey,
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sereinflow-api-integration-{Guid.NewGuid():N}");
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
