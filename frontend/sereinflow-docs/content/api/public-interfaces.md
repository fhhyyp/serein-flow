# 公开流程接口 API

公开接口给已发布流程一个稳定的调用 ID。它属于环境设置中的“流程接口”，创建时需要指定项目和流程。

## 管理接口

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `GET` | `/api/environment/interfaces` | 列出公开接口 |
| `POST` | `/api/environment/interfaces` | 创建公开接口 |
| `PUT` | `/api/environment/interfaces/{interfaceId}` | 改名称、调用模式或启用状态 |
| `DELETE` | `/api/environment/interfaces/{interfaceId}` | 删除公开接口 |

创建时需要 `projectId`、`flowId`、`name`、`invocationMode` 和可选的 `isEnabled`。`invocationMode` 可用 `synchronous` 或 `asynchronous`。只有已发布生产版本的流程才能创建接口。

```json
{
  "projectId": "<项目 ID>",
  "flowId": "<流程 ID>",
  "name": "查询订单状态",
  "invocationMode": "asynchronous",
  "isEnabled": true
}
```

## 调用与查询

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `POST` | `/api/public/flows/{interfaceId}/invoke` | 调用已启用的公开接口 |
| `GET` | `/api/public/tasks/{taskId}` | 查询调用结果 |

调用请求可以包含 `projectInputs`、`timeoutSeconds`、`maxSteps` 和 `maxNodeVisits`。异步接口返回 `202` 和任务 ID；同步接口如果及时完成则返回 `200`，否则也返回 `202`。查任务时，`isCompleted` 表示是否结束，`flowData` 包含可对外读取的节点数据。

如果启用了 REST 鉴权，调用需要 `run.execute`，查询任务需要 `run.read`。完整使用顺序见[公开流程接口](/modules/public-interfaces)。
