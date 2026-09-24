# 项目与流程 API

先用项目接口找到工作区，再用流程接口读取画布内容。一个项目列表项包含 `project` 和 `flows` 两部分；`flows` 是摘要，不是完整画布。

## 当前路由

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `GET` | `/api/projects` | 列出项目；`includeArchived=true` 可包含归档项目 |
| `POST` | `/api/projects` | 用名称和初始流程定义创建项目 |
| `PUT` | `/api/projects/{projectId}` | 重命名项目 |
| `POST` | `/api/projects/{projectId}/archive` | 归档项目 |
| `GET` | `/api/projects/{projectId}/flows/{flowId}` | 读取完整流程定义 |
| `PUT` | `/api/projects/{projectId}/flows/{flowId}` | 保存流程定义 |

## 读取一条流程

```powershell
$items = Invoke-RestMethod 'http://localhost:8188/api/projects'
$projectId = $items[0].project.id
$flowId = $items[0].flows[0].id
$flow = Invoke-RestMethod "http://localhost:8188/api/projects/$projectId/flows/$flowId"
$flow
```

项目列表可能为空；这时先按[第一个流程教程](/guide/first-flow)创建项目。这里的 ID 是系统给每个项目和流程分配的唯一编号。

## 写入时要留意

创建项目的请求体是 `{ "name": "...", "definition": { ... } }`，其中 `definition` 必须是完整流程定义，不能只给一个流程名称。刚开始最稳妥的做法是在控制台创建，再用 GET 读取现成定义。

重命名项目需要 `name` 和当前的 `expectedVersion`。保存流程需要 `expectedVersion` 和完整 `definition`，且 `definition.id` 要与路径中的 `flowId` 一致。若版本已被别人更新，服务器返回 `409`；先重新读取最新内容，再决定怎样合并自己的修改。

为什么这样设计，见[项目与流程](/modules/projects-and-flows)。
