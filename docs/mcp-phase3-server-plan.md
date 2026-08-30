# MCP 第三阶段计划：统一宿主适配层

## 执行记录

2026-08-30：本计划已按 Web API 与 MCP 单宿主方案完成改造。正式入口只有
`SereinFlow.Api`；MCP 协议、Backend、HTTP transport、stdio transport、认证
上下文、限流和诊断逻辑位于 `src/SereinFlow.Mcp` 类库。

## 目标

让支持 MCP 的 Codex、OpenCode 和其他客户端安全读取 SereinFlow 项目、流程、
类库、运行与调试状态，并通过受控的预览/apply 合同执行允许的流程和类库操作。
Web API 与 MCP 必须共享同一 Application 服务、Storage options、数据库、类库
目录和配置解析结果。

## 入口

- Web 模式：`SereinFlow.Api` 同时提供普通 API 和 `/mcp`，开发地址为
  `http://127.0.0.1:5178/mcp`。
- stdio 模式：显式运行 `SereinFlow.Api --mcp-stdio`，只启用 MCP、Application、
  Storage 和 stdio 所需服务。
- HTTP 和 stdio 使用同一个 `SereinFlowMcpBackend` 与
  `SereinFlowMcpServer`，不通过 REST 回调，也不维护第二份 Backend。

## 配置与安全

服务器只从统一 `SereinFlowStorageOptions` 读取 `DataRoot`、数据库文件名、类库
目录名、脚本 artifact 目录名和 MCP staging 目录名。客户端不能通过参数或环境
变量注入 `DatabasePath`、`LibraryDirectory` 或其他服务器绝对路径。

HTTP 必须使用 `Authorization: Bearer <api-key>` 和有效 `Mcp-Session-Id`；loopback
也不提供免认证回退。stdio 必须使用显式
`SereinFlow:Mcp:Stdio:ApiKey`。stdout 保持纯 JSON-RPC，错误和日志写入 stderr。

## 能力边界

- Resource 和只读 Tool 通过 Application read model 提供有界数据。
- 流程 patch 使用 v2 typed union 合同，同时兼容流程公共合同的 legacy v1 输入；
  attach/detach library 继续是独立 preview/apply 操作。
- `sereinflow_create_library_node_template` 是无副作用的合同读取与节点组装能力，
  只接受已扫描并已附加的 Action/Flipflop 合同，不创建版本、预览或资源。
- 类库 ZIP 由用户本地工具链构建，服务端只做受控检查和显式导入。
- 服务端不执行用户构建脚本，不提供任意命令执行，不加载或执行上传程序集。

## 测试关注点

测试覆盖 Storage/path resolver、DI composition、Web/stdio 传输、认证、会话、
限流、大小和超时边界，以及 HTTP/stdio parity。还需验证 v2 flow patch、legacy
v1 normalization、normalized preview、library node template、节点布局规则和
旧入口架构约束。

## 后续维护

任何新 MCP Tool 都必须通过 `AddSereinFlowMcp` 注册，Backend 必须复用 Application
服务，不得新增独立 MCP executable、REST 回调或第二份权限/诊断实现。客户端文档
只能指导 `SereinFlow.Api` 的 HTTP URL 或显式 `--mcp-stdio` 入口。
