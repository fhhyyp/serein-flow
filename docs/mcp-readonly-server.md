# SereinFlow MCP 服务

## 状态

MCP 由 `SereinFlow.Api` 唯一宿主承载。Web 模式在同一 API 进程挂载
`/mcp`，与普通 Web API、Application 服务、数据库、类库目录和执行服务共用
同一个 DI 容器；本地自动化可以显式使用 `SereinFlow.Api --mcp-stdio`。

MCP Backend 只通过 Application 服务和受控 DI Scope 访问业务数据，不通过本机
REST 回调 API，也不直接访问 SQLite、SqlSugar Repository 或 Worker 句柄。流程
修改、发布、回滚、SereinLang 编译和类库导入继续经过权限检查、预览确认、幂等
控制和审计。

## 启动

### Web API 与 HTTP MCP

构建并启动 API：

```text
dotnet build src/SereinFlow.Api/SereinFlow.Api.csproj
dotnet run --project src/SereinFlow.Api/SereinFlow.Api.csproj
```

开发配置的 MCP 地址是 `http://127.0.0.1:8188/mcp`。请求必须携带：

```text
Authorization: Bearer <api-key>
```

首次请求 `initialize` 会建立 `Mcp-Session-Id`，后续请求必须回传同一会话 ID。
HTTP transport 继续执行请求体大小、响应大小、并发、每主体速率和工具超时限制。
未认证、无效会话、超限、超时和内部异常均返回稳定的 `mcp.*` 诊断；loopback
地址不提供免认证管理员绕过。

Git 中的 MCP 客户端配置只保存 URL，不保存 API key、数据库路径或类库目录：

```json
{
  "mcpServers": {
    "sereinflow": {
      "type": "http",
      "url": "http://127.0.0.1:8188/mcp"
    }
  }
}
```

API key 应由 MCP 客户端的安全凭据、外部密钥注入或凭据存储提供。不要在仓库、
插件配置、日志或流程内容中写入 key。

SereinFlow AI Toolkit 将 `SEREINFLOW_MCP_API_KEY` 作为 HTTP Bearer token 的
环境变量名；插件只声明变量名，不存储或传输密钥值。推荐在 Web Console 的“环境设置”
中完成密钥管理：第一次打开时点击“生成首个密钥”，随后为 Codex 创建项目范围的
客户端 key。完整 `sfk_...` Secret 只在生成或轮换后显示一次，服务端数据库只保存
哈希和盐。初始化接口仅接受本机 loopback 请求；如果服务器和浏览器不在同一台机器，
请先由服务器管理员通过受控配置完成首次初始化，再在 Web Console 中继续管理密钥。

生成的 Secret 需要填入 Codex 的 MCP 凭据配置中，变量名填写
`SEREINFLOW_MCP_API_KEY`，变量值粘贴 Web Console 显示的完整 Secret。浏览器出于
安全边界不能直接修改已经运行的 Codex 进程环境变量，因此服务端可以负责生成和持久化
密钥，但客户端仍需要这一步凭据绑定。不要把 Secret 写入仓库、插件源文件、日志或流程
内容。

只有在无法访问 Web Console 时，才使用 PowerShell 配置首次 bootstrap key；尖括号
内容是同一段随机密钥，不能提交或记录：

```powershell
[Environment]::SetEnvironmentVariable('SereinFlow__Mcp__BootstrapAdminKey', '<temporary-random-secret>', 'User')
[Environment]::SetEnvironmentVariable('SEREINFLOW_MCP_API_KEY', '<temporary-random-secret>', 'User')
```

修改用户环境变量后，必须重启正在运行的 `SereinFlow.Api` 和 Codex，令两个进程读取
新的环境。若返回 HTTP `401`，表示地址和 MCP 路由可达，但 Bearer key 缺失、无效、
过期或已撤销；这不是端口或 Resource 清单错误。

### 本地 stdio

stdio 是 API executable 的显式入口：

```text
dotnet run --project src/SereinFlow.Api/SereinFlow.Api.csproj -- --mcp-stdio
```

stdio 模式只注册 MCP、Application、Storage 和 stdio 所需服务，不启动 HTTP
listener、SignalR 或 API-only hosted services。必须显式设置
`SereinFlow:Mcp:Stdio:ApiKey`（环境变量形式为
`SereinFlow__Mcp__Stdio__ApiKey`）；缺少、无效、过期或撤销的 key 会使进程以
非零状态退出。stdout 只输出 JSON-RPC，启动错误、日志和内部诊断写入 stderr。
正常 EOF 会清理并正常退出。

## 数据路径配置

数据库和类库目录属于服务器宿主配置，不是 MCP 客户端参数。统一配置位于
`SereinFlow`：

```json
{
  "SereinFlow": {
    "DataRoot": "data",
    "DatabaseFileName": "sereinflow.db",
    "LibraryDirectoryName": "libraries",
    "ScriptArtifactDirectoryName": "script-artifacts",
    "McpStagingDirectoryName": "mcp-staging"
  }
}
```

相对 `DataRoot` 名称只在服务器宿主内部相对于 `ContentRootPath` 解析，并由
统一的 `SereinFlowStorageOptions` 解析一次。旧的 `DatabasePath`、
`LibraryDirectory`、`ScriptArtifactRoot` 和 MCP staging 路径键不应继续配置；
没有显式 `DataRoot` 时检测到这些键会直接失败，并给出安全迁移诊断，不会静默
切换数据库。

## Resources

The server exposes a compact routing index at `sereinflow://ai/guide` and
three capability-specific AI Resources:

```text
sereinflow://ai/guide
sereinflow://ai/skills/sereinflow
sereinflow://ai/skills/sereinlang
sereinflow://ai/skills/sereinflow-library-package
```

The index is intentionally short. A client should read only the capability
Resource matching the current request, so a syntax check does not load flow,
release and C# packaging rules. The former local Skills are represented by
separate server Resources; each capability also exposes smaller task modules,
and none of them are copied into the client plugin.

Each Resource is loaded from the server deployment at every read, so an
operator can update one Markdown file without rebuilding or reinstalling the
client plugin. The backing files are selected only by server configuration and
are constrained to remain under the server ContentRoot. The MCP caller
supplies only fixed Resource URIs and cannot select an arbitrary local file.
The default index files are `mcp/sereinflow-ai-guide.md`,
`mcp/sereinflow-skill.md`, `mcp/sereinlang-skill.md`, and
`mcp/sereinflow-library-package-skill.md`. Focused module files use the
`mcp/*-skill.md` defaults listed by `sereinflow://ai/guide`. Deployments can
override the four index paths with `SereinFlow:Mcp:AiGuidance:FilePath`,
`SereinFlow:Mcp:AiGuidance:SereinFlowFilePath`,
`SereinFlow:Mcp:AiGuidance:SereinLangFilePath`, and
`SereinFlow:Mcp:AiGuidance:LibraryPackageFilePath`. Focused module paths can
be overridden under `SereinFlow:Mcp:AiGuidance:Modules:<resource-key>`; each
file uses the shared `SereinFlow:Mcp:AiGuidance:MaxBytes` limit.

```text
sereinflow://projects
sereinflow://archived-projects
sereinflow://libraries
sereinflow://archived-libraries
sereinflow://projects/{projectId}
sereinflow://projects/{projectId}/flows/{flowId}/topology
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}
sereinflow://libraries/{libraryId}
sereinflow://runs
sereinflow://debug-sessions
sereinflow://runs/{runId}
sereinflow://debug-sessions/{sessionId}
sereinflow://mcp-previews/{previewId}
```

The default project and library collection Resources are the active working
set: `sereinflow://projects` excludes archived projects and
`sereinflow://libraries` contains only available library artifacts. Their
archived counterparts return only archived records, so the default and
archived collections are mutually exclusive. Direct project and library
Resources remain readable by ID for audit and existing-reference inspection.

## Prompts

MCP clients that support the standard Prompt capability can discover these
workflow entry points through `prompts/list` and request a prepared instruction
through `prompts/get`:

```text
sereinflow.inspect
sereinflow.edit-flow
sereinflow.debug-run
sereinflow.publish-flow
sereinflow.package-library
sereinflow.upgrade-library
sereinlang.compile
```

Each Prompt returns a bounded workflow message and injects only its selected
capability Resource. `sereinflow.inspect` accepts an optional `request`
argument; the other Prompts require a non-empty `request` argument. Prompt
messages guide the client toward the corresponding read-only inspection,
preview, explicit confirmation, apply, and verification steps; a Prompt does
not authorize a mutation by itself.

Clients that do not automatically read MCP resources or invoke Prompts still
receive only the compact routing and safety rules through
`initialize.instructions` and the individual tool descriptions. The detailed
capability text is never appended to initialization, which keeps unrelated
rules out of the initial context. The Resources and Prompt catalog do not
contain API keys, server absolute paths, client-local build paths, database
names, or server-local library directories.

## Tools

工具清单由 `tools/list` 返回，包含项目、流程、运行、调试、类库和 API key
管理能力。调试闭环使用 `sereinflow_list_runs` 或
`sereinflow_list_debug_sessions` 发现目标，使用
`sereinflow_start_debug_session` 启动，再通过
`sereinflow_wait_debug_state`、`sereinflow_get_debug_state`、
`sereinflow_continue_debug`、`sereinflow_step_debug` 和
`sereinflow_stop_debug` 控制。启动调试要求 `debug.control` 和
`idempotencyKey`；列表、状态和等待读取接受 `debug.read` 或 `run.read`，
控制命令要求严格递增的 `commandSequence`。

活动运行消息发布使用独立的 mutation 工具
`sereinflow_publish_run_message`，不属于 `debug.control`。工具需要
`run.message.publish` 权限和必填的 `idempotencyKey`，输入的 `payload` 是任意
JSON 值（包括数组、字符串、数值、布尔值和 `null`），`channelKind` 使用稳定的
`queue` 或 `eventBus` 字符串。工具按 `runId` 查找活动运行并执行项目范围检查，
因此普通运行和调试运行使用同一入口；它不接受 `flowId`，也不会按消息重新加载
流程。

```json
{
  "runId": "8e0f2b1e-7e9c-4e8e-b5d0-2f4d8d6f21a8",
  "topic": "order.created",
  "payload": { "orderId": "A10001", "amount": 99.5 },
  "channelKind": "queue",
  "contractId": "order.created.v1",
  "messageId": "af46ef89-5712-4dff-a6df-bf4e57f80a7d",
  "idempotencyKey": "agent-call-20260902-001"
}
```

成功结果是结构化的 Worker 消息接收结果；`status: "accepted"` 只代表消息进入
运行级 Broker。MCP 应通过运行详情、事件、输出和调试状态观察 Flipflop 后继节点
以及整个流程。MCP 幂等结果按主体、工具和请求内容持久化，重试同一个
`idempotencyKey` 返回原结果而不再次投递；Worker 仍按稳定 `messageId` 防止 Broker
内的重复消息。常见业务错误码包括 `run.not_found`、`worker.not_active`、
`worker.not_found`、`message.endpoint_not_ready`、`message.endpoint_forbidden`、
`message.contract_mismatch`、`message.channel_full` 和
`message.delivery_timeout`。

MCP API key 管理工具仅限管理员使用。使用
`sereinflow_list_mcp_api_keys` 读取当前状态后，再按明确请求调用创建、轮换或
撤销工具；变更操作需要 `idempotencyKey`。项目级 key 必须绑定未归档项目，管理
员 key 不能绑定项目。创建或轮换返回的 secret 只显示一次，不得写入日志、仓库、
Prompt 或无关工具参数；发生不明确响应时先重新读取 key 列表，不要盲目重试。

流程修改使用 `sereinflow_preview_flow_patch` 后再由显式确认的
apply Tool 执行；v2 请求使用 `schemaVersion: "2.0"`、`op` discriminator、
具名 payload 和 canonical camelCase 枚举。兼容期仍接受流程公共合同的 legacy
v1 输入，但响应始终返回 v2 `normalizedOperations` 与
`normalizationWarnings`。

类库节点必须先调用只读的
`sereinflow_create_library_node_template`。它只接受已扫描并已附加项目的真实
Action/Flipflop 合同，返回完整 canonical `NodeDto`、runtime library metadata、
参数端口、默认 literal、枚举/variadic 元数据、包 SHA-256 和
`contractRevision`。将返回的 node 原样放进 v2 `addNode`；类库 attach/detach
仍是独立的 preview/apply 操作，不属于 flow patch。

Flow patch also exposes `addNodeParameter` and `removeNodeParameter` for
parameter-level edits. Use `addNodeParameter` with a complete parameter
contract and a unique `ui.id`; this is the MCP operation for adding another
member of a variadic group. Remove incoming data connections before using
`removeNodeParameter`. The operations are ordered within the preview, so a
new parameter can be added before an `addConnection` targets its ID.

## 节点绘制约束

绘制前先读取 `sereinflow_get_flow_edit_model`，依据实际节点位置、尺寸、端口、
连接和画布边界布局。每个节点必须有不重叠的 bounding box，并与现有节点、其他
新节点和画布边界保持安全间距。通常约 `260 px` 宽的节点，列间距使用约
`340-420 px`；参数较多时增加间距，横向至少保留 `80 px` 清晰区，分支行至少
保留 `64 px`。

主执行路径从左向右，同一执行阶段对齐；成功、失败、错误分支使用独立行并向
右侧展开。连接尽量短且少交叉；数据连接不能穿过节点主体或端口列表，必要时
使用节点上下方的专用通道。新增节点造成重叠时只移动受影响节点，并在同一个
preview 中包含坐标变化；提交 preview 前检查所有节点是否越界、重叠以及连接
是否穿过节点。

## 安全与诊断

客户端应把已连接的 SereinFlow 服务视为黑盒，只使用 MCP 错误码、结构化
`data`、公开资源和有界诊断。不得通过客户端参数或环境变量改变服务器的
数据库、类库或 staging 路径，也不得搜索服务源码、读取服务器数据库或反编译
上传程序集来解释远程错误。

未预期异常返回 JSON-RPC `-32603`，带有 `data.code = "mcp.internal_error"`
和 `diagnosticId`；HTTP 工具超时返回 `mcp.tool_timeout` 和同样的诊断 ID。
客户端应将诊断 ID 提供给服务运维人员。
