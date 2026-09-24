# Hosting SereinFlow in ASP.NET Core

The hosting library targets .NET 10. The first version composes REST API, SignalR, and HTTP MCP. `UseMcp()` does not start the stdio MCP transport. An empty library catalog is valid; startup-time library registration is not part of this version.

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

`AddSereinFlow` registers storage, application services, and Worker execution. `UseSereinFlow` installs HTTP middleware; `MapSereinFlow` maps controllers, the two hubs, and `/mcp`. Either transport can be enabled alone. Duplicate registration is rejected. Attribute-routed host controllers share MVC's `MapControllers()` and must not conflict with SereinFlow routes.

Set `SereinFlow:DataRoot` for persistent data. A relative `SereinFlow:WorkerRunnerPath` resolves against ContentRoot; a managed DLL also needs its `.deps.json` and `.runtimeconfig.json`. Packaged deployments use `sereinflow/worker/` under the application output by default. SQLite initialization, Runner validation, duplicate-route checks, and upload-limit checks happen before the host serves requests.

REST in an external host requires an API key by default. Operations without dedicated project-scoped authorization require an administrator key. `/healthz` and the loopback-only initial key setup endpoint are exceptions. The official host explicitly retains the older `RequireAuthentication=false` configuration. MCP always requires a key. Keep bootstrap secrets out of source control and logs.

Swagger defaults to Development only; `options.UseWebApi(api => api.EnableSwagger = true)` overrides it. `OpenApiRouteTemplate` and `SwaggerUiRoutePrefix` customize its paths. REST and hubs use the `sereinflow-webapi` named CORS policy, which denies cross-origin access by default; set `api.AllowedOrigins` or `SereinFlow:Api:Cors:AllowedOrigins` to allow specific origins. The official host explicitly sets `AllowAnyOrigin=true` for legacy behavior. Reverse-proxy upload limits must be configured separately. SereinFlow controllers use their own exception filter rather than replacing the host's exception handler. `UseSereinFlow()` calls `UseCors()`, so check its ordering with the host's CORS middleware.

Run `eng/pack-aspnetcore.ps1` to build a local NuGet feed. A direct `dotnet pack` of the facade project rejects missing published Worker assets. Verify restore, publish, and runtime behavior from a clean project outside this repository before distributing the package.
