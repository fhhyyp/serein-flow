# 在 .NET 程序中调用 SereinFlow

如果 SereinFlow 已经在别处运行，你的程序可以注入 `ISereinFlowClient`，用 C# 方法启动流程、查看结果和下载文件。当前注册扩展在 `SereinFlow.SDK.DependencyInjection` 中。

## 注册客户端

项目引用 `SereinFlow.SDK.DependencyInjection` 后，在自己的 ASP.NET Core 或通用 .NET 宿主中注册：

```csharp
using SereinFlow.Client.DependencyInjection;

builder.Services.AddSereinFlowClient(options =>
{
    options.BaseAddress = new Uri("http://localhost:8188/");
    options.ApiKey = "你的 API Key";
});
```

`BaseAddress` 是运行中的 **SereinFlow 服务**地址，不是文档门户地址。只有目标服务启用了 API Key 验证时才需要填写 `ApiKey`；请在自己的配置中读取真实密钥，不要把它写进仓库。

注册方法会建立所需的 HTTP 客户端，并让代码可以注入 `ISereinFlowClient` 或 `SereinFlowClient`。

## 能做什么

| 客户端成员 | 常见用途 |
| --- | --- |
| `Flows` | 启动项目流程、调用公开流程、等待公开任务。 |
| `Runs` | 查询运行、读取输出和事件、等待完成、取消运行。 |
| `Messages` | 向正在运行的流程投递消息。 |
| `Workpieces` | 列出和下载流程生成的文件。 |

例如，在应用服务的构造函数中接收 `ISereinFlowClient` 后，可以调用 `client.Runs.GetAsync(runId)` 读取一次运行状态。启动流程需要项目 ID、流程 ID 和 `RunFlowRequestDto`；完整请求字段可在[运行 REST API](/api/runs)及运行中的 Swagger UI 查看。

这个客户端用于**调用已运行的服务**。如果要在你的进程里直接托管 SereinFlow，请看[嵌入 ASP.NET Core](/integration/hosting)。
