# SereinFlow MCP

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

开发配置的 MCP 地址是 `http://127.0.0.1:5178/mcp`。请求必须携带：

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
      "url": "http://127.0.0.1:5178/mcp"
    }
  }
}
```

API key 应由 MCP 客户端的安全凭据、外部密钥注入或凭据存储提供。不要在仓库、
插件配置、日志或流程内容中写入 key。

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
these separate server Resources; they are not copied into the client plugin.

Each Resource is loaded from the server deployment at every read, so an
operator can update one Markdown file without rebuilding or reinstalling the
client plugin. The backing files are selected only by server configuration and
are constrained to remain under the server ContentRoot. The MCP caller
supplies only fixed Resource URIs and cannot select an arbitrary local file.
The default files are `mcp/sereinflow-ai-guide.md`,
`mcp/sereinflow-skill.md`, `mcp/sereinlang-skill.md`, and
`mcp/sereinflow-library-package-skill.md`. Deployments can override the four
paths with `SereinFlow:Mcp:AiGuidance:FilePath`,
`SereinFlow:Mcp:AiGuidance:SereinFlowFilePath`,
`SereinFlow:Mcp:AiGuidance:SereinLangFilePath`, and
`SereinFlow:Mcp:AiGuidance:LibraryPackageFilePath`; each file uses the shared
`SereinFlow:Mcp:AiGuidance:MaxBytes` limit.

```text
sereinflow://projects
sereinflow://libraries
sereinflow://projects/{projectId}
sereinflow://projects/{projectId}/flows/{flowId}/topology
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}
sereinflow://libraries/{libraryId}
sereinflow://runs/{runId}
sereinflow://debug-sessions/{sessionId}
sereinflow://mcp-previews/{previewId}
```

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
管理能力。流程修改使用 `sereinflow_preview_flow_patch` 后再由显式确认的
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
