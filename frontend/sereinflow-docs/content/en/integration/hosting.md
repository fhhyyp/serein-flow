# Host in ASP.NET Core

Add SereinFlow to an existing ASP.NET Core app from the `SereinFlow.AspNetCore` assembly, targeting .NET 10.

## Minimal setup

After referencing `SereinFlow.AspNetCore`, add:

```csharp
using SereinFlow.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi();
    options.UseMcp();
});

var app = builder.Build();
app.UseSereinFlow();
app.MapSereinFlow();
app.Run();
```

Enable at least one of `UseWebApi()` or `UseMcp()`. Call `AddSereinFlow`, `UseSereinFlow`, and `MapSereinFlow` once each in the same host, and map routes before starting it.

## What the three calls do

| Call | Purpose |
| --- | --- |
| `builder.Services.AddSereinFlow(...)` | Register storage, application and execution services, plus enabled Web API or MCP services. |
| `app.UseSereinFlow()` | Add required middleware and, when enabled, Swagger UI. |
| `app.MapSereinFlow()` | Map REST controllers, run/workspace SignalR hubs, and the HTTP MCP endpoint when enabled. |

With Web API enabled, hubs are at `/hubs/runs` and `/hubs/workspace`. Swagger is enabled in Development by default; set `UseWebApi(api => api.EnableSwagger = true)` to enable it explicitly. `SereinFlowWebApiOptions` also configures CORS origins, OpenAPI, and Swagger UI paths. Set the MCP path with `UseMcp(mcp => mcp.EndpointPath = "/mcp")`.

## Verify it

Start the host and check health as in [Quick start](/en/guide/getting-started), then create a flow in the console. For node-accessible functions, see [Built-in services](/en/library/built-in-services). For host registrations, see [Internal service registration](/en/integration/service-registration).
