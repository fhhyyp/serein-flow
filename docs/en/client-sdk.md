# SereinFlow .NET Client SDK

`SereinFlow.SDK` is a typed HTTP client for third-party .NET applications
that invoke and inspect SereinFlow workflows. Its public namespace is
`SereinFlow.Client`, and the core package only references
`SereinFlow.Contracts`.

## Installation

```powershell
dotnet add package SereinFlow.SDK
```

ASP.NET Core applications can also install
`SereinFlow.SDK.DependencyInjection`.

## Direct construction

```csharp
using SereinFlow.Client;
using SereinFlow.Contracts;

using var client = new SereinFlowClient(new SereinFlowClientOptions
{
    BaseAddress = new Uri("http://127.0.0.1:8188/"),
    BearerTokenProvider = _ => ValueTask.FromResult(configuration["SereinFlow:BearerToken"]),
});

var run = await client.Flows.StartRunAsync(
    projectId,
    flowId,
    new RunFlowRequestDto(
        ExpectedFlowVersion: null,
        ProjectInputs: new Dictionary<string, JsonElement>(),
        TimeoutSeconds: 60,
        MaxSteps: null));

var result = await client.Runs.WaitForCompletionAsync(run.Id);
if (!result.IsSucceeded)
    throw new InvalidOperationException(result.Run.ErrorSummary);
```

## ASP.NET Core DI

```csharp
using SereinFlow.Client.DependencyInjection;

builder.Services.AddSereinFlowClient(options =>
{
    options.BaseAddress = new Uri("http://127.0.0.1:8188/");
    options.BearerTokenProvider = _ => ValueTask.FromResult(builder.Configuration["SereinFlow:BearerToken"]);
});
```

Inject `ISereinFlowClient` and use its `Flows`, `Runs`, `Messages`, and
`Workpieces` clients.

## Public invocation

```csharp
var response = await client.Flows.InvokeAsync(
    interfaceId,
    new PublicFlowInvocationRequestDto(
        new Dictionary<string, JsonElement>
        {
            ["orderId"] = JsonSerializer.SerializeToElement("A10001")
        },
        TimeoutSeconds: 60));

if (!response.IsCompleted)
    response = await client.Flows.WaitForPublicTaskAsync(response.TaskId);
```

## Node output and events

```csharp
var output = await client.Runs.GetLatestNodeOutputAsync<MyResult>(run.Id, "result-node");

await foreach (var item in client.Runs.StreamEventsAsync(run.Id, afterSequence: 0, cancellationToken))
    Console.WriteLine($"{item.Sequence}: {item.Type}");
```

The event stream uses the server's `Last-Event-ID` resume semantics. The SDK
does not reconnect automatically; reconnect with the last received sequence.

## Messages and workpieces

```csharp
await client.Messages.PublishMessageAsync(
    run.Id,
    "orders.created",
    new { orderId = "A10001" });

var workpieces = await client.Workpieces.ListAsync(run.Id);
await using var download = await client.Workpieces.DownloadAsync(run.Id, workpieces[0].Id);
await using var file = File.Create(download.FileName ?? "workpiece.bin");
await download.Content.CopyToAsync(file);
```

Message calls generate an `Idempotency-Key` automatically and reuse it during
safe retries. Starting a run, public invocation, and cancellation are not
automatically retried because the current REST contract has no corresponding
idempotency protocol.

## Authentication and errors

HTTP and MCP allow direct access by default (`None` mode). Authentication is optional: set `SereinFlow:Security:Mode` to `ApiKey` in the standalone host, or explicitly select `UseApiKeySecurity()` or `UseHostSecurity()` in an embedding host. In API-key mode, initial setup uses `/api/security/api-keys/initial-admin` and accepts only direct loopback requests. The permissions below apply when authentication is enabled.

- `run.execute`: start, invoke, and cancel;
- `run.read`: read runs, outputs, events, and workpieces;
- `run.message.publish`: publish run messages.

HTTP failures throw `SereinFlowClientException`, which exposes `StatusCode`,
`Code`, `Error`, and `ResponseBody`. Wait timeouts throw
`SereinFlowClientTimeoutException` with the resource ID and last known state.
