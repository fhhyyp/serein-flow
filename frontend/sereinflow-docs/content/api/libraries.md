# 节点类库 API

类库是一包可复用的 .NET 节点。先上传到服务端，再把它加入项目。下面列出当前使用的主路由。

## 查看与上传

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `GET` | `/api/node-catalog/builtins` | 查看自带节点 |
| `GET` | `/api/libraries` | 列出类库 |
| `GET` | `/api/libraries/usage` | 查看类库使用情况 |
| `GET` | `/api/libraries/{libraryId}` | 读取一份类库 |
| `POST` | `/api/libraries/upload` | 上传 ZIP 包，使用 `multipart/form-data` |
| `POST` | `/api/libraries/upload-zip` | 同样用于上传 ZIP 包 |
| `PATCH` | `/api/libraries/{libraryId}/family` | 指定类库系列 |
| `PATCH` | `/api/libraries/{libraryId}/lifecycle` | 调整类库状态 |
| `DELETE` | `/api/libraries/{libraryId}` | 归档类库 |
| `GET` | `/api/library-families` | 列出类库系列 |
| `GET` | `/api/library-families/{familyId}/artifacts` | 列出系列下的制品 |

上传表单里的文件字段名是 `file`。若缺少它，服务器会返回 `400`。

## 让项目使用类库

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `GET` | `/api/projects/{projectId}/libraries` | 查看项目引用 |
| `GET` | `/api/projects/{projectId}/libraries/usage` | 查看该项目中的使用情况 |
| `PUT` | `/api/projects/{projectId}/libraries/{libraryId}` | 加入项目 |
| `DELETE` | `/api/projects/{projectId}/libraries/{libraryId}` | 从项目移除 |

上传成功不等于项目已经引用。若节点在上传后没有出现在项目中，先检查项目引用。

## 升级已有类库

先 `POST /api/projects/{projectId}/library-upgrades/preview` 生成升级预览，用 `GET /api/projects/{projectId}/library-upgrades/{upgradeId}` 查看计划，确认后再调用同一路径下的 `/apply` 或 `/apply-batch`。预览帮助你看清哪些流程会受影响。

环境管理员还可以使用 `/api/environment/libraries` 查看包含归档项的类库，用 `/api/environment/libraries/upload` 上传，并通过 `/archive`、`/reindex` 管理指定类库。这些是当前环境管理路由。

想先理解“类库”和“节点”的关系，请读[节点与类库](/modules/nodes-and-libraries)。
