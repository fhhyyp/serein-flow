# 节点参数与执行方式

写好第一个方法后，通常会遇到三个问题：画布上显示什么、参数从哪里来、节点什么时候继续往下走。

## 名称、说明和稳定标识

| 写在代码里的东西 | 用户会看到或用到什么 |
| --- | --- |
| `[FlowLibrary("设备工具")]` | 类库在节点目录中的名称；不写名称时取类名。 |
| `[FlowNode(AnotherName = "读取温度", Desc = "读取设备当前温度")]` | 节点的显示名称与说明。 |
| `[NodeParam(Name = "设备编号")] string deviceId` | 节点输入框的标签；参数 ID 默认使用 `deviceId`。 |
| `FlowNode.Id` / `NodeParam.Id` | 保存到流程里的稳定标识。通常让系统生成即可。 |

已发布的流程依赖这些标识。修改显示文字通常比修改标识安全。如果必须给参数改名，可以保留旧标识，或在新参数上用 `Aliases` 接受之前的 ID。

## 普通节点与等待节点

`NodeType.Action` 是默认方式：流程走到节点时立即调用方法。方法可以直接返回值，也可以返回 `Task<T>` 做异步工作。

`NodeType.Flipflop` 用来**等待一次外部触发**，例如等待队列里出现消息。它的方法应返回 `Task` 或 `Task<T>`，并把 `IFlowContext.CancellationToken` 交给等待操作。取消流程时，等待才能及时结束。

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

[FlowLibrary("设备消息")]
public sealed class DeviceNodes(IMessageService messages)
{
    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "等待设备消息")]
    public async Task<string> WaitForMessage(IFlowContext context)
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

`IFlowContext` 是**方法参数**，由当前节点调用提供，不能作为节点类的构造函数参数。它有本次运行的 `RunId`、当前 `NodeId`、这次执行步骤的 `ExecutionId` 和取消令牌。需要走特定分支时，可调用 `SelectSuccess()`、`SelectFailure(code, message)` 或 `SelectError(code, message)`。

## 返回复杂对象时

一个节点返回结果后，SereinFlow 会把它交给**当前运行中的下游节点**，也会把结果写到运行事件和 API 输出里。前者可以直接使用 C# 对象；后者需要能表示成 JSON。数字、文字和较小的普通数据对象通常可以直接返回。

如果结果是 OpenCV `Mat`、设备连接等不能直接表达成 JSON 的对象，或者虽然能转成 JSON、却包含很大的字节数组，就需要分开处理：

| 结果要去哪里 | 放什么 |
| --- | --- |
| 下游节点的数据连接 | 原始 C# 对象，供这次 Worker 运行继续使用。 |
| 运行事件、调试结果和 API 的 `outputs` | 一份简短、能写成 JSON 的说明。 |
| 用户要查看或下载完整文件 | 用 `IFlowWorkpiece.UploadNodeOutput` 保存文件。 |

`[NodeResult<转换器类型>]` 负责第二行。它只改变**对外展示的结果**，不会把下游数据连接里的原对象替换掉。文件输出则是另一件事：转换器不会自动保存文件。

### 例子：报告继续传给下游，同时对外展示摘要

下面的 `ReportFile` 包含完整文件字节。若直接把它写进运行事件，会产生一大段没有必要的内容。我们用转换器只公开文件名和字节数，并用内置服务保存一份可下载的文件：

```csharp
using System.Text;
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

public sealed record ReportFile(string FileName, byte[] Content);

public sealed class ReportSummaryConverter : INodeResultConverter<ReportFile, object>
{
    public object Transfer(ReportFile report) => new
    {
        name = report.FileName,
        bytes = report.Content.Length
    };
}

[FlowLibrary("报告工具")]
public sealed class ReportNodes(IFlowWorkpiece workpieces)
{
    [FlowNode(AnotherName = "生成报告")]
    [NodeResult<ReportSummaryConverter>]
    public ReportFile Create(IFlowContext context)
    {
        var report = new ReportFile(
            "report.txt", Encoding.UTF8.GetBytes("检查完成"));

        workpieces.UploadNodeOutput(
            context, report.FileName, report.Content,
            FlowWorkpieceContentTypes.Text);

        return report;
    }

    [FlowNode(AnotherName = "读取报告大小")]
    public int GetSize([NodeParam(Name = "报告")] ReportFile report)
        => report.Content.Length;
}
```

把“生成报告”的数据输出连到“读取报告大小”的“报告”输入后，`GetSize` 收到的是完整的 `ReportFile`。运行事件与 `GET /api/runs/{runId}/outputs` 中的 `outputs.result` 则是类似 `{"name":"report.txt","bytes":12}` 的摘要。要拿到完整内容，查看这次运行的[工件列表并下载文件](/api/runs)。原始对象只在当前 Worker 运行中传递；需要在运行后取回内容，应保存为工件。

### 写转换器时检查这几项

- `INodeResultConverter<TPrimitive, TTransfer>` 中的 `TPrimitive` 要能接收节点实际返回的对象。上例返回 `ReportFile`，因此转换器也接收 `ReportFile`。
- `Transfer` 返回的 `TTransfer` 要适合 JSON：例如数字、文字、数组或只含这些值的小对象。不要在摘要里放文件字节、连接句柄或循环引用的对象。
- 转换器必须是**同一上传类库程序集**里的具体类，且只实现一个确定类型的 `INodeResultConverter<,>`。Worker 会自动注册它；无需再加 `[FlowService]`。
- 节点方法完成后才会执行转换。转换器若抛异常，节点会得到转换失败的错误；可以从运行事件的错误码和消息排查。

本仓库的 `SereinFlow.OpenCvLibrary` 也是这个做法：节点返回原始 `Mat` 给后面的图像节点，`MatConverter` 只向外输出宽、高、通道数等摘要，同时把 PNG 用 `UploadNodeOutput` 保存。它用到的本机依赖可参照[native DLL 加载](/library/native-dll)。
