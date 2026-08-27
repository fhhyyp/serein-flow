# SereinFlow 测试类库

这是一个面向生产线自动化流程的示例类库，用于验证 SereinFlow 的“上传类库”、节点目录、参数转换和 Worker 节点执行能力。类库中的业务名称、参数名称和说明均使用中文，便于直接作为新项目的本地测试类库。

## 构建上传包

在仓库根目录执行：

```powershell
dotnet build tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj
```

构建完成后，上传包会生成到：

```text
artifacts\libraries\SereinFlow.TestLibrary-1.1.0.zip
```

压缩包结构固定为：

```text
SereinFlow.TestLibrary-1.1.0\
└── SereinFlow.TestLibrary.dll
```

其中包含 5 个面向生产线业务的节点：

- `计算合格率`：Action，接收合格数量和检测总数，返回 `System.Decimal`。
- `构建设备写入指令`：Action，接收设备编号、点位名称和写入值，保留小数位为可选参数，返回 `System.String`。
- `记录工序结果`：Action，接收工单、工序、合格状态和可选备注，返回 `System.Void`。
- `等待设备触发`：Flipflop，接收设备编号和可选轮询间隔（50 至 3,600,000 毫秒），返回 `Task<System.Boolean>`。它模拟设备触发等待；作为全局 Flipflop 时，完成后会调度下游，并继续等待下一次触发。
- `汇总批次质量`：Action，返回 `批次质量结果`，用于覆盖结构化业务结果在数据连接中的传递。

属性由共享的 `SereinFlow.Contracts` 程序集提供，命名空间仍为 `SereinFlow.Core.Api`，以保持类库元数据契约稳定。服务端通过该契约提供的完整类型名读取 PE 元数据，不使用属性名后缀匹配，也不需要在 API 进程加载 DLL。
