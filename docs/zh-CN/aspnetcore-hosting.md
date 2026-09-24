# 在 ASP.NET Core 项目中托管 SereinFlow

目标框架为 .NET 10。首版提供 REST API、SignalR 与 HTTP MCP 的组合式宿主；`UseMcp()` **不**启动 stdio MCP。类库目录初始可以为空，首版没有 `AddFlowLibraryPackage(path)`、`AddFlowLibrary<T>()` 或默认类库注册。

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

`AddSereinFlow` 注册存储、应用服务和 Worker 执行服务；`UseSereinFlow` 安装 HTTP 中间件；`MapSereinFlow` 映射 Controller、`/hubs/runs`、`/hubs/workspace` 与 `/mcp`。只启用 Web API 或只启用 MCP 均可。重复调用 `AddSereinFlow` 或同一个 `Use...` 会抛出异常。宿主自己的属性路由 Controller 与 SereinFlow 共享 MVC 的 `MapControllers()`，需避免路由冲突。

通过 `SereinFlow:DataRoot` 指定数据目录。相对的 `SereinFlow:WorkerRunnerPath` 按 ContentRoot 解析；显式路径必须存在，托管 DLL 还必须有配套的 `.deps.json` 和 `.runtimeconfig.json`。打包部署时 Runner 默认位于发布目录下的 `sereinflow/worker/`。启动时会初始化 SQLite、检查 Runner、检查重复路由和 Kestrel 上传限额；失败会阻止服务启动。独立宿主需要安装同版本的 .NET 10 ASP.NET Core 运行时。

外部宿主 REST 默认要求 API 密钥；未单独实现项目级权限的 REST 操作要求管理员密钥。`/healthz` 和仅允许 loopback 的 `/api/environment/settings/mcp-keys/setup` 例外。官方宿主在 `appsettings.json` 中显式保留了旧版 `RequireAuthentication=false`。MCP 始终要求密钥。生产环境应限制初始密钥设置入口的网络可达性，勿把 `SereinFlow:Mcp:BootstrapAdminKey` 写入源码或日志。

Swagger 默认只在 Development 启用，可通过 `options.UseWebApi(api => api.EnableSwagger = true)` 显式启用，或配置 `SereinFlow:ApiDocumentation:Enabled`；`OpenApiRouteTemplate` 和 `SwaggerUiRoutePrefix` 可更改路径。REST/Hub 使用 `sereinflow-webapi` 命名 CORS 策略，默认不开放跨域；可配置 `api.AllowedOrigins` 或 `SereinFlow:Api:Cors:AllowedOrigins`，官方宿主显式设置 `AllowAnyOrigin=true` 保持旧行为。REST 上传限额和 MCP 请求限额需要同时考虑反向代理配置。SereinFlow Controller 使用自己的异常过滤器，不替换宿主的全局异常处理；`UseSereinFlow()` 会调用 `UseCors()`，宿主应测试自己的 CORS 中间件顺序。

运行 `eng/pack-aspnetcore.ps1` 会生成单个 `SereinFlow.AspNetCore` NuGet 包：宿主所需的仓库内程序集位于 `lib/net10.0/`，Worker Runner 的完整发布目录位于 `worker/`。使用者只需发布这个包；SQLite、SqlSugar 和 Swagger 等第三方依赖仍由 NuGet 还原。直接 `dotnet pack` 门面项目会因缺少已发布的宿主及 Worker 文件而拒绝生成不完整的包。发布包的验收还需使用仓库外的空白项目完成 restore、publish 和运行验证，不能仅凭打包成功判断可交付。
