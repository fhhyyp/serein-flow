# SereinFlow OpenCV 图像处理类库

`SereinFlow.OpenCvLibrary` provides ordinary Action nodes backed by
OpenCvSharp. No node is a trigger or waits for an external event.

每个图像处理节点都返回进程内的 `OpenCvSharp.Mat`，并使用
`[NodeResult<MatConverter>]` 标记结果转换器。转换器只输出尺寸、通道和像素深度等
JSON 安全摘要；节点在成功返回前通过注入的 `IFlowWorkpiece` 上传一次 PNG 工件，
供运行控制台、调试会话和 MCP 查阅或下载。上传时通过 `IFlowContext.NodeId`
调用 `IFlowWorkpiece.UploadNodeOutput(...)` 保存节点绑定；“流程工件”仍会显示本次运行的全部工件，
在调试面板中点击执行步骤后，会自动选中该节点最新生成的工件（没有匹配工件时保持当前选择）。

类库节点可按下面的方式上传任意节点输出文件（`IFlowContext` 是 Worker 自动注入的隐藏参数）：

```csharp
private readonly IFlowWorkpiece workpiece;

public MyNode(IFlowWorkpiece workpiece) => this.workpiece = workpiece;

public void Process(IFlowContext context)
{
    workpiece.UploadNodeOutput(context.NodeId, "result.json", bytes, "application/json");
}
```

Included nodes include:

- 生成示例图像 / 读取图像
- 灰度化 / 二值化
- 膨胀 / 腐蚀 / 开运算 / 闭运算
- 高斯模糊 / Canny 边缘检测
- 缩放图像 / 反色

The package includes the OpenCvSharp managed assembly and Windows x64 native
runtime assets required by the Worker.

The assembly declares `runtimes/{rid}/native` as a required native dependency
directory. The Worker loads the matching OpenCvSharp native shim before the
first node instance is invoked; the declaration is run-scoped and does not
load the x86 and x64 directories together.
