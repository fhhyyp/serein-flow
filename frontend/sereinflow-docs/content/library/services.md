# 注册自己的服务

如果两个节点都要访问同一台设备，不必在每个方法里重复建立连接。可以把设备操作放进一个普通 C# 类，并在这个类上加 `[FlowService]`。SereinFlow 会在执行节点时把它交给节点构造函数。

## 一个最小例子

```csharp
using SereinFlow.Library;

namespace MyFlowNodes;

public interface IDeviceReader
{
    string ReadStatus(string deviceId);
}

[FlowService<IDeviceReader>]
public sealed class DeviceReader : IDeviceReader
{
    public string ReadStatus(string deviceId) => $"{deviceId}: ready";
}

[FlowLibrary("设备工具")]
public sealed class DeviceNodes(IDeviceReader reader)
{
    [FlowNode(AnotherName = "读取设备状态")]
    public string Read([NodeParam(Name = "设备编号")] string deviceId)
        => reader.ReadStatus(deviceId);
}
```

这里 `[FlowService<IDeviceReader>]` 注册 `DeviceReader`，节点构造函数用 `IDeviceReader` 接收它。如果只需要按具体类注入，写 `[FlowService]` 就够了。非泛型写法是 `[FlowService(typeof(IDeviceReader))]`。

## 对象会使用多久

| `Lifetime` | 好理解的意思 | 适合什么 |
| --- | --- | --- |
| `Run`（默认） | 同一个类库在一次 Worker 运行中共用一个对象。 | 本次运行共享的连接或状态。 |
| `Invocation` | 一次节点调用里共用一个对象。 | 只在当前节点调用里使用的工作对象。 |
| `Transient` | 每次请求这个服务都新建。 | 不保留共享状态的轻量对象。 |

例如 `[FlowService<IDeviceReader>(Lifetime = FlowServiceLifetime.Invocation)]` 会改成每次节点调用使用一份。`Run` 与 `Invocation` 下，同一个服务用接口或具体类解析，得到的是同一个对象。这里没有跨运行的单例。

## 哪些东西能注入

节点类的构造函数可接收同一上传程序集用 `[FlowService]` 注册的服务，也可以接收 Worker 提供的 `IMessageService`、`IFlowWorkpiece`、`IFlowNativeLibraryLoader`。注册的实现应是同一上传程序集内的具体、非开放泛型类。

`IFlowContext` 属于**每次方法调用**，要写在节点方法参数里。宿主的 `IServiceProvider`、`IServiceScopeFactory`、配置、API 仓储、密钥或 Worker 内部对象不会作为节点构造函数服务提供。

::: tip 这与 ASP.NET Core 的 `builder.Services` 不是同一个容器
`[FlowService]` 只负责上传类库在 Worker 中的服务。要在自己的 ASP.NET Core 程序中托管 SereinFlow，请看[宿主接入](/integration/hosting)。
:::
