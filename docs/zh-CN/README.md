# SereinFlow 文档

[English documentation index](../en/README.md)

此目录只保留长期维护的参考资料。历史设计笔记、实施计划、迁移记录和风险报告不再
作为公开项目文档的一部分，以便开发者和运维人员能快速找到当前有效的内容。

## 参考资料

- [MCP 服务参考](mcp-readonly-server.md)：HTTP 和 stdio 传输、鉴权、资源、提示词、
  工具与运行边界。
- [Worker 协议](worker-protocol.md)：统一说明当前 v2 生产协议、v1 历史兼容差异、
  JSON Lines 边界、会话流程、消息桥接和稳定错误码。
- [Worker 消息服务](worker-message-service.md)：运行级队列、事件总线、Worker
  DI 注入和活动运行的受控外部消息入口。
- [节点类库开发指南](node-library-development.md)：`SereinFlow.Library` SDK、节点特性、
  参数/返回值、注入、消息、工件、Native 依赖、OpenCV 示例、打包和升级兼容性。

首次本机启动和 MCP 客户端配置流程请阅读仓库根目录的
[中文 README](../../README.zh-CN.md)。
