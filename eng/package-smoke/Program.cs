using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SereinFlow.AspNetCore;

var getTempPathResult = Path.GetTempPath();
string resolveToStringResult()
{
    var newGuidResult = Guid.NewGuid();
    var toStringResult = newGuidResult.ToString("N");
    return toStringResult;
}

var evaluatedValue1 = "sereinflow-package-smoke-" + resolveToStringResult();
var dataRoot = Path.Combine(getTempPathResult, evaluatedValue1);
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
}

)
{
    if (!resolveExistsResult())
    {
        throw new InvalidOperationException($"Published Worker asset is missing: {file}");
    }

    bool resolveExistsResult()
    {
        var combineResult = Path.Combine(runnerRoot, file);
        var existsResult = File.Exists(combineResult);
        return existsResult;
    }
}

Uri resolveUri()
{
    var singleResult = app.Urls.Single();
    var uri = new Uri(singleResult);
    return uri;
}

using var client = new HttpClient
{
    BaseAddress = resolveUri()
};
var getAsyncResult = await client.GetAsync("/healthz");
await RequireStatusAsync(getAsyncResult, HttpStatusCode.OK);
var getAsyncResult2 = await client.GetAsync("/api/projects");
await RequireStatusAsync(getAsyncResult2, HttpStatusCode.Unauthorized);
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "package-smoke-test-only");
var getAsyncResult3 = await client.GetAsync("/api/projects");
await RequireStatusAsync(getAsyncResult3, HttpStatusCode.OK);
var postAsyncResult = await client.PostAsync("/hubs/runs/negotiate?negotiateVersion=1", null);
await RequireStatusAsync(postAsyncResult, HttpStatusCode.OK);
var postAsyncResult4 = await client.PostAsync("/hubs/workspace/negotiate?negotiateVersion=1", null);
await RequireStatusAsync(postAsyncResult4, HttpStatusCode.OK);
var mcpRequestResult5 = McpRequest("initialize", 1);
using var initialized = await client.SendAsync(mcpRequestResult5);
if (initialized.StatusCode != HttpStatusCode.OK)
{
    throw new InvalidOperationException($"MCP initialize returned {(int)initialized.StatusCode}.");
}

var getValuesResult = initialized.Headers.GetValues("Mcp-Session-Id");
var session = getValuesResult.Single();
client.DefaultRequestHeaders.Add("Mcp-Session-Id", session);
foreach (var method in new[]
{
    "tools/list",
    "resources/list"
}

)
{
    var mcpRequestResult = McpRequest(method, 2);
    var sendAsyncResult = await client.SendAsync(mcpRequestResult);
    await RequireStatusAsync(sendAsyncResult, HttpStatusCode.OK);
}

var mcpRequestResult6 = McpRequest("resources/read", 3, "\"params\":{\"uri\":\"sereinflow://ai/guide\"}");
using var guide = await client.SendAsync(mcpRequestResult6);
var guideContent = await guide.Content.ReadAsStringAsync();
await RequireStatusAsync(guide, HttpStatusCode.OK);
if (!guideContent.Contains("SereinFlow", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("The embedded MCP guide was not readable.");
}

await app.StopAsync();
Console.WriteLine("SereinFlow package smoke test passed.");
static HttpRequestMessage McpRequest(string method, int id, string? parameters = null)
{
    var json = $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",{parameters ?? "\"params\":{}"}}}";
    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/mcp")
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
    return httpRequestMessage;
}

static async Task RequireStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
{
    using (response)
    {
        if (response.StatusCode != expected)
        {
            var evaluatedValue = $"Expected {(int)expected}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
            throw new InvalidOperationException(evaluatedValue);
        }
    }
}
