# 流程版本 API

编辑流程时产生开发版本。发布后产生供公开接口使用的生产版本。版本接口让你查看历史、发布当前内容，或从旧内容创建一个新的当前版本。

路径前缀：`/api/projects/{projectId}/flows/{flowId}`。

| 方法 | 后续路径 | 做什么 |
| --- | --- | --- |
| `GET` | `/versions` | 列出版本，可用 `track` 筛选 |
| `GET` | `/versions/{version}` | 读取某个版本及其流程内容 |
| `POST` | `/publish` | 发布当前开发版本 |
| `POST` | `/versions/{version}/rollback` | 以旧版本内容创建新的当前版本 |

## 发布需要什么

发布请求体中的 `expectedDevelopmentVersion` 应等于你刚读取到的开发版本号。可选的 `remark` 是本次发布说明：

```json
{
  "expectedDevelopmentVersion": 3,
  "remark": "修正订单处理步骤"
}
```

服务器会先检查流程是否能够执行，以及它引用的类库是否可用。版本已变化、项目已归档或校验失败时，发布不会成功。

## 回滚要怎样理解

回滚请求还需给出 `track`（`development` 或 `production`）和 `expectedHeadVersion`。它不会抹掉中间版本，而是生成一个新版本。若当前版本已变化，请重新读取历史，再决定是否继续。

先读[版本与发布](/modules/versions)，会更容易理解何时保存、何时发布。
