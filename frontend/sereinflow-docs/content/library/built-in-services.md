# 使用内置服务

除了你用 `[FlowService]` 注册的类，Worker 还会给节点提供几项现成服务。把它们写在**节点类的构造函数**中即可。

| 服务 | 用来做什么 |
| --- | --- |
| `IMessageService` | 在同一次运行的节点之间收发消息，也可显式打开外部消息入口。 |
| `IFlowWorkpiece` | 把图片、报告等文件作为本次运行的节点输出保存。 |
| `IFlowNativeLibraryLoader` | 加载类库 ZIP 中随附的 native 文件。详见[专门教程](/library/native-dll)。 |

## 等待一条消息

`IMessageService.CreateMessageQueue()` 创建队列视图。相同主题中的一条消息只会被一个接收者取走。下面的等待节点允许外部 API 向 `device.inbox` 投递 JSON 消息：

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

[FlowLibrary("设备消息")]
public sealed class MessageNodes(IMessageService messages)
{
    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "等待设备消息")]
    public async Task<string> Wait(IFlowContext context)
    {
        var queue = messages.CreateMessageQueue(new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "device.message.v1"
        });
        return await queue.ReceiveAsync<string>("device.inbox", context.CancellationToken);
    }
}
```

队列默认用 JSON。外部投递需要明确设置 `ExternalIngress = true`；内部使用的主题默认不对外开放。`CreateEventBus()` 适合一条消息同时给多个已订阅者；事件订阅要及时异步释放。消息只存在于这次 Worker 运行期间，不能用作长期存储。

## 保存本次节点生成的文件

`IFlowWorkpiece.UploadNodeOutput` 接收文件名和内容，并用 `IFlowContext` 记下是哪个节点、哪一次执行生成了它。返回的 `FlowWorkpieceInfo` 包含文件 ID、名称、类型、大小等信息。

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

[FlowLibrary("报告工具")]
public sealed class ReportNodes(IFlowWorkpiece workpieces)
{
    [FlowNode(AnotherName = "保存报告")]
    public string Save(IFlowContext context)
    {
        var file = workpieces.UploadNodeOutput(
            context, "report.txt", "检查完成", "text/plain; charset=utf-8");
        return file.Id;
    }
}
```

它也能接收 `byte[]` 或 `Stream`，适合图片和其他二进制内容。上传后，可在运行的工件列表中查看和下载，相关 HTTP 地址见[运行与工件 API](/api/runs)。

::: tip 两种传入方式
`IFlowContext` 写在**节点方法参数**里；上表中的三个服务写在**节点类构造函数**里。这样每个方法调用会拿到自己的运行信息，而类可以重复使用注入的服务。
:::
