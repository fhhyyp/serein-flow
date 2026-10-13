# SereinFlow script modules / 脚本模块

`SereinFlow.ScriptModules` registers the built-in `sereinflow` module for each
script invocation. `log` emits run logs, `env` exposes invocation metadata, and
`variables` accesses the run's shared `LibraryVariableService`.

每次脚本调用会注册内置 `sereinflow` 模块：`log` 写运行日志，`env` 提供当前调用信息，
`variables` 访问本次运行共用的 `LibraryVariableService`。

```sereinlang
import { variables, log } from "sereinflow"

variables.set("batch.count", 42)
variables.set("batch.note", null)
if variables.contain("batch.count") {
    log.info(variables.get("batch.count"))
}
return variables.get("batch.count")
```

| Script method / 脚本方法 | Behavior / 行为 |
| --- | --- |
| `variables.contain(name)` | True if present, including stored null. / 存在时返回 true，包含已存入 null 的变量。 |
| `variables.get(name)` | Returns the script value; missing and stored null return null. / 返回脚本值，不存在或存储值为 null 时返回 null。 |
| `variables.set(name, value)` | Creates or replaces a variable; returns null. / 新增或覆盖变量，返回 null。 |

Names are non-empty, case-sensitive strings. All Script nodes, library nodes,
listener triggers and FlowCall frames in one run share the same table. Different
runs are isolated. Each table operation is locked; a sequence of `get` and `set`
is not an atomic update.

名称必须是非空、非纯空白的字符串，区分大小写。同一次运行的脚本、类库节点、监听触发器和
FlowCall 调用帧共用同一张表；不同运行隔离。每次表内读写受锁保护，先 `get` 再 `set`
组成的更新并非原子操作。

Script scalars are stored as native CLR values. For example, `42` is an `int`
and a C# node can read it with `Get<int>("batch.count")`. Arrays become `object?[]`
and objects become `Dictionary<string, object?>`; these containers are copied on
conversion, so call `set` to save script changes. CLR object handles retain their
original references, including when nested in containers. JSON written through
HTTP/MCP is readable as script objects and arrays. Script functions and method
values cannot be stored. Collection conversion is limited to 32 nesting levels
and 10,000 items per container; cycles fail with a script error.

脚本标量按原生 CLR 类型存储，如 `42` 存为 `int`，C# 节点可用
`Get<int>("batch.count")` 读取。数组转为 `object?[]`，对象转为
`Dictionary<string, object?>`；容器转换时复制，修改脚本容器后需再次 `set` 才能保存。
CLR 对象句柄保留原引用，包括容器内嵌套的句柄。HTTP/MCP 写入的 JSON 可按脚本对象或数组
读取。不支持存储脚本函数和方法值。集合转换最多嵌套 32 层，每个容器最多 10,000 项；
循环引用会产生脚本错误。

`FlowRunner` supplies the shared table through `NodeExecutionRuntime.Variables`.
Custom hosts must supply their run's table there too. No table is created silently
for an individual Script node. Registration and compilation work without an active
run; variable operations require a host table and respect run cancellation.

`FlowRunner` 通过 `NodeExecutionRuntime.Variables` 传入共享表；自定义宿主也应在这里
传入本次运行的表，不会为单独脚本节点隐式创建变量表。注册和编译不需要活动运行，实际变量
操作要求宿主提供变量表，并响应运行取消。

Runtime inspection and saved final snapshots use the existing variable APIs and
**Flow variables** panel. See [运行变量文档](../../docs/zh-CN/run-variables.md) /
[run variables](../../docs/en/run-variables.md).
