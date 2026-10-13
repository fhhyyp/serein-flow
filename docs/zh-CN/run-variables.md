# 运行中的流程变量表

每次运行拥有一张 `LibraryVariableService` 表。节点、触发器、`FlowCall` 调用帧及所有上传类库共用它，其他运行独立。节点通过构造函数注入或 `IFlowContext.Variables` 访问；方法说明见[节点类库开发](node-library-development.md#92-流程变量表libraryvariableservice)。`Snapshot()` 在表内锁保护下复制当前名称和值，引用对象仍保留原始引用。

## 脚本读写

`SereinFlow.ScriptModules` 在内置 `sereinflow` 模块中导出 `variables`，与 C# 节点的
`LibraryVariableService` 共用同一张表：

```sereinlang
import { variables, log } from "sereinflow"
variables.set("batch.count", 42)
variables.set("batch.note", null)
if variables.contain("batch.count") {
    log.info(variables.get("batch.count"))
}
return variables.get("batch.count")
```

`contain(name)` 判断变量存在，包括值为 null 的变量；`get(name)` 在不存在或值为 null 时
返回 null；`set(name, value)` 新增或覆盖并返回 null。名称必须是非空、非纯空白的字符串，
区分大小写。脚本写入的 `42` 按 `System.Int32` 存储，C# 可直接 `Get<int>("batch.count")`。

脚本数组存为 `object?[]`，对象存为 `Dictionary<string, object?>`。这些容器转换时复制，
脚本修改后需再次 `set` 保存；CLR 句柄及容器内的句柄保留原引用。HTTP/MCP 写入的 JSON 可按
脚本对象或数组读取。不支持存储脚本函数和方法值。集合转换最多嵌套 32 层、每个容器最多
10,000 项，循环引用报错。单次读写有锁保护，多次 `get`/`set` 不构成原子更新。
详见 [ScriptModules 说明](../../src/SereinFlow.ScriptModules/README.md)。

## HTTP 读写

| 方法 | 路径 | 权限 |
| --- | --- | --- |
| `GET` | `/api/runs/{runId}/variables?offset=0&take=100` | `run.read` |
| `GET` | `/api/runs/{runId}/variables/{name}` | `run.read` |
| `PUT` | `/api/runs/{runId}/variables/{name}` | `run.variable.write` |

名称区分大小写，非空，外部请求最多 256 个字符；放进 URL 时应编码。列表按名称排序，`offset` 从 0 开始，`take` 默认 100，范围 1–200。响应包含 `variables`、`totalCount`、`hasMore` 和 `capturedAt`；读取响应禁止缓存。写入请求必须显式给出 JSON `value` 和 `valueType`：

```json
{ "value": 42, "valueType": "int32" }
```

| `valueType` | 存储的 CLR 值 |
| --- | --- |
| `null` | null，`value` 必须是 JSON null |
| `boolean` | `System.Boolean` |
| `string` | `System.String`，允许 null |
| `int32` | `System.Int32` |
| `int64` | `System.Int64` |
| `double` | 有限 `System.Double` |
| `decimal` | `System.Decimal` |
| `json` | 原始 `System.Text.Json.JsonElement`，适合对象和数组 |

设置 `int32` 后，节点可用 `Get<int>(name)` 读取。`json` 则用 `Get<JsonElement>(name)` 读取。禁止指定程序集限定类型名，也不会将字符串自动解析为数字。写入成功返回 `200` 和该变量的元数据及 JSON 投影。

## MCP 读写

- `sereinflow_get_run_variables`：必需 `runId`；可选 `name` 查询单个变量，或用 `offset` / `take` 查询分页。需要本项目的 `run.read` 权限。
- `sereinflow_set_run_variable`：必需 `runId`、`name`、`value`、`valueType`、`idempotencyKey`。需要本项目的 `run.variable.write` 权限；同一请求重试沿用幂等键，成功响应会重放，不再写入 Worker。

```json
{
  "runId": "运行实例的 GUID",
  "name": "batch.count",
  "value": 42,
  "valueType": "int32",
  "idempotencyKey": "batch-count-write-1"
}
```

不同内容的新写入要使用新的幂等键。Key 管理面板中可勾选“流程变量写入”权限。跨项目访问在请求进入 Worker 前拒绝。

## 前端动态查看

运行或调试启动后，在工作区面板切换器中打开 **流程变量**。面板显示名称、CLR 类型和值；打开时每秒刷新，支持手动刷新和每页 25 项的分页。运行控制台打开活动运行的快照后，也可切换到 **流程变量** 面板。切换运行、分页或关闭面板会取消旧请求，避免把其他运行的数据显示在当前表中。

Worker 在返回运行结果前采集一次最终变量快照，并随结束事件持久化。普通运行和调试运行均支持；成功、失败、正常取消和正常超时退出都会尝试采集。结束后，面板停止轮询并读取这份快照，标明“已保存的最终变量快照”；重新打开历史运行也能查看、刷新和分页。

原始 CLR 对象不会持久化，只保存有界 JSON 投影和类型信息。最终快照最多保存 2,000 项、256 KB；名称超过 256 字符的项会省略，超限值保留类型和 `snapshot_limit` 标记，并通过 `isTruncated` 指示截断。`isFinal` 区分最终快照与实时查询，`capturedAt` 是实际采集时间。之前没有采集的历史运行、被强制终止或崩溃的 Worker 可能没有最终快照，不能补回其变量；面板会明确提示未保存。

## JSON 投影和失败响应

条目包含 `name`、`clrType`、`valueAvailable`、`value`、保留数值精度的原始 JSON 文本 `valueJson` 和可选的 `unavailableReason`。null 是存在的变量，`valueAvailable` 为 true。不存在的变量返回 `404 variable.not_found`；不存在的运行返回 `404 run.not_found`；排队中或对已结束运行写入返回 `409 worker.not_active`；没有保存最终快照的历史读取返回 `409 variable.snapshot_unavailable`。

常见标量、`JsonElement`、有限大小的数组、标准 `List<T>` 和字符串键 `Dictionary` 可投影为 JSON。自定义对象、设备句柄、`Mat` 等仅返回 CLR 类型及 `unsupported_type`，不会执行其属性 getter、转换器或 `ToString()`。单值 JSON 上限为 16 KiB，响应上限为 512 KB；超大的本地值仍保留在 Worker，外部展示标记 `value_limit` 或 `snapshot_limit`。数组和标准集合投影最多 128 项、8 层嵌套，值无法投影时会显示原因。

请求格式或值类型不匹配返回 `400`，过大写入返回 `413`，Worker 应答超时返回 `504 variable.request_timeout`。变量锁只保护表本身；并发修改引用对象和多操作更新仍由节点自行同步。
