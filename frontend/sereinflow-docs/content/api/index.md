# REST API 手册

API 是让你的程序与 SereinFlow 说话的入口。先把流程在控制台中画好，再用这些地址读取它、启动它、查看结果；需要对外长期开放时，再发布并创建公开接口。

想写可放在画布里的节点，请看[节点类库教程](/library/)；想在 ASP.NET Core 里托管服务，请看[宿主接入](/integration/hosting)。这一章专门介绍 HTTP 接口。

## 第一次调用建议这样走

1. 看[地址、密钥与报错](/api/basics)，确认自己能访问 API。
2. 用[项目与流程 API](/api/projects) 找到项目 ID 和流程 ID。
3. 用[运行 API](/api/runs) 启动并查询一次运行。
4. 需要对外调用时，读[流程版本](/api/versions)和[公开流程接口](/api/public-interfaces)。

## 按事情找接口

| 你想做什么 | 去哪里看 |
| --- | --- |
| 找项目、保存流程 | [项目与流程](/api/projects) |
| 发布或回滚流程 | [流程版本](/api/versions) |
| 启动运行、看输出和文件 | [运行、事件与工件](/api/runs) |
| 上传或升级节点类库 | [节点类库](/api/libraries) |
| 让外部程序调用已发布的流程 | [公开流程接口](/api/public-interfaces) |
| 管理运行设置和密钥 | [环境设置与密钥](/api/environment) |
| 暂停、单步检查流程 | [调试会话](/api/debug) |

本手册列出当前代码中的 REST 路由。完整字段模型还可以在运行中 API 的 Swagger UI（默认 `http://localhost:8188/swagger`）查看；它在 `Development` 环境默认开启。
