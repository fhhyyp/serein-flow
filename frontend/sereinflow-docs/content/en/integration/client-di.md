# Call SereinFlow from .NET

If SereinFlow runs elsewhere, inject `ISereinFlowClient` to start flows, inspect results, and download files. The registration extension is in `SereinFlow.SDK.DependencyInjection`.

## Register the client

After referencing `SereinFlow.SDK.DependencyInjection` in an ASP.NET Core or generic .NET host:

```csharp
using SereinFlow.Client.DependencyInjection;

builder.Services.AddSereinFlowClient(options =>
{
    options.BaseAddress = new Uri("http://localhost:8188/");
    options.ApiKey = "your API key";
});
```

`BaseAddress` is the running **SereinFlow service**, not this documentation portal. Set `ApiKey` only when the service requires it; load the real secret from your configuration rather than source control.

The registration supplies an HTTP client and allows injection of `ISereinFlowClient` or `SereinFlowClient`.

## Available members

| Member | Common use |
| --- | --- |
| `Flows` | Start project flows, invoke public flows, wait for public tasks. |
| `Runs` | Read runs, outputs, and events; wait or cancel. |
| `Messages` | Publish to a running flow. |
| `Workpieces` | List and download generated files. |

For example, call `client.Runs.GetAsync(runId)` to read a run's state. Starting a flow needs project and flow IDs plus `RunFlowRequestDto`; see [Runs REST API](/en/api/runs) and the running service's Swagger UI.

This client calls a **running service**. To host SereinFlow in your process, see [Host in ASP.NET Core](/en/integration/hosting).
