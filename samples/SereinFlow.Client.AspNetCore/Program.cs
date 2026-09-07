using SereinFlow.Client;
using SereinFlow.Client.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSereinFlowClient(options =>
{
    options.BaseAddress = new Uri(
        builder.Configuration["SereinFlow:BaseAddress"] ?? "http://127.0.0.1:8188/");
    options.ApiKey = builder.Configuration["SereinFlow:ApiKey"];
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/runs/{runId:guid}", async (Guid runId, ISereinFlowClient client, CancellationToken cancellationToken) =>
    Results.Ok(await client.Runs.GetAsync(runId, cancellationToken)));
await app.RunAsync();
