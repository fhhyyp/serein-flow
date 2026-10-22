# 在 ASP.NET Core 项目中托管 SereinFlow

目标框架为 .NET 10。首版提供 REST API、SignalR 与 HTTP MCP 的组合式宿主；`UseMcp()` **不**启动 stdio MCP。类库目录初始可以为空，首版没有 `AddFlowLibraryPackage(path)`、`AddFlowLibrary<T>()` 或默认类库注册。

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

`AddSereinFlow` 注册存储、应用服务和 Worker 执行服务；`UseSereinFlow` 安装 HTTP 中间件；`MapSereinFlow` 映射 Controller、`/hubs/runs`、`/hubs/workspace` 与 `/mcp`。只启用 Web API 或只启用 MCP 均可。重复调用 `AddSereinFlow` 或同一个 `Use...` 会抛出异常。宿主自己的属性路由 Controller 与 SereinFlow 共享 MVC 的 `MapControllers()`，需避免路由冲突。

通过 `SereinFlow:DataRoot` 指定数据目录。相对的 `SereinFlow:WorkerRunnerPath` 按 ContentRoot 解析；显式路径必须存在，托管 DLL 还必须有配套的 `.deps.json` 和 `.runtimeconfig.json`。打包部署时 Runner 默认位于发布目录下的 `sereinflow/worker/`。启动时会初始化 SQLite、检查 Runner、检查重复路由和 Kestrel 上传限额；失败会阻止服务启动。独立宿主需要安装同版本的 .NET 10 ASP.NET Core 运行时。

HTTP 宿主必须显式选择 `UseHostSecurity()` 或可选库的 `UseApiKeySecurity()`。独立宿主默认启用 Key 库；嵌入宿主复用现有身份与资源策略。初始化管理员接口为 `/api/security/api-keys/initial-admin`，仅允许直接回环访问。旧授权开关已移除。

Swagger 默认只在 Development 启用，可通过 `options.UseWebApi(api => api.EnableSwagger = true)` 显式启用，或配置 `SereinFlow:ApiDocumentation:Enabled`；`OpenApiRouteTemplate` 和 `SwaggerUiRoutePrefix` 可更改路径。REST/Hub 使用 `sereinflow-webapi` 命名 CORS 策略，默认不开放跨域；可配置 `api.AllowedOrigins` 或 `SereinFlow:Api:Cors:AllowedOrigins`，独立宿主同样要求显式配置浏览器跨域来源白名单。REST 上传限额和 MCP 请求限额需要同时考虑反向代理配置。SereinFlow Controller 使用自己的异常过滤器，不替换宿主的全局异常处理；`UseSereinFlow()` 会调用 `UseCors()`，宿主应测试自己的 CORS 中间件顺序。

运行 `eng/pack-aspnetcore.ps1` 会生成单个 `SereinFlow.AspNetCore` NuGet 包：宿主所需的仓库内程序集位于 `lib/net10.0/`，Worker Runner 的完整发布目录位于 `worker/`。使用者只需发布这个包；SQLite、SqlSugar 和 Swagger 等第三方依赖仍由 NuGet 还原。直接 `dotnet pack` 门面项目会因缺少已发布的宿主及 Worker 文件而拒绝生成不完整的包。发布包的验收还需使用仓库外的空白项目完成 restore、publish 和运行验证，不能仅凭打包成功判断可交付。


基础宿主包不包含 Key 实现。需要默认 Key 功能时额外安装同版本的 `SereinFlow.Security.ApiKeys`。第三方宿主应改用 `UseHostSecurity()`，注册 `ISereinFlowProjectScopeProvider`，为操作名配置 ASP.NET Core 资源策略，并在 `UseSereinFlow()` 之前调用宿主的 `UseAuthentication()`。完整示例见 `samples/SereinFlow.HostSecurity`。浏览器通过 `/api/security/session` 建立八小时、不滑动续期的 HttpOnly、SameSite=Strict 会话；后续请求重新验证 Key。stdio 不加载 Key 库。
