# SereinFlow MCP Server

## 状态

MCP Server 位于 `src/SereinFlow.McpServer`，提供本地 stdio 和可选的 Streamable HTTP 两种传输。它复用 `AiReadModelService`，不直接访问数据库记录，不启动流程 Worker。流程修改、发布、回滚、SereinLang 编译和类库导入都经过权限检查、预览确认、幂等控制和审计。

## 启动

先构建 Server，再在仓库根目录执行：

```text
dotnet build src/SereinFlow.McpServer/SereinFlow.McpServer.csproj --no-restore
dotnet run --project src/SereinFlow.McpServer/SereinFlow.McpServer.csproj --no-build --no-restore

# 远程 Streamable HTTP
dotnet run --project src/SereinFlow.McpServer/SereinFlow.McpServer.csproj --no-build --no-restore -- --http
```

MCP 客户端配置必须使用 `--no-build`，避免 `dotnet run` 的构建日志混入标准输出。生产部署也可以直接使用构建后的 `SereinFlow.McpServer.dll` 运行。

stdio 模式从标准输入读取 JSON-RPC 消息，将响应写入标准输出。HTTP 模式默认监听 `http://127.0.0.1:5187/mcp`，要求 `Authorization: Bearer <api-key>`，并通过 `Mcp-Session-Id` 绑定 MCP 会话。日志和错误只写入标准错误。数据库和类库目录通过现有配置键读取：

```text
SereinFlow__DatabasePath
SereinFlow__LibraryDirectory
```

HTTP 默认不启用跨域请求。需要由浏览器或跨域代理访问时，可显式配置逗号分隔的来源列表：

```text
SereinFlow__Mcp__Http__AllowedOrigins=https://automation.example.com,https://admin.example.com
```

只有通过该配置明确列出的 `http`/`https` 来源会获得 CORS 响应头；TLS 证书和反向代理仍由宿主或部署层配置。

使用 MCP 客户端配置时，应将 `command` 设为 `dotnet`，将项目路径、`--no-build` 和 `--no-restore` 作为参数，并通过客户端支持的环境变量配置数据库路径。不同版本的 Codex、OpenCode 或其他 MCP 客户端配置键名可能不同，具体以客户端文档为准。

一个使用仓库绝对路径的 stdio 配置值如下。配置文件的外层字段名按客户端要求调整：

```json
{
  "command": "dotnet",
  "args": [
    "run",
    "--project",
    "D:/Project/dotnet/SereinFlow/src/SereinFlow.McpServer/SereinFlow.McpServer.csproj",
    "--no-build",
    "--no-restore"
  ],
  "env": {
    "SereinFlow__DatabasePath": "D:/Project/dotnet/SereinFlow/data/sereinflow.db",
    "SereinFlow__LibraryDirectory": "D:/Project/dotnet/SereinFlow/data/libraries"
  }
}
```

如果客户端支持 MCP 的标准 stdio Server 配置，可以将上述值放入 `sereinflow` Server 条目，例如：

```json
{
  "servers": {
    "sereinflow": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:/Project/dotnet/SereinFlow/src/SereinFlow.McpServer/SereinFlow.McpServer.csproj",
        "--no-build",
        "--no-restore"
      ],
      "env": {
        "SereinFlow__DatabasePath": "D:/Project/dotnet/SereinFlow/data/sereinflow.db",
        "SereinFlow__LibraryDirectory": "D:/Project/dotnet/SereinFlow/data/libraries"
      }
    }
  }
}
```

## Resources

静态资源：

```text
sereinflow://projects
sereinflow://libraries
```

资源模板：

```text
sereinflow://projects/{projectId}
sereinflow://projects/{projectId}/flows/{flowId}/topology
sereinflow://libraries/{libraryId}
sereinflow://runs/{runId}
sereinflow://debug-sessions/{sessionId}
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}
sereinflow://mcp-previews/{previewId}
```

## Tools

```text
sereinflow_list_projects
sereinflow_preview_create_project
sereinflow_apply_create_project
sereinflow_get_project
sereinflow_get_flow_topology
sereinflow_list_libraries
sereinflow_get_library
sereinflow_get_run_inspection
sereinflow_get_debug_state
sereinflow_wait_debug_state
sereinflow_get_flow_edit_model
sereinflow_preview_flow_patch
sereinflow_apply_flow_patch
sereinflow_compare_flow_versions
sereinflow_preview_publish_flow
sereinflow_apply_publish_flow
sereinflow_preview_rollback_flow
sereinflow_apply_rollback_flow
sereinflow_compile_sereinlang
sereinflow_preview_library_package
sereinflow_apply_library_package
sereinflow_preview_project_library_attach
sereinflow_apply_project_library_attach
sereinflow_list_mcp_api_keys
sereinflow_create_mcp_api_key
sereinflow_revoke_mcp_api_key
sereinflow_rotate_mcp_api_key
```

所有结果默认受 AI 只读模型的数量、JSON 大小和脚本源码策略限制。`includeScriptSource` 和 `includeFlowLiteralValues` 必须由调用方显式传入才会启用。

stdio 默认用于本机受信任进程，并使用本地管理员主体。受控部署可以配置 `SereinFlow:Mcp:Stdio:ApiKey`（或 `SereinFlow:Mcp:StdioApiKey`）；配置后 stdio 会先验证该 Key，并使用其项目范围和权限，不再自动授予管理员权限。

## 当前边界

- C# 源码生成、项目创建和 DLL 编译由用户本地 VS/.NET 工具链完成，服务端不执行 `dotnet build`、MSBuild 或用户构建脚本。
- 类库 Tool 只接收符合 `[类库名称]-[版本号].zip` 规则的已构建 ZIP，并在受控目录中校验、PE 扫描、分析和导入，不加载或执行上传程序集。
- 所有状态变更 Tool 都必须先预览，再使用 `APPLY`、预览指纹和幂等键确认；类库项目引用与类库工件导入是两个独立操作。
- 创建项目使用 `sereinflow_preview_create_project` 和 `sereinflow_apply_create_project`，需要管理员主体和 `project.write` 权限；预览会生成一个空的 Draft 项目及 `main` 流程。
- `project.write` 仅用于创建项目，创建操作仍要求管理员主体；项目级 Key 不能借此创建其他项目。
- HTTP 默认只监听本机地址；远程监听、TLS、反向代理和 CORS 必须通过显式部署配置启用。
