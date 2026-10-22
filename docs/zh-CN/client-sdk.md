# SereinFlow .NET 客户端 SDK

`SereinFlow.SDK` 面向需要通过 HTTP 调用 SereinFlow 的第三方 .NET 程序。
公开命名空间为 `SereinFlow.Client`，核心包只依赖 `SereinFlow.Contracts`。

## 安装

```powershell
dotnet add package SereinFlow.SDK
```

ASP.NET Core 应用还可以安装 `SereinFlow.SDK.DependencyInjection`。

## 直接构造

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

注入 `ISereinFlowClient` 后，可以使用 `Flows`、`Runs`、`Messages` 和
`Workpieces` 四组客户端。

## 公开流程调用

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

## 节点输出和事件

```csharp
var output = await client.Runs.GetLatestNodeOutputAsync<MyResult>(run.Id, "result-node");

await foreach (var item in client.Runs.StreamEventsAsync(run.Id, afterSequence: 0, cancellationToken))
    Console.WriteLine($"{item.Sequence}: {item.Type}");
```

事件流使用服务端的 `Last-Event-ID` 断点语义。SDK 不会自动重连；断开后使用最后
一个序列号重新调用即可。

## 消息和工件

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

消息调用会自动生成 `Idempotency-Key`，并在安全重试期间复用。启动运行、公开调用
和取消不会自动重试，因为当前 REST 契约没有对应的幂等协议。

## 鉴权和错误

HTTP 宿主必须显式选择 `UseHostSecurity()` 或可选库的 `UseApiKeySecurity()`。独立宿主默认启用 Key 库；嵌入宿主复用现有身份与资源策略。初始化管理员接口为 `/api/security/api-keys/initial-admin`，仅允许直接回环访问。旧授权开关已移除。

- `run.execute`：启动、公开调用和取消；
- `run.read`：运行、输出、事件和工件读取；
- `run.message.publish`：消息推送。

HTTP 错误会抛出 `SereinFlowClientException`，可读取 `StatusCode`、`Code`、`Error`
和 `ResponseBody`。等待超时会抛出 `SereinFlowClientTimeoutException`，其中包含资源 ID
和最后已知状态。
