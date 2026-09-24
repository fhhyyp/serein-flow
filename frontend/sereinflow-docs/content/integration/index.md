# 服务与接入

这里的“服务注入”可能指三件不同的事。先看你要做什么，再去对应章节。

| 你要做什么 | 使用哪个入口 | 阅读 |
| --- | --- | --- |
| 在画布上增加自己的节点，让节点共用一个对象 | 节点类库的 `[FlowService]` | [注册自己的服务](/library/services) |
| 在自己的 ASP.NET Core 程序中运行 SereinFlow | `AddSereinFlow`、`UseSereinFlow`、`MapSereinFlow` | [宿主接入](/integration/hosting) |
| 在已有 .NET 程序中调用运行中的 SereinFlow | `AddSereinFlowClient`、`ISereinFlowClient` | [.NET 客户端](/integration/client-di) |

SereinFlow 自己如何注册存储、执行、HTTP 和 MCP 服务，见[内部服务怎样注册](/integration/service-registration)。节点类库在独立的 Worker 中运行，宿主服务不会自动成为节点构造函数参数。
