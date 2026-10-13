# 节点与类库

流程图中的每个方块叫**节点**。节点可以运行一段脚本，也可以调用你上传的 .NET 类库中的方法。把若干节点连起来，流程才会按你的设计执行。

## 自带节点与上传的节点

当前服务会提供自带节点目录：脚本节点和 FlowCall 节点。脚本节点可直接写 SereinScript；FlowCall 用来调用其他画布中公开的节点。

脚本的编辑步骤、语法、输入输出和内置模块见[使用 SereinScript](/script/usage)。需要扩展脚本执行或增加 C# 模块时，从[SereinScript 使用与开发](/script/)开始。

如果需要连接设备、处理业务数据等，可以编写 .NET 节点类库，打成 ZIP 包并上传。上传后，先让项目引用这个类库，节点才会出现在项目可用的目录里。

## 在脚本中读写流程变量

脚本可导入 `variables`，与同一次运行中的 C# 类库节点、触发器和 FlowCall 子流程共用变量表：

```sereinlang
import { variables } from "sereinflow"
variables.set("batch.count", 42)
if variables.contain("batch.count") {
    return variables.get("batch.count")
}
return null
```

`get(name)` 在变量不存在或值为 null 时返回 null；`contain(name)` 可区分两种情况。
`set(name, value)` 新增或覆盖变量。名称区分大小写，必须是非空、非纯空白的字符串。
上面的 `42` 存为 `int`，C# 节点可直接用 `Get<int>("batch.count")` 读取。

数组、对象会转为 CLR 容器，转换时复制，脚本修改后需再次 `set` 保存；CLR 对象句柄保留原引用。
函数和方法值不能存入变量表，集合转换最多嵌套 32 层、每个容器最多 10,000 项。
每次表内读写受锁保护，多次 `get`/`set` 组成的更新并非原子操作。
运行时在 **流程变量** 面板查看，结束后可在运行快照中查看已保存的最终值。
接口和共享规则见[内置服务](/library/built-in-services#共用流程变量)及[运行 API](/api/runs#流程变量表)。

## 上传与引用是两步

1. `POST /api/libraries/upload` 上传类库包，让服务器认识它。
2. `PUT /api/projects/{projectId}/libraries/{libraryId}` 把它加入项目。

这样区分的好处是：一个类库可以被多个项目使用，而每个项目只看到自己需要的节点。类库有新版本时，可以先预览升级，再应用到项目。

## 看结果时要注意

节点在流程内部传递的值，与 API 对外展示的输出，不一定完全相同。需要对外读取节点结果时，查看运行的 `outputs`；需要下载生成的文件时，查看 `workpieces`（工件）。

想自己编写节点，请从[第一个节点](/library/first-node)开始。类库需要共享对象时读[服务注入](/library/services)；依赖 `.dll`、`.so` 等本机文件时读[native DLL 加载](/library/native-dll)。上传接口的地址见[节点类库 API](/api/libraries)。
