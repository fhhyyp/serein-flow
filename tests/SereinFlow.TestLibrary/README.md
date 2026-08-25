# SereinFlow 测试类库

这是一个不依赖运行时宿主的最小测试类库，用于验证 SereinFlow 的“上传类库”与节点目录功能。

## 构建上传包

在仓库根目录执行：

```powershell
dotnet build tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj
```

构建完成后，上传包会生成到：

```text
artifacts\libraries\SereinFlow.TestLibrary-1.0.0.zip
```

压缩包结构固定为：

```text
SereinFlow.TestLibrary-1.0.0\
└── SereinFlow.TestLibrary.dll
```

其中包含 5 个节点：

- `Add numbers`：Action，两个 `System.Int32` 参数，返回 `System.Int32`。
- `Format text`：Action，`System.Double` 与 `System.String` 参数，返回 `System.String`。
- `Is positive`：Flipflop，一个 `System.Int32` 参数，返回 `Task<System.Boolean>`（异步触发器方法）。
- `Emit`：Action，一个 `System.String` 参数，返回 `System.Void`。
- `Join labels`：Action，三个 `System.String` 参数，最后一个参数可选。

属性由共享的 `SereinFlow.Contracts` 程序集提供，命名空间仍为 `SereinFlow.Core.Api`，以保持类库元数据契约的稳定性。服务端通过该契约提供的完整类型名读取 PE 元数据，不使用属性名后缀匹配，也不需要在 API 进程加载该 DLL。
