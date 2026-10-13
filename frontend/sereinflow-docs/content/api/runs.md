# 运行、事件与工件 API

一次运行对应一次真实执行。启动后先保存返回的 `runId`，再用它查看状态、节点输出和生成的文件。

## 当前路由

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `POST` | `/api/projects/{projectId}/flows/{flowId}/runs` | 提交运行，成功返回 `202` |
| `GET` | `/api/runs` | 按 `status`、`projectId`、`take` 列出运行 |
| `GET` | `/api/runs/overview` | 读取队列与近期运行概览 |
| `GET` | `/api/runs/{runId}` | 读取一次运行的状态 |
| `GET` | `/api/runs/{runId}/debug-session` | 查找关联的调试会话 |
| `GET` | `/api/runs/{runId}/snapshot` | 读取这次运行用到的流程快照 |
| `GET` | `/api/runs/{runId}/outputs` | 读取节点对外输出 |
| `GET` | `/api/runs/{runId}/events` | 读取事件；可用 `afterSequence` 只取新事件 |
| `GET` | `/api/runs/{runId}/events/stream` | 持续接收 SSE 事件 |
| `POST` | `/api/runs/{runId}/cancel` | 取消未结束的运行 |
| `POST` | `/api/runs/{runId}/interrupt` | 将失去 Worker 管理的运行标为中断 |
| `POST` | `/api/runs/{runId}/messages/{topic}` | 向活动运行发消息 |
| `GET` | `/api/runs/{runId}/workpieces` | 列出生成的文件 |
| `GET` | `/api/runs/{runId}/variables?offset=0&take=100` | 分页读取活动运行变量 |
| `GET` | `/api/runs/{runId}/variables/{name}` | 读取单个变量 |
| `PUT` | `/api/runs/{runId}/variables/{name}` | 设置变量，需 `run.variable.write` 权限 |
| `GET` | `/api/runs/{runId}/workpieces/{workpieceId}` | 读取文件；`download=true` 下载 |

## 启动请求

请求体可包含 `expectedFlowVersion`、`projectInputs`、`timeoutSeconds`、`maxSteps` 和 `maxNodeVisits`。例如：

```json
{
  "expectedFlowVersion": null,
  "projectInputs": {},
  "timeoutSeconds": 60,
  "maxSteps": null
}
```

`projectInputs` 里的键和值要与自己的流程约定一致。若你刚开始试，可以先运行没有外部输入的流程。完整可复制的 PowerShell 请求见[用 API 运行流程](/guide/run-with-api)。

## 怎样判断完成

启动返回 `202` 后，定期查询 `GET /api/runs/{runId}`，直到状态从 `pending` 或 `running` 变为结束状态。成功状态为 `succeeded`；失败时可查看 `errorSummary` 和事件。

持续监听事件的 `/events/stream` 会保持连接，每产生一条新事件就发给客户端。这种方式叫 SSE；它没有放进 OpenAPI 页面。客户端断线后，带上最后收到的 `Last-Event-ID` 可以从后续事件继续读取。

## 流程变量表

变量属于一个运行实例，节点、触发器和子流程共用；普通运行和调试暂停期间都可读写。读取需要 `run.read` 权限，设置需要 `run.variable.write` 权限，并受到项目范围限制。

写入请求示例：

```json
{ "value": 42, "valueType": "int32" }
```

`valueType` 必需，可选 `null`、`boolean`、`string`、`int32`、`int64`、`double`、`decimal` 或 `json`。`int32` 在节点中按 `Get<int>(name)` 读取；`json` 存为 `JsonElement`，按 `Get<JsonElement>(name)` 读取。数值不会自动从字符串转换，也不能指定程序集限定类型名。

列表按名称排序，`take` 默认 100、范围 1–200。响应包含名称、CLR 类型、`valueAvailable`、JSON `value` 和保留数值精度的 `valueJson` 文本，以及 `totalCount`、`hasMore`、`capturedAt`。复杂对象只展示类型，不执行属性 getter 或 `ToString()`；超大值会标注无法展示。单值最多 16 KiB JSON，整个响应最多 512 KB。

MCP 使用 `sereinflow_get_run_variables`（`runId`，可选 `name` 或分页参数）和 `sereinflow_set_run_variable`（`runId`、`name`、`value`、`valueType`、`idempotencyKey`）。成功的相同写入请求会按幂等键重放响应。

前端工作区和运行快照窗口都有 **流程变量** 面板，运行时每秒刷新，支持手动刷新和每页 25 项的分页。Worker 退出前采集一次最终变量快照，普通和调试运行均随结束事件保存；结束后及重新打开历史运行时，面板读取保存的快照。`isFinal` 表示最终快照，`isTruncated` 表示达到最多 2,000 项或 256 KB 的保存上限。只保存安全 JSON 投影及类型信息，不保存 `Mat` 等原始 CLR 对象。旧运行、崩溃和强制终止可能没有快照，会返回 `409 variable.snapshot_unavailable`。缺失变量返回 `404`，已结束运行的写入返回 `409`，非法类型返回 `400`，过大写入返回 `413`，应答超时返回 `504`。

## 消息与工件

发消息时，`{topic}` 是运行中的消息主题，请求体有 `payload`，也可带 `messageId`、`contractId` 和 `channelKind`。这个入口只对正在运行且开放了外部消息的流程有用。需要安全重试时，保持同一个 `Idempotency-Key` 请求头。

工件是流程生成的文件。先列出工件，再用返回的 `workpieceId` 读取或下载。详解见[运行与结果](/modules/runs)。

`/interrupt` 只用于修复已经失去 Worker 管理、却仍显示为运行中的记录；普通取消请使用 `/cancel`。
