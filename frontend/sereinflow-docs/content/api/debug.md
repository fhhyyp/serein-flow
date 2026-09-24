# 调试会话 API

普通运行让流程直接走到结束；调试会话可以在指定节点前暂停，方便你查看当时的数据，再选择继续或单步执行。

## 创建与读取

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `POST` | `/api/projects/{projectId}/flows/{flowId}/debug-sessions` | 启动调试会话 |
| `GET` | `/api/debug-sessions/{sessionId}` | 读取会话状态 |
| `GET` | `/api/debug-sessions/{sessionId}/wait` | 等待状态变化 |
| `GET` | `/api/runs/{runId}/debug-session` | 从运行 ID 找调试会话 |

创建请求可以给 `breakpointNodeIds`（断点节点 ID），还可以给流程输入、超时和期望版本。返回 `202` 后保存 `sessionId`，随后读取会话状态。

`/wait` 可传 `afterRevision` 和 `timeoutSeconds`，让客户端等到状态变化或超时。等待秒数为 `0` 到 `60`。它不会修改流程，只是减少反复查询。

## 控制执行

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `POST` | `/api/debug-sessions/{sessionId}/continue` | 继续执行 |
| `POST` | `/api/debug-sessions/{sessionId}/step` | 执行一步 |
| `POST` | `/api/debug-sessions/{sessionId}/stop` | 停止调试 |

这三个请求体都需要 `commandSequence`。它是同一个会话里递增的命令序号，避免旧命令在新状态下重复执行。状态变化时先重新读取会话，再发送下一条命令。若返回 `409`，通常表示会话状态已经变化。

第一次调试建议先在控制台使用“调试流程”熟悉暂停和单步，再接入这些 API。运行与调试的关系见[运行与结果](/modules/runs)。
