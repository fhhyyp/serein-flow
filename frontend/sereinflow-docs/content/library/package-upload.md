# 打包、上传与排错

写好的 C# 方法只有变成可上传的 ZIP，并被项目引用，才会出现在该项目的节点库中。

## ZIP 里要有什么

参考仓库里的 `SereinFlow.TestLibrary` 打包目标：它在 `dotnet publish` 后把完整输出放进 `类库名-版本号` 目录，再压缩为 `类库名-版本号.zip`。保留 `.dll`、依赖文件、`.deps.json` 和 `runtimes/.../native` 等原有相对目录。

```text
MyFlowNodes-1.0.0.zip
└─ MyFlowNodes-1.0.0/
   ├─ MyFlowNodes.dll
   ├─ MyFlowNodes.deps.json
   ├─ 其他运行时依赖...
   └─ runtimes/.../native/...
```

ZIP 文件名必须是 `类库名-版本号.zip`，里面第一层目录也必须叫 `类库名-版本号`，主程序集必须放在这层目录下且叫 `类库名.dll`。不要只压缩 `.cs` 源码，也不要漏掉运行时依赖。用 native DLL 时尤其要保留 `runtimes` 目录。包内的依赖应来自这次发布输出，避免混入旧版本文件。

## 在控制台中使用

1. 编译并制作 ZIP。
2. 在控制台上传节点类库；或者使用 `POST /api/libraries/upload`，表单文件字段名为 `file`。
3. 在目标项目中加入这份类库。上传与项目引用是两步；同一份类库可以提供给多个项目。
4. 回到画布节点库，选择新节点，填写输入、保存并运行。

需要自动化上传和项目引用时，见[节点类库 REST API](/api/libraries)。

## 找不到节点时

| 看到的情况 | 先检查 |
| --- | --- |
| 上传成功，项目里没有节点 | 项目是否引用了这份类库；主程序集里是否有 `[FlowLibrary]` 类和 `[FlowNode]` 方法。 |
| 节点出现了，运行时报依赖错误 | ZIP 是否带齐运行时依赖；native 文件目录是否与声明一致。 |
| 改了方法后旧流程不能正常读参数 | 是否改动了节点或参数的稳定 ID；有意改名时考虑 `NodeParam.Aliases`。 |
| 某服务无法注入 | 服务实现是否在同一上传程序集用 `[FlowService]` 注册，或是否属于 Worker 提供的内置服务。 |

类库更新后，可先使用项目的[升级预览](/api/libraries)查看影响，再应用升级。
