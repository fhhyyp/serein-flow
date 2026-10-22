# Hosting SereinFlow in ASP.NET Core

The hosting library targets .NET 10. The first version composes REST API, SignalR, and HTTP MCP. `UseMcp()` does not start the stdio MCP transport. An empty library catalog is valid; startup-time library registration is not part of this version.

```csharp
using SereinFlow.AspNetCore;
using SereinFlow.Security.ApiKeys;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi();
    options.UseMcp();
    options.UseApiKeySecurity();
});

var app = builder.Build();
app.UseSereinFlow();
app.MapSereinFlow();
app.Run();
```

`AddSereinFlow` registers storage, application services, and Worker execution. `UseSereinFlow` installs HTTP middleware; `MapSereinFlow` maps controllers, the two hubs, and `/mcp`. Either transport can be enabled alone. Duplicate registration is rejected. Attribute-routed host controllers share MVC's `MapControllers()` and must not conflict with SereinFlow routes.

Set `SereinFlow:DataRoot` for persistent data. A relative `SereinFlow:WorkerRunnerPath` resolves against ContentRoot; a managed DLL also needs its `.deps.json` and `.runtimeconfig.json`. Packaged deployments use `sereinflow/worker/` under the application output by default. SQLite initialization, Runner validation, duplicate-route checks, and upload-limit checks happen before the host serves requests.

HTTP hosts explicitly select `UseHostSecurity()` or `UseApiKeySecurity()` from the optional security package. The standalone host enables API-key security by default; embedding hosts reuse existing identities and resource policies. Initial setup uses `/api/security/api-keys/initial-admin` and accepts only direct loopback requests. The previous authentication switch has been removed.

Swagger defaults to Development only; `options.UseWebApi(api => api.EnableSwagger = true)` overrides it. `OpenApiRouteTemplate` and `SwaggerUiRoutePrefix` customize its paths. REST and hubs use the `sereinflow-webapi` named CORS policy, which denies cross-origin access by default; set `api.AllowedOrigins` or `SereinFlow:Api:Cors:AllowedOrigins` to allow specific origins. The standalone host also requires an explicit origin allowlist for cross-origin browser access. Reverse-proxy upload limits must be configured separately. SereinFlow controllers use their own exception filter rather than replacing the host's exception handler. `UseSereinFlow()` calls `UseCors()`, so check its ordering with the host's CORS middleware.

Run `eng/pack-aspnetcore.ps1` to create one `SereinFlow.AspNetCore` NuGet package. It includes the repository's host assemblies under `lib/net10.0/` and the complete published Worker Runner under `worker/`. Consumers need to distribute only this package; NuGet still restores third-party dependencies such as SQLite, SqlSugar, and Swagger. A direct `dotnet pack` of the facade project rejects missing published host and Worker assets. Verify restore, publish, and runtime behavior from a clean project outside this repository before distributing the package.


The base hosting package contains no API-key implementation. Install the same-version `SereinFlow.Security.ApiKeys` package for default key security. Embedding hosts instead select `UseHostSecurity()`, register `ISereinFlowProjectScopeProvider`, configure ASP.NET Core resource policies for operation names, and call their own `UseAuthentication()` before `UseSereinFlow()`. See `samples/SereinFlow.HostSecurity`. Browser sessions use `/api/security/session`, last eight hours without sliding renewal, and use HttpOnly, SameSite=Strict cookies. Requests revalidate their keys. stdio does not register the key package.
