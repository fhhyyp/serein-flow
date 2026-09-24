# 嵌入 ASP.NET Core

如果你已有一个 ASP.NET Core 程序，可以在它的 `Program.cs` 中加入 SereinFlow。当前公开入口在 `SereinFlow.AspNetCore` 程序集中，目标框架是 .NET 10。

## 最小接入

项目引用 `SereinFlow.AspNetCore` 后，可以这样写：

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

至少启用 `UseWebApi()` 或 `UseMcp()` 中的一项。`AddSereinFlow`、`UseSereinFlow`、`MapSereinFlow` 在同一个宿主里各调用一次。映射路由要在应用启动前完成。

## 这三行分别做什么

| 调用 | 作用 |
| --- | --- |
| `builder.Services.AddSereinFlow(...)` | 注册存储、应用服务、执行服务，以及你启用的 Web API 或 MCP。 |
| `app.UseSereinFlow()` | 加入 SereinFlow 需要的中间件；开启文档时也提供 Swagger UI。 |
| `app.MapSereinFlow()` | 映射 REST 控制器、运行和工作区 SignalR Hub，以及启用时的 HTTP MCP 地址。 |

Web API 启用时，两个 Hub 分别位于 `/hubs/runs` 与 `/hubs/workspace`。Swagger 默认只在 Development 环境开启；也可以通过 `UseWebApi(api => api.EnableSwagger = true)` 明确开启。`SereinFlowWebApiOptions` 还可设置跨域来源、OpenAPI 路径和 Swagger UI 路径。MCP 的地址可以通过 `UseMcp(mcp => mcp.EndpointPath = "/mcp")` 设置。

## 接下来怎么验证

启动宿主后，按[快速开始](/guide/getting-started)中的方式访问健康检查，再用控制台创建流程。需要知道节点类库如何使用宿主提供的功能，请读[内置服务](/library/built-in-services)；SereinFlow 在宿主里注册了哪些服务，请读[内部服务怎样注册](/integration/service-registration)。
