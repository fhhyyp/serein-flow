# SereinFlow 节点类库开发指南

本文面向希望编写、打包、上传和升级 SereinFlow 节点类库的开发者。内容以仓库中的下列项目为准：

- [`src/SereinFlow.Library/SereinFlow.Library.csproj`](../../src/SereinFlow.Library/SereinFlow.Library.csproj)：独立的节点类库 SDK；
- [`tests/SereinFlow.TestLibrary/SereinFlow.TestLibrary.csproj`](../../tests/SereinFlow.TestLibrary/SereinFlow.TestLibrary.csproj)：覆盖普通节点、异步触发、参数转换、分支、枚举、消息和构造函数注入的示例/测试类库；
- [`src/SereinFlow.OpenCvLibrary/SereinFlow.OpenCvLibrary.csproj`](../../src/SereinFlow.OpenCvLibrary/SereinFlow.OpenCvLibrary.csproj)：使用 OpenCvSharp、Native 运行时文件、非 JSON 结果转换器和流程工件的图像处理类库。

## 1. 总体模型

节点类库不是 API 项目，也不是流程引擎项目。类库只引用 `SereinFlow.Library`，通过稳定的公共契约描述节点；API 在上传时读取这些契约，Worker 在流程运行时加载并执行实际程序集。

```text
节点类库源码
    │  FlowLibrary / FlowNode / NodeParam 等标注
    ▼
ZIP 上传包 ──► API 以 PE 元数据扫描节点、参数、枚举和兼容性 Manifest
    │
    ▼
项目引用类库制品 ──► 创建节点模板、保存流程契约
    │
    ▼
Worker 运行 ──► 解压到本次运行目录、隔离加载程序集、注入受限 SDK 服务、反射调用节点
    │
    ├─ 原始返回值：在当前 Worker 内供下游节点使用
    └─ TransferOutputs：事件、调试、API 或 MCP 对外展示
```

上传扫描阶段不会执行用户代码，也不会把上传 DLL 加载到 API 进程。执行阶段使用可回收的 `AssemblyLoadContext`；一次 Worker 运行结束后，程序集、Native 加载器、消息 Broker 和运行级工件一起释放。

## 2. 创建类库项目和依赖

生产类库可引用已发布的 SDK 包：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SereinFlow.Library" Version="1.1.0" />
  </ItemGroup>
</Project>
```

仓库内的 `TestLibrary` 和 `OpenCvLibrary` 为便于联调，使用对 `SereinFlow.Library.csproj` 的 `ProjectReference`。新类库应使用下列命名空间中的 SDK 契约：

```csharp
using SereinFlow.Core.Api;             // FlowLibrary、FlowNode、NodeParam、NodeResult 等
using SereinFlow.Library;              // 消息、工件、Native、服务生命周期
using SereinFlow.Runtime.Abstractions; // IFlowContext（类型属于 SDK 程序集）
```

`SereinFlow.Contracts` 仍保留类型转发以支持旧二进制，但新类库应直接引用 `SereinFlow.Library`，不要在类库中复制旧的 `DynamicFlow`、`NodeAction` 或自定义同名特性。

## 3. 节点容器、节点标注和稳定身份

### 3.1 `FlowLibraryAttribute`

把类标记为节点容器：

```csharp
[FlowLibrary("生产线设备与质量数据示例库")]
public sealed class 生产线节点
{
    // [FlowNode] 方法
}
```

- 特性只能标记类，类库名称可通过构造函数显式指定。
- 不传名称时使用被标记的 CLR 类名，例如 `未命名类库节点`。
- 类库名称会参与未显式指定节点 ID 时的契约 ID 推导；对外发布的类库应尽早确定名称。
- 同一个程序集可以有多个 `[FlowLibrary]` 类，每个类中的 `[FlowNode]` 方法都会被扫描。

### 3.2 `FlowNodeAttribute`

```csharp
[FlowNode(
    Id = "quality.pass-rate",
    NodeType = NodeType.Action,
    AnotherName = "计算合格率",
    Desc = "根据合格数量和检测总数计算本批次合格率。")]
public decimal Calculate(int passCount, int totalCount) => ...;
```

属性含义如下：

| 属性 | 作用 |
| --- | --- |
| `Id` | 稳定的节点契约 ID。推荐对所有正式节点显式设置；同一上传包内必须唯一。 |
| `NodeType` | `Action` 或 `Flipflop`。默认是 `Action`。外部节点类库目前只支持这两种类型。 |
| `AnotherName` | 编辑器、目录和节点模板使用的显示名称。 |
| `Desc` | 节点说明。 |

未设置 `Id` 时，扫描器使用“类库显示名称 + CLR 方法名”推导契约 ID，例如：

```text
生产线设备与质量数据示例库.计算合格率
```

推导 ID 依赖类库名称和方法名。它适合示例代码，但公共类库最好使用不随重命名变化的显式 ID。节点契约 ID 重复会使上传失败；同名重载也不建议用于节点，最好使用不同的方法名。

### 3.3 `NodeParamAttribute`

```csharp
public string BuildCommand(
    [NodeParam(Id = "device-id", Name = "设备编号")] string deviceId,
    [NodeParam(Name = "保留小数位")] int digits = 2,
    [NodeParam(Name = "工程单位")] string unit = "毫米") => ...;
```

| 属性 | 作用 |
| --- | --- |
| `Id` | 参数稳定契约 ID；不设置时使用 CLR 参数名。 |
| `Aliases` | 参数重命名后接受的旧 ID，例如 `Aliases = ["pass-count"]`。 |
| `Name` | 用户看到的参数名称。 |
| `IsExplicit` | 是否要求用户/流程显式提供。默认 `true`；若为 `false`，参数在目录中可作为非必填输入。 |

参数是否真正能够省略，还取决于 CLR 方法是否有默认值或是否可为空。通常应同时写 CLR 默认值和 `IsExplicit = false`：

```csharp
[NodeParam(Name = "图像宽度", IsExplicit = false)] int width = 640
```

扫描器还会读取方法参数的默认值，显示到节点模板和目录中；默认值可以来自数字、布尔、字符串、`decimal`、`DateTime` 以及枚举参数的默认值。

## 4. 节点方法支持的签名和执行方式

### 4.1 输入参数

普通参数会成为节点输入。`IFlowContext` 是特殊的隐藏参数：它不会出现在节点目录、流程编辑器或节点输入审计中，由 Worker 自动注入。它可以出现在普通参数前后：

```csharp
[FlowNode]
public string Describe(
    [NodeParam(Name = "设备状态")] string state,
    IFlowContext context)
{
    return state;
}
```

`IFlowContext` 只提供受限的节点上下文，不提供任意流程数据、环境变量、密钥、API 仓储或 `IServiceProvider`。

### 4.2 返回值

- 同步返回值会作为名为 `result` 的数据输出；`void` 不产生数据输出。
- `Task` / `Task<T>` 会由 Worker 等待；`Task<T>` 的 `T` 作为最终结果。
- `Flipflop` 节点必须返回 `Task` 或 `Task<T>`，上传扫描阶段就会拒绝其他返回类型。
- 当前类库执行器识别的是 `Task` / `Task<T>`；开发时不要把 `ValueTask` 当作可等待节点契约。
- 当节点只有一个输出时，流程引擎还会写入通用的数据端口 `data-out`，便于连接下游节点。

`TestLibrary` 中的 `等待设备触发` 和 `监听设备配置变更` 展示了 `Flipflop` 与 `Task<T>`；`记录工序结果` 展示了无输出的 `void` 节点。

### 4.3 `Action` 与 `Flipflop`

`Action` 在流程到达时调用一次。`Flipflop` 表示异步等待/触发节点：

- 有执行输入时，在流程到达它时等待一次并根据结果继续；
- 没有执行输入时，流程引擎把它视为全局监听器，完成一次等待后调度选中分支的下游，然后继续等待下一次触发；
- 等待、订阅和设备轮询必须使用 `IFlowContext.CancellationToken`，否则取消流程或 Worker 关闭时可能无法及时结束。

### 4.4 静态方法和实例方法

无依赖的节点可以使用静态方法；需要构造函数注入、`IDisposable` 或状态封装时使用实例类。Worker 每次节点调用都会创建新的节点容器实例，并在调用结束时释放 `IDisposable`/`IAsyncDisposable` 实例；不要把节点实例字段当成跨调用缓存。

## 5. 输入转换、可变参数和枚举

流程输入可能来自字面量、项目输入、表达式或前置节点数据，Worker 会把它转换成方法声明的 CLR 类型。常用支持包括：

- `string`、布尔、整数、浮点、`decimal`、`Guid`、`char`；
- 可空值类型；
- 普通枚举和 `[Flags]` 枚举；
- 数组；
- 有合适 `TypeConverter` 的类型；
- 可通过 `System.Text.Json` 从对象/数组转换的 DTO 或记录类型；
- 在同一 Worker 内已经是目标类型的对象引用，例如 OpenCV 节点之间传递 `Mat`。

转换失败会产生 `node.input_invalid`；缺少不可为空且没有默认值的值类型输入会产生 `node.input_missing`。节点自身仍应对业务范围做校验，例如 `TestLibrary` 将检测总数限制为大于零。

### 5.1 `params` 可变参数

```csharp
[FlowNode(AnotherName = "汇总多个检测值")]
public int Sum([NodeParam(Name = "检测值")] params int[] values)
    => values.Sum();
```

上传目录会把 `params int[]` 记录为可变参数，并公开元素类型 `System.Int32`。运行时支持两种输入形式：

1. `expanded`：编辑器产生多个“检测值”输入，空的可选占位会跳过；
2. `collection`：一个数组/集合输入，例如 `[2, 3, 5]`。

这是运行时对 `params` 的支持，不需要额外的 SDK 特性。

### 5.2 普通枚举与 `Flags`

直接把枚举作为参数即可：

```csharp
public string SetMode(
    [NodeParam(Name = "运行模式")] DeviceMode mode = DeviceMode.Automatic) => ...;

[Flags]
public enum DevicePermission : ulong
{
    None = 0,
    ReadStatus = 1,
    WriteParameter = 2,
    Diagnose = 4,
    All = 7
}
```

扫描器从 PE 元数据读取枚举成员名称、底层类型、数字值和 `[Flags]` 标志，不加载用户代码。编辑器因此可以生成普通枚举下拉和 `Flags` 多选；`ulong` 等无符号值会以字符串保存，避免 JSON/JavaScript 精度丢失。`TestLibrary` 的 `设备运行模式`、`设备操作权限`、`生成设备配置摘要`和`监听设备配置变更`覆盖了这些场景。

## 6. 分支控制：`IFlowContext`

节点默认走 `Success` 分支。需要把业务判定映射到不同执行出口时，使用上下文：

```csharp
[FlowNode(AnotherName = "按设备状态选择分支")]
public string Choose([NodeParam(Name = "设备状态")] string state, IFlowContext context)
{
    if (state == "故障")
    {
        context.SelectError("device.faulted", "设备发生故障。");
        return "设备故障";
    }

    if (state != "就绪")
        context.SelectFailure("device.not_ready", "设备未就绪。");

    return state;
}
```

可用方法：

- `SelectSuccess()`：恢复/选择成功分支，并清除分支代码和消息；
- `SelectFailure(code, message)`：选择失败分支；省略代码时使用 `node.branch_failure`；
- `SelectError(code, message)`：选择错误分支；省略代码时使用 `node.branch_error`。

分支选择会改变流程引擎选择的执行连接，但不会自动抛异常。未处理异常通常进入错误结果；需要表示可预期的业务失败时，优先显式选择 `Failure` 并提供稳定错误码。

## 7. 非 JSON 返回值：`NodeResult` 转换器

相机句柄、OpenCV `Mat`、串口句柄或 HTTP 客户端对象不应直接序列化成 API JSON。为节点加结果转换器：

```csharp
public sealed class MatSummaryConverter : INodeResultConverter<Mat, object>
{
    public object Transfer(Mat value) => new
    {
        type = "opencv-mat",
        width = value.Width,
        height = value.Height,
        channels = value.Channels()
    };
}

[FlowNode(AnotherName = "读取图像")]
[NodeResult<MatSummaryConverter>]
public Mat ReadImage(byte[] imageBytes) => ...;
```

也可以使用旧 C# 版本可用的非泛型写法：

```csharp
[NodeResultAttribute(typeof(MatSummaryConverter))]
```

执行语义：

- `INodeResultConverter<TPrimitive, TTransfer>` 必须是类库程序集中的具体、非泛型类，并且恰好实现一个闭合转换器契约；
- Worker 自动发现并以瞬时服务注册转换器；
- `TPrimitive` 必须与节点实际返回值匹配；
- `TTransfer` 应是 JSON 安全的对象、数组、标量或 DTO；
- 原始 `TPrimitive` 仍保留在当前 Worker 中，后继节点拿到原始对象，而事件、调试结果、API/MCP 输出使用转换后的 `TTransfer`；
- 二进制内容应使用下一节的流程工件，而不是塞进 `TTransfer`。

OpenCV 类库的 `MatConverter`只返回尺寸、通道、深度和空图像标志等摘要，因此不会把 Mat 缓冲区序列化到事件中。

## 8. 图像和文件：`IFlowWorkpiece`

`IFlowWorkpiece` 是一次运行范围内的非 JSON 数据存储服务：

```csharp
public sealed class ImageNodes(IFlowWorkpiece workpiece)
{
    public FlowWorkpieceInfo Save(byte[] png)
        => workpiece.UploadImage("inspection.png", png, FlowWorkpieceContentTypes.Png);

    public FlowWorkpieceInfo SaveReport(Stream report)
        => workpiece.UploadFile("report.json", report, FlowWorkpieceContentTypes.Json);

    public FlowWorkpieceInfo SaveStep(IFlowContext context, byte[] content)
        => workpiece.UploadNodeOutput(
            context, "result.json", content, FlowWorkpieceContentTypes.Json);
}
```

SDK 提供 `FlowWorkpieceContentTypes.Png` 和
`FlowWorkpieceContentTypes.Json` 两个编译期常量，节点库无需重复手写
`"image/png"` 和 `"application/json"`，可避免 MIME 类型字面量写错。

可用操作：

- `UploadImage`：字节数组、流或 Base64/data URI；
- `UploadFile`：字节数组或流；
- `UploadNodeOutput`：字节数组或流，并把 `IFlowContext` 中的节点 ID 和执行步骤 ID 写入工件元数据；
- 返回的 `FlowWorkpieceInfo` 含稳定 ID、类型（`Image`/`File`）、名称、MIME 类型、长度、创建时间、节点 ID 和执行步骤 ID。

工件属于当前流程运行，API 和 MCP 可以列出/下载。`UploadNodeOutput` 让调试面板在选择执行步骤后自动定位该步骤产生的第一个工件，同时仍保留本次运行的全部工件。传入的流由调用方持有，SDK 不会接管或关闭它；文件名应是单一文件名，不要传路径。

OpenCV 类库的每个处理节点都把结果编码为 PNG 后调用 `UploadNodeOutput(context, ...)`，工件会自动记录本次执行步骤 ID，再把原始 `Mat` 返回给下游，因此同时具备“可视化工件”和“进程内数据连接”。

## 9. 构造函数依赖注入与服务生命周期

### 9.1 节点可直接使用的 SDK 服务

Worker 为每个上传类库的受限容器预注册：

- `IMessageService`：运行级消息队列/事件总线；
- `IFlowWorkpiece`：运行级工件服务；
- `IFlowNativeLibraryLoader`：当前类库包的 Native 加载器。

```csharp
[FlowLibrary("设备类库")]
public sealed class DeviceNodes(
    IMessageService messages,
    IFlowWorkpiece workpiece)
{
    [FlowNode(Id = "device.read")]
    public string Read() => "ok";
}
```

### 9.2 用 `FlowService` 声明类库自己的依赖

只有同一上传程序集中的具体、闭合类可以通过 `FlowService` 注册：

```csharp
public interface IDeviceChannel
{
    string Read();
}

[FlowService<IDeviceChannel>]
public sealed class DeviceChannel : IDeviceChannel
{
    public string Read() => "ok";
}

[FlowLibrary("设备类库")]
public sealed class DeviceNodes(IDeviceChannel channel)
{
    [FlowNode(Id = "device.read")]
    public string Read() => channel.Read();
}
```

不支持泛型开放类型、抽象类、多实现冲突或同一契约的重复注册。服务必须恰好有一个公共构造函数；实现类和暴露的接口/基类必须匹配。旧 C# 版本使用 `[FlowService(typeof(IDeviceChannel))]`。

生命周期：

| 生命周期 | 语义 |
| --- | --- |
| `Run`（默认） | 一次 Worker 运行中，同一上传程序集共享一个服务实例。 |
| `Invocation` | 一次节点调用内共享一个实例；下一次节点调用重新创建。 |
| `Transient` | 每次从容器解析都创建新实例。 |

指定接口契约时，具体类型和接口在 `Run`/`Invocation` 下解析到同一个实例。服务容器按上传程序集隔离，类库之间不能互相解析 `FlowService`。节点容器实例本身仍然是每次调用新建的。

以下类型明确禁止通过构造函数注入：`IServiceProvider`、`IServiceScopeFactory`、`IServiceProviderIsService`、`IKeyedServiceProvider`、`IFlowContext` 以及 API 仓储、配置、密钥和 Worker 协议对象。`IFlowContext` 必须写在节点方法参数中。

`TestLibrary/ConstructorInjectionNodes.cs` 用 `RunMarker`、`InvocationMarker` 和 `TransientMarker`验证三种生命周期；`ForbiddenProviderNode` 用于验证被禁止的 `IServiceProvider` 注入会以 `library.service_dependency_forbidden` 失败。

## 10. 运行级消息：队列、事件总线和外部入口

节点通过注入的 `IMessageService` 创建消息视图：

```csharp
public sealed class MessageNodes(IMessageService messages)
{
    [FlowNode(
        Id = "device.message.receive",
        NodeType = NodeType.Flipflop,
        AnotherName = "接收设备消息")]
    public async Task<string> Receive(IFlowContext context)
    {
        var queue = messages.CreateMessageQueue(new MessageChannelOptions
        {
            SerializationMode = MessageSerializationMode.Json,
            Capacity = 8,
            ExternalIngress = true,
            ContractId = "device.text.v1"
        });

        return await queue.ReceiveAsync<string>(
            "device.inbox", context.CancellationToken);
    }
}
```

两种模型：

- `IMessageQueue`：按 topic 的有界 FIFO 竞争消费队列；多个消费者共享队列，一条消息只交付给一个消费者；
- `IEventBus`：广播模型；每个订阅者拥有独立缓冲区，发布时已存在的订阅者各收到一份，晚订阅者不收到历史事件。

`MessageChannelOptions` 可配置：

- `SerializationMode`：`Json`（默认，推荐跨类库协作）或 `DirectObject`（仅同一 Worker 内的兼容 CLR 对象）；
- `Capacity`：每个 topic/订阅的缓冲容量；
- `OverflowStrategy`：`Reject`、`DropOldest`、`DropNewest`；
- `MessageTtl`：消息有效期；
- `MaxPayloadBytes`：JSON 载荷上限；
- `ExternalIngress`：是否向 Supervisor/API 开放外部投递，默认关闭；
- `ContractId`：外部入口必须匹配的服务端控制契约 ID。

同一个 topic 每次创建视图时的选项必须一致。外部入口必须显式 `ExternalIngress = true`，且必须使用 JSON；外部 API/MCP 只能投递 JSON，不允许请求指定程序集限定类型。每个发送、接收和订阅都应传入取消令牌；事件订阅应使用 `await using` 释放。

`TestLibrary/MessageNodes` 展示了：

- `ReceiveExternal`：外部 JSON 队列入口；
- `ReceiveExternalEvent`：外部 JSON EventBus 入口；
- `ProcessExternal`：接收 Flipflop 的 `data-out` 并继续执行的 Action。

消息服务只在当前 Worker 运行内存中存在，不提供跨运行持久化、重放、消费确认或运行结束恢复。HTTP `202 Accepted` 只表示消息进入 Worker Broker，不表示下游流程或业务副作用已经完成；应通过运行事件、输出和最终状态确认结果。详细的 HTTP/MCP 投递协议见[Worker 消息服务](worker-message-service.md)。

## 11. Native 依赖和 OpenCV 类库

### 11.1 声明 Native 目录

固定依赖可在程序集或节点类上声明：

```csharp
using SereinFlow.Core.Api;

[assembly: NativeLibraryDirectory(
    "runtimes/{rid}/native",
    Recursive = false,
    Required = true)]
```

`{rid}` 会替换为 Worker 当前运行时标识，例如 `win-x64`。`Recursive` 默认 `true`，`Required` 默认 `true`。必需目录不存在、为空或其中 Native 文件加载失败时，运行会产生带机器可读代码和相对路径的 `FlowNativeLibraryException`。

需要按条件加载的依赖可注入 `IFlowNativeLibraryLoader`：

```csharp
public DeviceNodes(IFlowNativeLibraryLoader native)
{
    native.LoadNativeLibraryDirectory(
        "vendor/native/{rid}", recursive: true, required: true);
}
```

也可以调用 `LoadNativeLibrary("vendor/native/{rid}/device.dll")`。路径必须是包内相对路径，不能是绝对路径，不能含 `..`、驱动器片段或跳出包根目录。加载器按当前类库包和 Worker 运行范围工作，并且是幂等的；不要把 x86、x64 等不同架构目录同时加载。

### 11.2 OpenCV 示例

`SereinFlow.OpenCvLibrary` 的依赖是：

```xml
<ProjectReference Include="..\SereinFlow.Library\SereinFlow.Library.csproj" />
<PackageReference Include="OpenCvSharp4" Version="4.11.0.20250507" />
<PackageReference Include="OpenCvSharp4.runtime.win"
                  Version="4.11.0.20250507"
                  PrivateAssets="all" />
```

它在 `NativeLibraryManifest.cs` 中声明 `runtimes/{rid}/native` 为必需、非递归目录。Worker 加载程序集时先处理程序集级声明；解析到节点类型时再处理类级声明。OpenCV 节点在第一次实例化前加载当前 RID 对应的 `OpenCvSharpExtern`，不会把 x86/x64 一起加载。

当前 OpenCV 节点包括：

- 生成示例图像、读取 PNG/JPEG 等编码图像；
- 灰度化、二值化、反色；
- 膨胀、腐蚀、开运算、闭运算；
- 高斯模糊、Canny 边缘检测；
- 缩放图像。

每个节点都返回 `Mat`，使用 `MatConverter` 输出 JSON 摘要，并通过 `IFlowWorkpiece.UploadNodeOutput`上传 PNG。下游 OpenCV 节点拿到的仍是原始 `Mat`，不是摘要对象。

## 12. 流程引擎如何使用这些特性

### 12.1 上传和目录阶段

API 会读取 ZIP 内与类库同名的主程序集，例如 `SereinFlow.TestLibrary.dll`，并用 PE metadata scanner 解析：

- `[FlowLibrary]` 类和显示名称；
- `[FlowNode]` 的节点类型、契约 ID、显示名、描述和返回类型；
- `[NodeParam]` 的参数 ID、名称、别名、显式输入标志；
- `params` 的可变参数和元素类型；
- CLR 默认值；
- 普通枚举和 `[Flags]` 枚举元数据；
- 是否存在 `NodeResult` 转换器；
- 兼容性 Manifest 和工件哈希。

目录数据可以直接用于项目节点模板和编辑器，不需要在 API 进程执行类库代码。类库引用到的其他 managed DLL 仍需随包提供，否则 Worker 运行时的 `AssemblyDependencyResolver` 无法加载它们。

### 12.2 节点模板和流程保存

项目引用一个类库制品后，编辑器/MCP 可以按节点契约创建模板。模板保存类库制品 ID、版本、哈希、节点契约 ID、类名、方法名和参数契约。流程数据连接可以把字面量、项目输入、表达式或前置节点输出绑定到这些参数。

只有 `Action` 和 `Flipflop` 类库契约能创建类库节点模板；`Script`、`FlowCall` 是引擎自身的节点类型，不由 `SereinFlow.Library` 的 `[FlowNode]` 产生。

### 12.3 Worker 执行阶段

运行时会：

1. 校验流程引用的类库是否在本次运行允许的类库列表中；
2. 从受控包根目录读取 `{libraryId}.zip`，解压到本次运行专用目录；
3. 使用可回收加载上下文和依赖解析器加载主程序集；
4. 初始化 Native 目录声明、运行级消息服务、运行级工件服务和类库隔离 DI 容器；
5. 每次节点调用创建调用作用域和节点实例；
6. 隐藏注入 `IFlowContext`，转换普通输入，调用同步/异步方法；
7. 保存原始输出；如声明了 `NodeResult`，额外生成传输输出；
8. 按 `Success`、`Failure` 或 `Error` 分支连接继续执行，并在调用结束时释放节点实例和作用域。

主程序集、包 ID、类名和方法名必须能在上传制品中唯一解析。相同 CLR 方法名的重载可能导致 `library.method_ambiguous`，正式类库应避免把重载方法都标记成节点。

## 13. 打包、上传和依赖布局

### 13.1 ZIP 命名和目录

上传文件名必须是：

```text
{library-name}-{major.minor.patch}[前置/元数据].zip
```

当前类库包内的根目录应与文件名主体一致，例如：

```text
SereinFlow.OpenCvLibrary-1.0.3/
├── SereinFlow.OpenCvLibrary.dll
├── SereinFlow.Library.dll
├── SereinFlow.OpenCvLibrary.deps.json
├── SereinFlow.OpenCvLibrary.runtimeconfig.json
└── runtimes/
    └── win-x64/native/OpenCvSharpExtern.dll
```

主 DLL 必须位于根目录下的 `{library-name}.dll`，程序集名称也必须匹配类库名称。包内路径必须安全、不能重复、不能包含源代码、构建文件、脚本、压缩包或可执行文件；不能把发布目录扁平化，否则依赖解析或 Native 路径会失效。

`TestLibrary` 本身只依赖 `SereinFlow.Library`，其发布包没有 `runtimes` Native 目录；上面的 Native 示例对应 `OpenCvLibrary`。如果其他类库没有 Native 依赖，也不需要创建该目录。

### 13.2 仓库示例的构建方式

`TestLibrary` 的 `AfterTargets="Publish"` 会保留完整发布布局并创建：

```powershell
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
```

产物为 `artifacts\libraries\SereinFlow.TestLibrary-1.6.1.zip`。`OpenCvLibrary` 当前的 MSBuild `CreatePackage` 目标在 Build 后创建项目目录下的 `SereinFlow.OpenCvLibrary-1.0.3.zip`；它依赖 OpenCvSharp 的 managed DLL 和 `runtimes/{rid}/native` 资产。发布/打包后应检查 ZIP，而不是只检查 `bin` 中的主 DLL。

## 14. 版本兼容和升级建议

节点契约由节点 ID、参数 ID/别名、CLR 类型、返回类型、可等待性和节点类型共同决定。为了让旧流程安全升级：

- 正式节点显式设置稳定 `FlowNode.Id`；
- 正式参数显式设置稳定 `NodeParam.Id`；
- 有意重命名参数时保留旧 ID 到 `Aliases`；
- 不要改变既有节点的 `NodeType`、`Task`/同步性质、返回类型、参数 CLR 类型或 `params` 形态；
- 新增参数应优先提供 CLR 默认值，新增没有默认值的必需参数会要求流程重新连线；
- 删除节点或已有参数属于破坏性变化；
- 修改显示名称、描述、可选性或默认值通常是兼容元数据变化，但仍应在升级预览中确认；
- 不要随意修改 `FlowLibrary` 显示名称或 CLR 方法名，除非使用显式契约 ID 并确认升级映射。

上传类库时会生成不可变兼容性 Manifest。引擎可在不执行目标程序集的情况下比较两个制品，缺失或不明确的契约会保守地阻止静默重绑定。版本号变化本身不会让节点契约变化；稳定 ID 和参数别名才是升级依据。

## 15. 以仓库示例作为验收清单

### `SereinFlow.TestLibrary`

可用来验收：

- 数值、布尔、字符串、可空字符串和结构化 record 输出；
- `void`、同步返回值、`Task<T>` 和全局 `Flipflop`；
- 普通输入、默认参数和 `IsExplicit = false`；
- `IFlowContext` 隐藏注入与 Success/Failure/Error 分支；
- `params int[]` 的 expanded/collection 两种输入；
- 普通枚举、`[Flags]` 枚举、`ulong` 底层类型、多选和默认值；
- `IMessageService` 的 Queue/EventBus 外部 JSON 入口以及后继 Action；
- `FlowServiceLifetime.Run`、`Invocation`、`Transient`；
- 被禁止的 `IServiceProvider` 构造函数注入。

### `SereinFlow.OpenCvLibrary`

可用来验收：

- 第三方 managed 依赖和 RID-specific Native 文件；
- 程序集级 `NativeLibraryDirectory`；
- `Mat` 这种不适合直接 JSON 的进程内结果；
- `INodeResultConverter<Mat, object>`；
- `IFlowWorkpiece.UploadNodeOutput`、MIME 类型和节点工件关联；
- 原始对象在下游节点继续传递，同时事件/API 使用摘要输出。

推荐验证命令：

```powershell
dotnet build src\SereinFlow.OpenCvLibrary\SereinFlow.OpenCvLibrary.csproj -c Release
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
dotnet test SereinFlow.sln --no-build
```

## 16. 常见错误

| 现象 | 典型原因 |
| --- | --- |
| 上传时找不到节点 | 类没有 `[FlowLibrary]`，方法没有 `[FlowNode]`，或使用了 SDK 外的同名特性。 |
| 节点契约重复 | 两个节点的显式 `Id` 重复，或同一类库名/方法名推导出相同 ID。 |
| 参数无法绑定 | 参数名未保留且没有 `NodeParam.Id`，输入使用了错误的 ID/别名，或 CLR 类型无法转换。 |
| `Flipflop` 上传失败 | 方法返回了同步类型、`ValueTask` 或其他非 `Task` 类型。 |
| `library.service_dependency_forbidden` | 构造函数请求 `IServiceProvider`、`IFlowContext` 等被禁止对象。 |
| `library.result_converter_invalid` | 转换器不是当前类库程序集中的具体类，或实现了多个/开放的转换器契约。 |
| Native 加载失败 | 没有随 ZIP 提供当前 RID 的文件、路径包含 `..`、目录声明错误，或同时混放多个架构。 |
| 工件无法在调试器中定位 | 使用了 `UploadFile` 而不是 `UploadNodeOutput`，或没有把 `IFlowContext` 传给 `UploadNodeOutput`。 |
| 消息入口未唤醒 | 未设置 `ExternalIngress`、`ContractId` 不匹配、topic/通道类型不匹配，或 Worker 已结束。 |
| 下游拿到的是摘要而不是原对象 | 误把 `TransferOutputs` 当作流程数据；`NodeResult` 只改变对外投影，正常数据连接仍使用原始返回值。 |
