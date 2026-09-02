# SereinFlow 文档

[English documentation index](README.md)

此目录只保留长期维护的参考资料。历史设计笔记、实施计划、迁移记录和风险报告不再
作为公开项目文档的一部分，以便开发者和运维人员能快速找到当前有效的内容。

## 参考资料

- [MCP 服务参考](mcp-readonly-server.md)：HTTP 和 stdio 传输、鉴权、资源、提示词、
  工具与运行边界。
- [Worker Protocol v2](worker-protocol-v2.md)：API Supervisor 与临时 Worker Runner
  进程间的版本化 JSON Lines 协议，以及消息投递控制消息。
- [Worker 消息服务](worker-message-service.zh-CN.md)：运行级队列、事件总线、Worker
  DI 注入和活动运行的受控外部消息入口。

首次本机启动和 MCP 客户端配置流程请阅读仓库根目录的
[中文 README](../README.zh-CN.md)。
