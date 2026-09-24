# 内部服务怎样注册

“内部服务注册”说的是：宿主启动时，SereinFlow 会把完成运行所需的功能放进 ASP.NET Core 的服务容器。你通常只需调用一次 `AddSereinFlow`，不需要逐个注册数据库、执行器或 HTTP 控制器。

## `AddSereinFlow` 做的事

当前实现会注册存储与应用服务、SignalR、跨域处理、启动检查和流程执行服务。若启用 `UseWebApi()`，还会注册 Web API、问题响应和 API 文档；若启用 `UseMcp()`，还会注册 MCP 服务。

```text
AddSereinFlow
  ├─ 存储与应用服务
  ├─ 执行服务和启动检查
  ├─ UseWebApi → 控制器、文档、运行与工作区 Hub
  └─ UseMcp    → HTTP MCP
```

这与画布节点的服务范围不同。上传节点在 Worker 中执行；Worker 只给它提供公开的节点服务和同一类库用 `[FlowService]` 声明的对象。宿主容器中的数据库对象、配置或 `IServiceProvider` 不会作为节点参数传过去。

## 该改哪一处

| 你的需求 | 应该改哪里 |
| --- | --- |
| 给画布增加业务动作 | 新建[节点类库](/library/)，用 `[FlowNode]` 标记方法。 |
| 让多个节点共用业务对象 | 在类库内用 `[FlowService]` 注册，详见[服务注入](/library/services)。 |
| 在已有 ASP.NET Core 站点中提供 SereinFlow | 在宿主调用[三步接入](/integration/hosting)。 |
| 从另一段 .NET 代码发起流程 | 注册[客户端 SDK](/integration/client-di)。 |

`AddSereinFlowStorage`、`AddSereinFlowApplication`、`AddSereinFlowExecution` 等方法确实存在于当前源码，但普通宿主接入已经由 `AddSereinFlow` 统一完成。除非你正在开发 SereinFlow 本身，否则按统一入口接入更容易保持服务之间的配置一致。
