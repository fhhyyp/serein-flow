# SereinFlow MCP 服务

## 状态

MCP 由 `SereinFlow.Api` 唯一宿主承载。Web 模式在同一 API 进程挂载
`/mcp`，与普通 Web API、Application 服务、数据库、类库目录和执行服务共用
同一个 DI 容器；本地自动化可以显式使用 `SereinFlow.Api --mcp-stdio`。

MCP 后端只通过 Application 服务和受控 DI Scope 访问业务数据，不通过本机
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
HTTP 传输继续执行请求体大小、响应大小、并发、每主体速率和工具超时限制。
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
过期或已撤销；这不是端口或资源清单错误。

### 本地 stdio

stdio 是 API 可执行文件的显式入口：

```text
dotnet run --project src/SereinFlow.Api/SereinFlow.Api.csproj -- --mcp-stdio
```

stdio 模式只注册 MCP、Application、Storage 和 stdio 所需服务，不启动 HTTP
监听器、SignalR 或仅限 API 的托管服务。必须显式设置
`SereinFlow:Mcp:Stdio:ApiKey`（环境变量形式为
`SereinFlow__Mcp__Stdio__ApiKey`）；缺少、无效、过期或撤销的 key 会使进程以
非零状态退出。stdout 只输出 JSON-RPC，启动错误、日志和内部诊断写入 stderr。
正常 EOF 会清理并正常退出。

## JSON-RPC 协议错误码

`McpProtocolException` 在代码中统一使用
`McpProtocolErrorCodes` 的命名常量，不再直接散落数值字面量；JSON-RPC 线上的
`error.code` 仍然保持数字格式。`error.data.code` 中的业务诊断继续使用稳定的
字符串错误码，例如 `mcp.invalid_arguments`。

| 常量 | 线上错误码 | 含义 |
| --- | ---: | --- |
| `ParseError` | `-32700` | 请求体不是有效 JSON。 |
| `InvalidRequest` | `-32600` | JSON-RPC 外层请求结构无效。 |
| `MethodNotFound` | `-32601` | 请求的 MCP 方法或工具不受支持。 |
| `InvalidParams` | `-32602` | 请求参数或工具负载无效。 |
| `InternalError` | `-32603` | 服务端发生未预期异常。 |
| `GenericServerError` | `-32000` | 服务端失败，但没有更具体的映射。 |
| `Unauthenticated` | `-32001` | 需要鉴权或鉴权失败。 |
| `PermissionDenied` | `-32003` | 调用方已鉴权，但没有所需权限。 |
| `ResourceNotFound` | `-32004` | 请求的项目、流程、预览、资源或密钥不存在。 |
| `TransientFailure` | `-32005` | 请求被限流，或依赖服务超时。 |
| `Conflict` | `-32010` | 资源已变化，或与当前变更发生冲突。 |
| `OperationRejected` | `-32011` | 请求已理解，但当前不能应用。 |
| `RequestTooLarge` | `-32012` | 请求超过配置的大小限制。 |
| `ResponseTooLarge` | `-32013` | 响应超过配置的大小限制。 |
| `LibraryInspectionUnavailable` | `-32020` | 当前宿主未提供类库包检查能力。 |

客户端应使用 JSON-RPC 的数字 `error.code` 处理协议分支，并使用
`error.data.code` 判断 SereinFlow 业务诊断和修复方式。数字协议码与
`SereinFlow.Contracts` 中按类型划分的字符串业务错误码不是同一套值，不能混用。

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
    "McpStagingDirectoryName": "mcp-staging",
    "WorkpieceDirectoryName": "workpieces"
  }
}
```

相对 `DataRoot` 名称只在服务器宿主内部相对于 `ContentRootPath` 解析，并由
统一的 `SereinFlowStorageOptions` 解析一次。旧的 `DatabasePath`、
`LibraryDirectory`、`ScriptArtifactRoot` 和 MCP staging 路径键不应继续配置；
没有显式 `DataRoot` 时检测到这些键会直接失败，并给出安全迁移诊断，不会静默
切换数据库。

## 资源

服务器在 `sereinflow://ai/guide` 提供简短的路由索引，并提供三个能力索引资源：

```text
sereinflow://ai/guide
sereinflow://ai/skills/sereinflow
sereinflow://ai/skills/sereinlang
sereinflow://ai/skills/sereinflow-library-package
```

路由索引刻意保持简短。客户端只应读取与当前请求匹配的能力资源，因此语法检查不必
加载流程、发布和 C# 打包规则。原先的本地 Skill 已由服务器资源替代；每个能力还提供
更小的任务模块，包括用于图像预览和文件下载的运行工件模块，客户端不会复制这些内容。

每次读取资源时，服务器都会从部署目录加载对应文件，因此运维人员更新单个 Markdown
文件后，无需重建或重新安装客户端插件。文件路径只由服务器配置选择，并限制在服务器
ContentRoot 下。MCP 调用方只能请求固定的资源 URI，不能选择任意本地文件。默认索引文件
为 `mcp/sereinflow-ai-guide.md`、`mcp/sereinflow-skill.md`、
`mcp/sereinlang-skill.md` 和 `mcp/sereinflow-library-package-skill.md`。任务模块文件
使用 `sereinflow://ai/guide` 中列出的 `mcp/*-skill.md` 默认路径。部署时可以通过
`SereinFlow:Mcp:AiGuidance:FilePath`、`SereinFlow:Mcp:AiGuidance:SereinFlowFilePath`、
`SereinFlow:Mcp:AiGuidance:SereinLangFilePath` 和
`SereinFlow:Mcp:AiGuidance:LibraryPackageFilePath` 覆盖四个索引路径；任务模块路径
可以在 `SereinFlow:Mcp:AiGuidance:Modules:<resource-key>` 下覆盖。每个文件都受共享的
`SereinFlow:Mcp:AiGuidance:MaxBytes` 大小限制。

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

默认的项目和类库集合资源表示当前工作集：`sereinflow://projects` 不包含已归档项目，
`sereinflow://libraries` 只包含可用的类库制品。对应的归档集合只返回已归档记录，因此
默认集合与归档集合互斥。项目和类库的按 ID 资源仍可读取，用于审计和检查现有引用。

## 提示词

支持标准 Prompt 能力的 MCP 客户端可以通过 `prompts/list` 发现以下工作流入口，并通过
`prompts/get` 请求准备好的指令：

```text
sereinflow.inspect
sereinflow.edit-flow
sereinflow.debug-run
sereinflow.publish-flow
sereinflow.package-library
sereinflow.upgrade-library
sereinlang.compile
```

每个提示词返回有界的工作流消息，只注入所选的能力资源。`sereinflow.inspect` 的
`request` 参数可选；其他提示词都要求 `request` 非空。提示词消息会引导客户端完成
相应的只读检查、预览、显式确认、应用和验证步骤；提示词本身不会授权任何变更。

即使客户端不会自动读取 MCP 资源或调用提示词，也只能通过 `initialize.instructions`
和单个工具描述获得简短的路由及安全规则。详细能力文本不会追加到初始化消息中，
以避免无关规则进入初始上下文。资源和提示词目录不包含 API key、服务器绝对路径、
客户端本地构建路径、数据库名称或服务器本地类库目录。

## 工具

工具清单由 `tools/list` 返回，包含项目、流程、运行、调试、类库和 API 密钥
管理能力。调试闭环使用 `sereinflow_list_runs` 或
`sereinflow_list_debug_sessions` 发现目标，使用
`sereinflow_start_debug_session` 启动，再通过
`sereinflow_wait_debug_state`、`sereinflow_get_debug_state`、
`sereinflow_continue_debug`、`sereinflow_step_debug` 和
`sereinflow_stop_debug` 控制。启动调试要求 `debug.control` 和
`idempotencyKey`；列表、状态和等待读取接受 `debug.read` 或 `run.read`，
控制命令要求严格递增的 `commandSequence`。

运行查询的 `track` 使用 `development` 或 `production` 字符串；拓扑查询
省略时默认为 `development`。`sereinflow_list_runs` 的可选 `status` 过滤值为
`pending`、`running`、`succeeded`、`failed`、`cancelled`、`timedOut` 或
`interrupted`。

活动运行消息发布使用独立的变更工具
`sereinflow_publish_run_message`，不属于 `debug.control`。工具需要
`run.message.publish` 权限和必填的 `idempotencyKey`，输入的 `payload` 是任意
JSON 值（包括数组、字符串、数值、布尔值和 `null`），`channelKind` 使用稳定的
`queue` 或 `eventBus` 字符串，省略时默认为 `queue`。工具按 `runId` 查找活动运行并执行项目范围检查，
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

MCP API 密钥管理工具仅限管理员使用。使用
`sereinflow_list_mcp_api_keys` 读取当前状态后，再按明确请求调用创建、轮换或
撤销工具；变更操作需要 `idempotencyKey`。项目级密钥必须绑定未归档项目，管理员
密钥不能绑定项目。创建或轮换返回的 secret 只显示一次，不得写入日志、仓库、
提示词或无关工具参数；发生不明确响应时先重新读取密钥列表，不要盲目重试。

创建 key 时 `permissions` 必须使用稳定的点号名称：`project.read`、
`project.write`、`library.read`、`run.read`、`debug.read`、`flow.write`、
`debug.control`、`flow.publish`、`flow.rollback`、`script.compile`、
`library.import`、`library.manage`、`mcp.keys.manage`、`sensitive.read` 或
`run.message.publish`。项目级 key 提供 `projectId` 且不得设置
`isAdministrator: true`；管理员 key 设置 `isAdministrator: true` 且不得提供
`projectId`。

流程修改使用 `sereinflow_preview_flow_patch` 预览，再由显式确认的对应应用工具执行；
v2 请求使用 `schemaVersion: "2.0"`、`op` 判别字段、具名 payload 和规范的 camelCase
枚举值。兼容期仍接受流程公共合同的旧版 v1 输入，但响应始终返回 v2 的
`normalizedOperations` 与
`normalizationWarnings`。

类库节点的必需输入若没有字面量默认值，单独提交 `addNode` 预期会被
`node.missing_required_parameter` 阻止。应在同一个有序 Patch 中同时添加节点
和所有必需的数据连接；连接操作会在最终校验前绑定目标参数。只有组合预览返回
`canApply: true` 时才可执行 Apply。

Apply 成功返回的流程会主动脱敏参数字面量和脚本源码。需要核对实际参数值时，
应在 Apply 后重新读取权威流程，并设置 `includeFlowLiteralValues: true`。
`updateCanvas` 会替换完整画布对象，因此画布改名时必须在载荷中保留原有节点和连接。

`addConnection` 和 `replaceConnection` 的 v2 连接载荷必须同时
提供 `branch` 与 `dataSource`，两者都可以为 `null`。`kind` 与
`dataSource` 是两个独立枚举：执行连线使用
`kind: "execution"`、`branch: "success"|"failure"|"error"`、
`dataSource: null`；数据连线使用 `kind: "data"`、`branch: null`、
`dataSource: "previousNode"`。`dataSource` 不能填写 `"execution"`；
`toPortId` 对数据连线必须是目标参数 ID，而不是 `param-*` 用户界面端口 ID。

类库节点必须先调用只读的
`sereinflow_create_library_node_template`。它只接受已扫描并已附加到项目的真实
Action/Flipflop 合同，返回完整的规范 `NodeDto`、运行时类库元数据、参数端口、默认
字面量、枚举/可变参数元数据、包 SHA-256 和 `contractRevision`。将返回的节点原样
放进 v2 `addNode`；类库 attach/detach 仍是独立的预览/应用操作，不属于流程补丁。

流程补丁还支持 `addNodeParameter` 和 `removeNodeParameter` 进行参数级编辑。使用
`addNodeParameter` 时必须提供完整的参数合同和唯一的 `ui.id`；向可变参数组添加
成员时应使用此 MCP 操作。使用 `removeNodeParameter` 前，必须先移除传入该参数的
数据连线。预览中的操作按顺序执行，因此可以先添加参数，再让 `addConnection` 指向
该参数 ID。

删除节点时，如果 `removeNode` 删除的是当前入口节点，流程补丁会依据补丁完成后的
最终画布内容自动清空 `entryNodeId`。因此删除最后一个节点会得到
`entryNodeId: ""` 的空白编辑草稿，并可正常保存；如果同一补丁最终仍保留相同
ID 的节点，则入口引用会保留。若要改用其他入口节点，请先按连接、节点顺序删除
旧入口，再在同一个有序补丁中使用目标节点 ID 调用 `setEntryNode`。

## 节点绘制约束

绘制前先读取 `sereinflow_get_flow_edit_model`，依据实际节点位置、尺寸、端口、
连接和画布边界布局。每个节点必须有不重叠的 bounding box，并与现有节点、其他
新节点和画布边界保持安全间距。通常约 `260 px` 宽的节点，列间距使用约
`340-420 px`；参数较多时增加间距，横向至少保留 `80 px` 清晰区，分支行至少
保留 `64 px`。

主执行路径从左向右，同一执行阶段对齐；成功、失败、错误分支使用独立行并向
右侧展开。连接尽量短且少交叉；数据连接不能穿过节点主体或端口列表，必要时
使用节点上下方的专用通道。新增节点造成重叠时只移动受影响节点，并在同一个
预览中包含坐标变化；提交预览前检查所有节点是否越界、重叠以及连接
是否穿过节点。

## 安全与诊断

客户端应把已连接的 SereinFlow 服务视为黑盒，只使用 MCP 错误码、结构化
`data`、公开资源和有界诊断。不得通过客户端参数或环境变量改变服务器的
数据库、类库或 staging 路径，也不得搜索服务源码、读取服务器数据库或反编译
上传程序集来解释远程错误。

未预期异常返回 JSON-RPC `InternalError`（`-32603`），带有 `data.code = "mcp.internal_error"`
和 `diagnosticId`；HTTP 工具超时返回 `mcp.tool_timeout` 和同样的诊断 ID。
客户端应将诊断 ID 提供给服务运维人员。
