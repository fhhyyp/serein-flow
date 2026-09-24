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

## 消息与工件

发消息时，`{topic}` 是运行中的消息主题，请求体有 `payload`，也可带 `messageId`、`contractId` 和 `channelKind`。这个入口只对正在运行且开放了外部消息的流程有用。需要安全重试时，保持同一个 `Idempotency-Key` 请求头。

工件是流程生成的文件。先列出工件，再用返回的 `workpieceId` 读取或下载。详解见[运行与结果](/modules/runs)。

`/interrupt` 只用于修复已经失去 Worker 管理、却仍显示为运行中的记录；普通取消请使用 `/cancel`。
