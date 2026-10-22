using SereinFlow.Security.ApiKeys;
using System.Net.Http;
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
using SereinFlow.Contracts;

namespace SereinFlow.Api.IntegrationTests;
public sealed class McpHostIntegrationTests : IClassFixture<McpHostIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;
    public McpHostIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NonObjectMcpRequestsUseTheProtocolErrorResponse()
    {
        using var client = _factory.CreateClient();
        using var initializeRequest = CreateMcpRequest("initialize", 10);
        initializeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        using var initialize = await client.SendAsync(initializeRequest);
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        var argument2 = initialize.Headers.GetValues("Mcp-Session-Id");
        var sessionId = Assert.Single(argument2);
        foreach (var body in new[]
        {
            "[]",
            "null",
            "42",
            "true",
            "\"text\""
        }

        )
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
            request.Headers.Add("Mcp-Session-Id", sessionId);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var argument1 = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(argument1);
            {
                var actual1 = document.RootElement.GetProperty("error").GetProperty("code").GetInt32();
                Assert.Equal(-32600, actual1);
            }

            {
                var actual2 = document.RootElement.GetProperty("id").ValueKind;
                Assert.Equal(JsonValueKind.Null, actual2);
            }
        }
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
        var selectResult = unauthenticated.Headers.WwwAuthenticate.Select(static value => value.Scheme);
        Assert.Contains("Bearer", selectResult);
        var readAsStringAsyncResult = await unauthenticated.Content.ReadAsStringAsync();
        using (var unauthenticatedDocument = JsonDocument.Parse(readAsStringAsyncResult))
        {
            var getPropertyResult = unauthenticatedDocument.RootElement.GetProperty("error");
            var getPropertyResult1 = getPropertyResult.GetProperty("code");
            var getStringResult = getPropertyResult1.GetString();
            Assert.Equal(McpErrorCodes.Unauthenticated, getStringResult);
        }

        using var initializeRequest = CreateMcpRequest("initialize", 2);
        initializeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        var initialize = await client.SendAsync(initializeRequest);
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        var tryGetValuesResult = initialize.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues);
        Assert.True(tryGetValuesResult);
        var sessionId = Assert.Single(sessionValues);
        var readAsStringAsyncResult17 = await initialize.Content.ReadAsStringAsync();
        using (var initializeDocument = JsonDocument.Parse(readAsStringAsyncResult17))
        {
            var getPropertyResult2 = initializeDocument.RootElement.GetProperty("result");
            var getPropertyResult3 = getPropertyResult2.GetProperty("serverInfo");
            var getPropertyResult4 = getPropertyResult3.GetProperty("name");
            var getStringResult5 = getPropertyResult4.GetString();
            Assert.Equal("sereinflow", getStringResult5);
        }

        using var toolsRequest = CreateMcpRequest("tools/list", 3);
        toolsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        toolsRequest.Headers.Add("Mcp-Session-Id", sessionId);
        var tools = await client.SendAsync(toolsRequest);
        Assert.Equal(HttpStatusCode.OK, tools.StatusCode);
        var readAsStringAsyncResult18 = await tools.Content.ReadAsStringAsync();
        using (var toolsDocument = JsonDocument.Parse(readAsStringAsyncResult18))
        {
            var getPropertyResult12 = toolsDocument.RootElement.GetProperty("result");
            var getPropertyResult13 = getPropertyResult12.GetProperty("tools");
            var enumerateArrayResult = getPropertyResult13.EnumerateArray();
            Assert.Contains(enumerateArrayResult, tool =>
            {
                var evaluatedValue = resolveGetStringResult() == "sereinflow_create_library_node_template";
                return evaluatedValue;
                string? resolveGetStringResult()
                {
                    var getPropertyResult6 = tool.GetProperty("name");
                    var getStringResult7 = getPropertyResult6.GetString();
                    return getStringResult7;
                }
            });
            var getPropertyResult14 = toolsDocument.RootElement.GetProperty("result");
            var getPropertyResult15 = getPropertyResult14.GetProperty("tools");
            var enumerateArrayResult16 = getPropertyResult15.EnumerateArray();
            Assert.Contains(enumerateArrayResult16, tool =>
            {
                var evaluatedValue11 = resolveGetStringResult8() == "sereinflow_publish_run_message";
                return evaluatedValue11;
                string? resolveGetStringResult8()
                {
                    var getPropertyResult9 = tool.GetProperty("name");
                    var getStringResult10 = getPropertyResult9.GetString();
                    return getStringResult10;
                }
            });
        }

        using var rejectedOriginRequest = CreateMcpRequest("initialize", 4);
        rejectedOriginRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.BootstrapKey);
        rejectedOriginRequest.Headers.Add("Origin", "https://untrusted.example");
        var rejectedOrigin = await client.SendAsync(rejectedOriginRequest);
        Assert.Equal(HttpStatusCode.OK, rejectedOrigin.StatusCode);
        var containsResult = rejectedOrigin.Headers.Contains("Access-Control-Allow-Origin");
        Assert.False(containsResult);
    }

    [Fact]
    public async Task WebHostExposesControllerOpenApiWithoutProtocolTransports()
    {
        using var client = _factory.CreateClient();
        var swagger = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var readAsStringAsyncResult19 = await swagger.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", readAsStringAsyncResult19, StringComparison.OrdinalIgnoreCase);
        var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        var readAsStringAsyncResult20 = await openApi.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(readAsStringAsyncResult20);
        var paths = document.RootElement.GetProperty("paths");
        var tryGetPropertyResult = paths.TryGetProperty("/api/projects", out _);
        Assert.True(tryGetPropertyResult);
        var tryGetPropertyResult21 = paths.TryGetProperty("/api/projects/{projectId}/flows/{flowId}", out _);
        Assert.True(tryGetPropertyResult21);
        var tryGetPropertyResult22 = paths.TryGetProperty("/api/runs/{runId}", out _);
        Assert.True(tryGetPropertyResult22);
        var tryGetPropertyResult23 = paths.TryGetProperty("/api/runs/{runId}/messages/{topic}", out _);
        Assert.True(tryGetPropertyResult23);
        var tryGetPropertyResult24 = paths.TryGetProperty("/mcp", out _);
        Assert.False(tryGetPropertyResult24);
        var tryGetPropertyResult25 = paths.TryGetProperty("/hubs/runs", out _);
        Assert.False(tryGetPropertyResult25);
        var anyResult = paths.EnumerateObject().Any(path =>
        {
            var endsWithResult = path.Name.EndsWith("/events/stream", StringComparison.Ordinal);
            return endsWithResult;
        });
        Assert.False(anyResult);
    }

    [Fact]
    public async Task StdioModeOnlyWritesJsonRpcToStandardOutput()
    {
        using var reservedEndpoint = new TcpListener(IPAddress.Loopback, 0);
        reservedEndpoint.Start();
        var endpoint = (IPEndPoint)reservedEndpoint.LocalEndpoint;
        using var directory = new TemporaryDirectory();
        const string apiKey = "stdio-integration-key";
        using var process = StartStdioProcess(directory.Path, apiKey, endpoint.Port);
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            var serializeResult = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { }, });
            await process.StandardInput.WriteLineAsync(serializeResult);
            var serializeResult74 = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { }, });
            await process.StandardInput.WriteLineAsync(serializeResult74);
            var serializeResult75 = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 3, method = "resources/list", @params = new { }, });
            await process.StandardInput.WriteLineAsync(serializeResult75);
            var readLineAsyncResult = process.StandardOutput.ReadLineAsync();
            var fromSecondsResult = TimeSpan.FromSeconds(15);
            var initializeLine = await readLineAsyncResult.WaitAsync(fromSecondsResult);
            var readLineAsyncResult76 = process.StandardOutput.ReadLineAsync();
            var fromSecondsResult77 = TimeSpan.FromSeconds(15);
            var toolsLine = await readLineAsyncResult76.WaitAsync(fromSecondsResult77);
            var readLineAsyncResult78 = process.StandardOutput.ReadLineAsync();
            var fromSecondsResult79 = TimeSpan.FromSeconds(15);
            var resourcesLine = await readLineAsyncResult78.WaitAsync(fromSecondsResult79);
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(initializeLine);
            Assert.False(isNullOrWhiteSpaceResult);
            var isNullOrWhiteSpaceResult80 = string.IsNullOrWhiteSpace(toolsLine);
            Assert.False(isNullOrWhiteSpaceResult80);
            var isNullOrWhiteSpaceResult81 = string.IsNullOrWhiteSpace(resourcesLine);
            Assert.False(isNullOrWhiteSpaceResult81);
            using (var initialize = JsonDocument.Parse(initializeLine))
            {
                var getPropertyResult26 = initialize.RootElement.GetProperty("jsonrpc");
                var getStringResult27 = getPropertyResult26.GetString();
                Assert.Equal("2.0", getStringResult27);
                var getPropertyResult28 = initialize.RootElement.GetProperty("result");
                var getPropertyResult29 = getPropertyResult28.GetProperty("serverInfo");
                var getPropertyResult30 = getPropertyResult29.GetProperty("name");
                var getStringResult31 = getPropertyResult30.GetString();
                Assert.Equal("sereinflow", getStringResult31);
            }

            using (var tools = JsonDocument.Parse(toolsLine))
            {
                var getPropertyResult48 = tools.RootElement.GetProperty("result");
                var getPropertyResult49 = getPropertyResult48.GetProperty("tools");
                var enumerateArrayResult50 = getPropertyResult49.EnumerateArray();
                Assert.Contains(enumerateArrayResult50, tool =>
                {
                    var evaluatedValue35 = resolveGetStringResult32() == "sereinflow_list_projects";
                    return evaluatedValue35;
                    string? resolveGetStringResult32()
                    {
                        var getPropertyResult33 = tool.GetProperty("name");
                        var getStringResult34 = getPropertyResult33.GetString();
                        return getStringResult34;
                    }
                });
                var getPropertyResult51 = tools.RootElement.GetProperty("result");
                var getPropertyResult52 = getPropertyResult51.GetProperty("tools");
                var enumerateArrayResult53 = getPropertyResult52.EnumerateArray();
                Assert.Contains(enumerateArrayResult53, tool =>
                {
                    var evaluatedValue39 = resolveGetStringResult36() == "sereinflow_start_debug_session";
                    return evaluatedValue39;
                    string? resolveGetStringResult36()
                    {
                        var getPropertyResult37 = tool.GetProperty("name");
                        var getStringResult38 = getPropertyResult37.GetString();
                        return getStringResult38;
                    }
                });
                var getPropertyResult54 = tools.RootElement.GetProperty("result");
                var getPropertyResult55 = getPropertyResult54.GetProperty("tools");
                var enumerateArrayResult56 = getPropertyResult55.EnumerateArray();
                Assert.Contains(enumerateArrayResult56, tool =>
                {
                    var evaluatedValue43 = resolveGetStringResult40() == "sereinflow_step_debug";
                    return evaluatedValue43;
                    string? resolveGetStringResult40()
                    {
                        var getPropertyResult41 = tool.GetProperty("name");
                        var getStringResult42 = getPropertyResult41.GetString();
                        return getStringResult42;
                    }
                });
                var getPropertyResult57 = tools.RootElement.GetProperty("result");
                var getPropertyResult58 = getPropertyResult57.GetProperty("tools");
                var enumerateArrayResult59 = getPropertyResult58.EnumerateArray();
                Assert.Contains(enumerateArrayResult59, tool =>
                {
                    var evaluatedValue47 = resolveGetStringResult44() == "sereinflow_stop_debug";
                    return evaluatedValue47;
                    string? resolveGetStringResult44()
                    {
                        var getPropertyResult45 = tool.GetProperty("name");
                        var getStringResult46 = getPropertyResult45.GetString();
                        return getStringResult46;
                    }
                });
            }

            using (var resources = JsonDocument.Parse(resourcesLine))
            {
                var getPropertyResult68 = resources.RootElement.GetProperty("result");
                var getPropertyResult69 = getPropertyResult68.GetProperty("resources");
                var enumerateArrayResult70 = getPropertyResult69.EnumerateArray();
                Assert.Contains(enumerateArrayResult70, resource =>
                {
                    var evaluatedValue63 = resolveGetStringResult60() == "sereinflow://runs";
                    return evaluatedValue63;
                    string? resolveGetStringResult60()
                    {
                        var getPropertyResult61 = resource.GetProperty("uri");
                        var getStringResult62 = getPropertyResult61.GetString();
                        return getStringResult62;
                    }
                });
                var getPropertyResult71 = resources.RootElement.GetProperty("result");
                var getPropertyResult72 = getPropertyResult71.GetProperty("resources");
                var enumerateArrayResult73 = getPropertyResult72.EnumerateArray();
                Assert.Contains(enumerateArrayResult73, resource =>
                {
                    var evaluatedValue67 = resolveGetStringResult64() == "sereinflow://debug-sessions";
                    return evaluatedValue67;
                    string? resolveGetStringResult64()
                    {
                        var getPropertyResult65 = resource.GetProperty("uri");
                        var getStringResult66 = getPropertyResult65.GetString();
                        return getStringResult66;
                    }
                });
            }

            process.StandardInput.Close();
            var fromSecondsResult82 = TimeSpan.FromSeconds(15);
            using var exitTimeout = new CancellationTokenSource(fromSecondsResult82);
            await process.WaitForExitAsync(exitTimeout.Token);
            var diagnostics = await standardError;
            Assert.Equal(0, process.ExitCode);
            Assert.DoesNotContain("Now listening", diagnostics, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                var argument3 = TimeSpan.FromSeconds(5);
                await process.WaitForExitAsync().WaitAsync(argument3);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("obsolete-key-is-ignored")]
    public async Task StdioModeWorksWithoutKeysAndIgnoresInheritedLegacyKeys(string? legacyKey)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var directory = new TemporaryDirectory();
            using var process = StartStdioProcess(directory.Path, legacyKey, null);
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}");
                var argument4 = TimeSpan.FromSeconds(15);
                var line = await process.StandardOutput.ReadLineAsync().WaitAsync(argument4);
                using var response = JsonDocument.Parse(line!);
                {
                    var actual3 = response.RootElement.TryGetProperty("result", out _);
                    Assert.True(actual3);
                }

                process.StandardInput.Close();
                var argument5 = TimeSpan.FromSeconds(15);
                using var timeout = new CancellationTokenSource(argument5);
                await process.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, process.ExitCode);
                {
                    var actual4 = await process.StandardOutput.ReadToEndAsync();
                    Assert.Equal(string.Empty, actual4);
                }

                var argument6 = await error;
                Assert.DoesNotContain("Unhandled exception", argument6);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    var argument7 = TimeSpan.FromSeconds(5);
                    await process.WaitForExitAsync().WaitAsync(argument7);
                }
            }
        }
    }

    private static HttpRequestMessage CreateMcpRequest(string method, int id)
    {
        HttpRequestMessage evaluatedValue1 = new(HttpMethod.Post, "/mcp")
        {
            Content = resolveStringContent()
        };
        return evaluatedValue1;
        HttpContent resolveStringContent()
        {
            var serializeResult1 = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = new { } });
            var stringContent = new StringContent(serializeResult1, Encoding.UTF8, "application/json");
            return stringContent;
        }
    }

    private static Process StartStdioProcess(string dataRoot, string? apiKey, int? reservedPort)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!
        };
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("--mcp-stdio");
        startInfo.Environment["SereinFlow__DataRoot"] = dataRoot;
        startInfo.Environment.Remove("SereinFlow__Mcp__Stdio__ApiKey");
        if (apiKey is not null)
        {
            startInfo.Environment["SereinFlow__Mcp__Stdio__ApiKey"] = apiKey;
        }

        if (reservedPort is not null)
        {
            startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{reservedPort.Value}";
        }

        var evaluatedValue85 = Process.Start(startInfo) ?? throw new InvalidOperationException("The SereinFlow API stdio process could not be started.");
        return evaluatedValue85;
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
            builder.UseSetting("SereinFlow:Security:ApiKeys:BootstrapAdminKey", BootstrapKey);
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                var dictionary = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.Path,
                    ["SereinFlow:Security:ApiKeys:BootstrapAdminKey"] = BootstrapKey,
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
            var evaluatedValue86 = $"sereinflow-api-integration-{Guid.NewGuid():N}";
            Path = System.IO.Path.Combine(getTempPathResult, evaluatedValue86);
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
