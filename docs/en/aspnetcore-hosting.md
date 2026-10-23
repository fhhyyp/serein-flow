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

HTTP, SignalR, and HTTP MCP allow direct access by default (`None` mode). Network isolation and authentication are application-developer choices. The standalone host enables the optional key package only when `SereinFlow:Security:Mode` is `ApiKey`. Embedding hosts opt in with `UseApiKeySecurity()` or reuse existing identities with `UseHostSecurity()`. With API-key authentication enabled, initial setup uses `/api/security/api-keys/initial-admin` and accepts only direct loopback requests.

Swagger defaults to Development only; `options.UseWebApi(api => api.EnableSwagger = true)` overrides it. `OpenApiRouteTemplate` and `SwaggerUiRoutePrefix` customize its paths. REST and hubs use the `sereinflow-webapi` named CORS policy, which denies cross-origin access by default; set `api.AllowedOrigins` or `SereinFlow:Api:Cors:AllowedOrigins` to allow specific origins. The standalone host also requires an explicit origin allowlist for cross-origin browser access. Reverse-proxy upload limits must be configured separately. SereinFlow controllers use their own exception filter rather than replacing the host's exception handler. `UseSereinFlow()` calls `UseCors()`, so check its ordering with the host's CORS middleware.

Run `eng/pack-aspnetcore.ps1` to create one `SereinFlow.AspNetCore` NuGet package. It includes the repository's host assemblies under `lib/net10.0/` and the complete published Worker Runner under `worker/`. Consumers need to distribute only this package; NuGet still restores third-party dependencies such as SQLite, SqlSugar, and Swagger. A direct `dotnet pack` of the facade project rejects missing published host and Worker assets. Verify restore, publish, and runtime behavior from a clean project outside this repository before distributing the package.


The base hosting package contains no API-key implementation. To opt in, install the same-version `SereinFlow.Security.ApiKeys` package and call `UseApiKeySecurity()`. For existing authentication, select `UseHostSecurity()`, register `ISereinFlowProjectScopeProvider`, configure ASP.NET Core resource policies for operation names, and call the host's `UseAuthentication()` before `UseSereinFlow()`. See `samples/SereinFlow.HostSecurity`. In API-key mode, browser sessions use `/api/security/session`, last eight hours without sliding renewal, and use HttpOnly, SameSite=Strict cookies. Requests revalidate their keys. Default mode and stdio do not register the key services.
