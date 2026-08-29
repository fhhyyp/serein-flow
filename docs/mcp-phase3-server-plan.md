# MCP 第三阶段开发计划：只读 Server 适配层

## 执行记录

2026-08-29：本计划中的只读版本已完成。后续实现已扩展为统一 stdio/Streamable HTTP MCP 宿主，包含认证、项目授权、预览后写入、发布、回滚、SereinLang 仅编译诊断，以及预构建 DLL ZIP 的 PE 扫描和不可变导入。服务端不生成 C# 源码、不创建项目、不运行 `dotnet build` 或 MSBuild。

## 目标

将已经完成的 `AiReadModelService` 和结构化调试状态能力暴露为标准 MCP Server，使支持 MCP 的 Codex、OpenCode 和其他本地 AI 客户端可以读取 SereinFlow 流程与运行状态，并等待调试状态变化。

## 本阶段范围

1. 新增独立 `SereinFlow.McpServer` 进程，避免把 MCP 协议细节混入 API 或 Application 项目。
2. 支持 MCP stdio JSON-RPC 消息：`initialize`、`ping`、`tools/list`、`tools/call`、`resources/list`、`resources/templates/list`、`resources/read` 和 `shutdown`。
3. 暴露流程、类库、运行检查和结构化调试状态的只读 Resource。
4. 暴露项目、流程拓扑、类库、运行检查和调试状态查询 Tool。
5. 暴露 `sereinflow_wait_debug_state`，基于持久化 `StateRevision` 等待状态变化或终态。
6. 统一 JSON-RPC 错误码，非法参数使用 `-32602`，不支持的方法使用 `-32601`，资源不存在使用 `-32004`。
7. 通过 Application 服务和 DI Scope 访问业务数据，不直接引用 SQLite 表、SqlSugar Repository 或 Worker 句柄。
8. 增加协议测试、构建验证和本地配置说明。

## 暴露能力

### Resources

```text
sereinflow://projects
sereinflow://libraries
sereinflow://projects/{projectId}
sereinflow://projects/{projectId}/flows/{flowId}/topology
sereinflow://libraries/{libraryId}
sereinflow://runs/{runId}
sereinflow://debug-sessions/{sessionId}
```

### Tools

```text
sereinflow_list_projects
sereinflow_get_project
sereinflow_get_flow_topology
sereinflow_list_libraries
sereinflow_get_library
sereinflow_get_run_inspection
sereinflow_get_debug_state
sereinflow_wait_debug_state
```

所有查询继续使用 AI 只读模型的边界、脱敏和大小限制。默认不返回脚本源码和流程字面量值。

## 安全边界

- 当前传输仅面向受信任本地进程的 stdio；不能直接作为远程服务部署。
- Server 不提供流程定义修改、版本发布、回滚、类库上传或任意命令执行。
- Server 不启动调试 Worker，也不持有可执行程序集。
- 远程部署前必须增加认证、项目级授权、审计、请求超时、并发限制和输出配额。
- 流程写入 Tool 必须先经过校验和差异预览，并要求用户确认后才允许应用。

## 验收标准

1. MCP 客户端可以完成初始化、枚举 Tools/Resources 并读取结构化 JSON。
2. 调试客户端可以读取暂停节点、输入、步骤、调用帧深度和最近节点结果。
3. 客户端可以用状态修订号等待断点、节点结果或调试终态。
4. 协议错误不会泄露数据库路径、Worker 细节或完整异常堆栈。
5. Server 项目可以独立构建，协议测试稳定通过。

## 后续入口

后续演进应继续强化请求配额、审计检索、ZIP 兼容性分析和本地客户端 Skill；不得把 C# 构建或任意 shell 执行开放给 SereinFlow 服务端。
