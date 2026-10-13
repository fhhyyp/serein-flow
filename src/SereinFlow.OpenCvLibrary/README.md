# SereinFlow OpenCV 图像处理类库

`SereinFlow.OpenCvLibrary` provides ordinary Action nodes backed by
OpenCvSharp. No node is a trigger or waits for an external event.

每个图像处理节点都返回进程内的 `OpenCvSharp.Mat`，并使用
`[NodeResult<MatConverter>]` 标记结果转换器。转换器只输出尺寸、通道和像素深度等
JSON 安全摘要；节点在成功返回前通过注入的 `IFlowWorkpiece` 上传一次 PNG 工件，
供运行控制台、调试会话和 MCP 查阅或下载。上传时直接传入 `IFlowContext`，由运行时保存
节点 ID 和执行步骤 ID；“流程工件”仍会显示本次运行的全部工件，在调试面板中点击执行步骤后，
会自动选中该步骤产生的第一个工件；没有匹配工件时不会用其他执行步骤的工件替代。
会自动选中该步骤产生的第一个工件；没有匹配工件时不会用其他执行步骤的工件替代。

类库节点可按下面的方式上传任意节点输出文件（`IFlowContext` 是 Worker 自动注入的隐藏参数）：

```csharp
private readonly IFlowWorkpiece workpiece;

public MyNode(IFlowWorkpiece workpiece) => this.workpiece = workpiece;

public void Process(IFlowContext context)
{
    workpiece.UploadNodeOutput(context, "result.json", bytes, FlowWorkpieceContentTypes.Json);
}
```

Included nodes include:

- 生成示例图像 / 读取图像
- 灰度化 / 二值化
- 膨胀 / 腐蚀 / 开运算 / 闭运算
- 高斯模糊 / Canny 边缘检测
- 缩放图像 / 反色

## 运行级共享变量 / Run variables

从 1.0.5 起，每个节点在成功上传图像后更新同一次运行中的变量表。节点构造函数注入
`LibraryVariableService`，方法中的 `IFlowContext.Variables` 返回同一个实例。
后续节点通过 `Get<int>` 读取首张图像的尺寸，并通过 `TryGet<int>` 读取已有计数。
本类库使用 `lock (variables)` 同步跨节点的初始化和计数器读改写；变量表的单次读写本身也受锁保护。
此额外锁约定只同步采用相同约定的调用方，外部直接写入这些变量仍可能覆盖节点记录。

| 变量名 | CLR 类型 | 内容 |
| --- | --- | --- |
| `opencv.first.width` / `height` / `channels` | `System.Int32` | 本次运行首张成功输出图像的尺寸和通道数 |
| `opencv.processed.count` | `System.Int32` | 成功记录的图像处理次数，下一次运行从零开始 |
| `opencv.last.nodeId` / `fileName` | `System.String` | 最近成功记录图像的节点 ID 和工件名称 |
| `opencv.last.width` / `height` / `channels` | `System.Int32` | 最近图像的尺寸和通道数 |
| `opencv.last.scaleX` / `scaleY` | `System.Double` | 最近图像相对首张图像的宽高比例 |
| `opencv.last.image` | `OpenCvSharp.Mat` | 进程内原始图像引用，与下游节点收到的对象相同 |

变量名区分大小写，`Get<T>` / `TryGet<T>` 使用 CLR 强制转换或拆箱；类型不匹配会抛出异常。
图像引用无需序列化即可在 Worker 内共享；MCP 和前端仅显示 `Mat` 的类型信息，图像内容通过 PNG 工件查看。
原始变量表随 Worker 退出释放；退出前会保存一次有界 JSON 投影及类型信息作为最终快照。
通过 MCP 调用 `sereinflow_get_run_variables` 可以查询运行中的变量，也可在流程结束后查询保存的最终快照。
`Mat` 原始对象不会持久化，历史快照仅保留其 CLR 类型信息。

使用示例流程（生成 → 灰度 → 模糊 → 二值化 → 膨胀 → 腐蚀 → 开运算 → 闭运算 → Canny → 缩放 → 反色），
在缩放前暂停可看到计数 `9`、尺寸 `640×480`，在反色前暂停可看到计数 `10`、尺寸 `320×240`、
宽高比例均为 `0.5`。前端“流程变量”面板可在运行中持续刷新，MCP 查询同一张表。

Every successful image output updates the shared run table. Constructor injection and
`IFlowContext.Variables` share one instance. The OpenCV nodes synchronize their compound
initialization and counter updates, read the first image dimensions across node contexts,
and retain the latest `Mat` without serializing it. MCP and the frontend expose scalar values
and type metadata for `Mat`; PNG workpieces carry image content. Pause a debug run before
resize or inversion to inspect live values, or read the saved final projection after completion.
A new run starts with an empty table; original Mat objects are not persisted.

The package includes the OpenCvSharp managed assembly and Windows x64 native
runtime assets required by the Worker.

The assembly declares `runtimes/{rid}/native` as a required native dependency
directory. The Worker loads the matching OpenCvSharp native shim before the
first node instance is invoked; the declaration is run-scoped and does not
load the x86 and x64 directories together.
