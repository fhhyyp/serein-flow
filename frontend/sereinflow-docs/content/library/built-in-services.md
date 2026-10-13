# 使用内置服务

除了你用 `[FlowService]` 注册的类，Worker 还会给节点提供几项现成服务。把它们写在**节点类的构造函数**中即可。

| 服务 | 用来做什么 |
| --- | --- |
| `LibraryVariableService` | 在同一次运行的所有流程上下文和类库之间共用变量，读写受锁保护。 |
| `IMessageService` | 在同一次运行的节点之间收发消息，也可显式打开外部消息入口。 |
| `IFlowWorkpiece` | 把图片、报告等文件作为本次运行的节点输出保存。 |
| `IFlowNativeLibraryLoader` | 加载类库 ZIP 中随附的 native 文件。详见[专门教程](/library/native-dll)。 |

## 共用流程变量

每次运行都有一张 `LibraryVariableService` 变量表，节点、触发器和 `FlowCall` 子流程上下文共用它。构造函数注入的服务与 `IFlowContext.Variables` 是同一个实例；不同运行互相隔离。原始 CLR 对象仅在 Worker 内存中，结束时保存一次有界 JSON 投影及类型信息供历史快照查看。所有 SDK 类型统一通过 `using SereinFlow.Library;` 引入。使用旧 SDK 命名空间的类库需更新引用并重新编译。

```csharp
using SereinFlow.Library;

[FlowLibrary("批次变量")]
public sealed class BatchNodes(LibraryVariableService variables)
{
    [FlowNode(AnotherName = "保存批次")]
    public void Save(IFlowContext context)
    {
        variables.Set("batch.count", 42);
        context.Variables.Set<string?>("batch.note", null);
    }

    [FlowNode(AnotherName = "读取批次")]
    public int Read() => variables.Get<int>("batch.count");
}
```

| 方法 | 结果 |
| --- | --- |
| `Contain(name)` | 名称存在时返回 true，包括值为 null 的变量。 |
| `Get(name)` | 返回原始 object；不存在时返回 null。 |
| `Get<T>(name)` | 强制转换或拆箱为 T；不存在时返回 `default(T)`。 |
| `TryGet<T>(name, out value)` | 不存在时返回 false 和 `default(T)`；存在时转换或拆箱并返回 true。 |
| `Set<T>(name, value)` | 新增或覆盖变量，可存入 null。 |

变量名区分大小写，不能为 null、空字符串或纯空白。值类型装箱，引用对象保留原引用。泛型读取遵循 CLR 强制转换规则，不解析字符串或自动转换数值：`int` 不能读作 `long`。类型不匹配时，`Get<T>` 和 `TryGet<T>` 都抛出 `InvalidCastException`；将已存储的 null 读为非可空值类型也会抛出该异常。`Get<int>` 在变量不存在时返回 0，可用 `Contain` 或 `TryGet` 区分。

锁保护每次表内读写；多次方法调用组成的“读取再更新”以及已存入对象内部的修改，需要调用方自行同步。

`Snapshot()` 可在锁保护下复制当前变量表，复制后的字典与原表独立，值仍保留对象引用。运行期间也可以通过 HTTP/MCP 读写变量，并在工作区的 **流程变量** 面板动态查看，接口与类型约定见[运行 API](/api/runs#流程变量表)。

## 等待一条消息

`IMessageService.CreateMessageQueue()` 创建队列视图。相同主题中的一条消息只会被一个接收者取走。下面的等待节点允许外部 API 向 `device.inbox` 投递 JSON 消息：

```csharp
using SereinFlow.Library;

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
using SereinFlow.Library;

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
`IFlowContext` 写在**节点方法参数**里；上表中的服务写在**节点类构造函数**里。这样每个方法调用会拿到自己的运行信息，而类可以重复使用注入的服务。变量表也可以通过 `context.Variables` 访问。
:::
