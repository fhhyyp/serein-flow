# SereinFlow 测试类库

这是一个面向生产线自动化流程的示例类库，用于验证 SereinFlow 的“上传类库”、节点目录、参数转换和 Worker 节点执行能力。类库中的业务名称、参数名称和说明均使用中文，便于直接作为新项目的本地测试类库。

## 构建上传包

在仓库根目录执行：

```powershell
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
```

构建完成后，上传包会生成到：

```text
artifacts\libraries\SereinFlow.TestLibrary-1.6.1.zip
```

压缩包结构以发布目录为准，显式包含主程序集、托管依赖、运行时配置和原生运行时文件；相对目录不能扁平化：

```text
SereinFlow.TestLibrary-1.6.1\
├── SereinFlow.TestLibrary.dll
├── SereinFlow.Library.dll
├── SereinFlow.TestLibrary.deps.json
├── SereinFlow.TestLibrary.runtimeconfig.json
└── runtimes\<rid>\native\<native-library>
```

其中包含 11 个面向生产线业务的节点，覆盖数值、布尔、可变参数、流程上下文、结构化输出、普通枚举和 Flags 枚举：

- `计算合格率`：Action，接收合格数量和检测总数，返回 `System.Decimal`。
- `构建设备写入指令`：Action，接收设备编号、点位名称和写入值，保留小数位为可选参数，返回 `System.String`。
- `记录工序结果`：Action，接收工单、工序、合格状态和可选备注，返回 `System.Void`。
- `等待设备触发`：Flipflop，接收设备编号和可选轮询间隔（50 至 3,600,000 毫秒），返回 `Task<System.Boolean>`。它模拟设备触发等待；作为全局 Flipflop 时，完成后会调度下游，并继续等待下一次触发。
- `汇总批次质量`：Action，返回 `批次质量结果`，用于覆盖结构化业务结果在数据连接中的传递。
- `按设备状态选择分支`：Action，通过 `IFlowContext` 显式选择成功、失败或错误分支。
- `汇总多个检测值`：Action，接收 `params int[]`，用于验证可变参数的展开和集合模式。
- `设置设备运行模式`：Action，接收 `设备运行模式`，用于验证普通枚举下拉选择和运行时转换。
- `配置设备操作权限`：Action，接收 `[Flags] 设备操作权限`，用于验证多选组合与零值互斥。
- `生成设备配置摘要`：Action，同时接收普通枚举与 Flags 枚举，返回 `设备配置摘要`，适合验证保存、重载和数据输出。
- `监听设备配置变更`：Flipflop，返回 `Task<设备配置摘要>`，用于验证 Flipflop 的枚举参数转换和异步输出。

类库源码通过独立的 `SereinFlow.Library` NuGet SDK 使用
`SereinFlow.Core.Api` 下的 `FlowLibrary`、`FlowNode`、`NodeParam` 和
`NodeType`，以及 `SereinFlow.Runtime.Abstractions.IFlowContext`。该 SDK
不依赖 SereinFlow Domain、Application 或 Worker；类库不应复制这些特性，
也不应继续使用旧版 `DynamicFlow`/`NodeAction` 标注。服务端通过稳定的完整
类型名读取 PE 元数据，不使用属性名后缀匹配，也不需要在 API 进程加载 DLL。
