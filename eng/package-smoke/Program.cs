using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SereinFlow.AspNetCore;

var dataRoot = Path.Combine(Path.GetTempPath(), "sereinflow-package-smoke-" + Guid.NewGuid().ToString("N"));
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration["SereinFlow:DataRoot"] = dataRoot;
builder.Configuration["SereinFlow:Mcp:BootstrapAdminKey"] = "package-smoke-test-only";
builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi();
    options.UseMcp();
});

await using var app = builder.Build();
app.UseSereinFlow();
app.MapSereinFlow();
await app.StartAsync();

var runnerRoot = Path.Combine(AppContext.BaseDirectory, "sereinflow", "worker");
foreach (var file in new[]
         {
             "SereinFlow.Worker.Runner.dll",
             "SereinFlow.Worker.Runner.deps.json",
             "SereinFlow.Worker.Runner.runtimeconfig.json",
         })
{
    if (!File.Exists(Path.Combine(runnerRoot, file)))
        throw new InvalidOperationException($"Published Worker asset is missing: {file}");
}

using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
await RequireStatusAsync(await client.GetAsync("/healthz"), HttpStatusCode.OK);
await RequireStatusAsync(await client.GetAsync("/api/projects"), HttpStatusCode.Unauthorized);
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "package-smoke-test-only");
await RequireStatusAsync(await client.GetAsync("/api/projects"), HttpStatusCode.OK);
await RequireStatusAsync(await client.PostAsync("/hubs/runs/negotiate?negotiateVersion=1", null), HttpStatusCode.OK);
await RequireStatusAsync(await client.PostAsync("/hubs/workspace/negotiate?negotiateVersion=1", null), HttpStatusCode.OK);

using var initialized = await client.SendAsync(McpRequest("initialize", 1));
if (initialized.StatusCode != HttpStatusCode.OK)
    throw new InvalidOperationException($"MCP initialize returned {(int)initialized.StatusCode}.");
var session = initialized.Headers.GetValues("Mcp-Session-Id").Single();
client.DefaultRequestHeaders.Add("Mcp-Session-Id", session);

foreach (var method in new[] { "tools/list", "resources/list" })
    await RequireStatusAsync(await client.SendAsync(McpRequest(method, 2)), HttpStatusCode.OK);
using var guide = await client.SendAsync(McpRequest("resources/read", 3,
    "\"params\":{\"uri\":\"sereinflow://ai/guide\"}"));
var guideContent = await guide.Content.ReadAsStringAsync();
await RequireStatusAsync(guide, HttpStatusCode.OK);
if (!guideContent.Contains("SereinFlow", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("The embedded MCP guide was not readable.");

await app.StopAsync();
Console.WriteLine("SereinFlow package smoke test passed.");

static HttpRequestMessage McpRequest(string method, int id, string? parameters = null)
{
    var json = $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",{parameters ?? "\"params\":{}"}}}";
    return new HttpRequestMessage(HttpMethod.Post, "/mcp")
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}

static async Task RequireStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
{
    using (response)
    {
        if (response.StatusCode != expected)
            throw new InvalidOperationException(
                $"Expected {(int)expected}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
