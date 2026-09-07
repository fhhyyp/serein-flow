# SereinFlow

[English documentation](README.md)

SereinFlow 是一个面向 Web 的工作流编排与执行系统，提供可视化 Web 控制台、
ASP.NET Core API、隔离的 Worker Runner，以及用于 AI 工具集成的 MCP 服务。

## 项目介绍

SereinFlow 用于创建、维护和运行项目中的流程。Web 控制台管理项目、流程、版本、
运行和环境设置；API 协调持久化数据和执行请求；不受信任的脚本与插件只在独立的
Worker Runner 进程中执行。MCP 服务通过受控的工具、资源和提示词，让 AI 客户端
可以安全地读取和操作 SereinFlow。

| 目录 | 说明 |
| --- | --- |
| `src/SereinFlow.Api` | ASP.NET Core API 与 HTTP/stdio MCP 宿主。 |
| `src/SereinFlow.SDK` | 面向第三方程序的类型安全 HTTP 客户端 SDK。 |
| `frontend/sereinflow-web` | Vue + Vite Web 控制台。 |
| `plugins/sereinflow-ai-toolkit` | 本机 MCP 客户端与 Skill 源材料。 |
| `docs` | 长期维护的技术参考资料。 |

## 社区与技术支持

如果你在使用 SereinFlow 时遇到问题，欢迎加入 QQ 群 **955830545** 联系我，获取技术支持、交流使用经验或反馈问题。

## 环境要求

- .NET SDK `10.0.100`，或由 [`global.json`](global.json) 选择的兼容 SDK。
- [`.node-version`](.node-version) 所定义的 Node.js `24.19.0`，以及 npm
  `11.6.2`。
- 下方的环境变量与 Codex 配置说明以 Windows 为例；API 与前端命令本身使用标准
  的 .NET 和 npm 工作流。

使用 SereinLang 的项目依赖 `src/ThirdParty/SereinScript` 中已固定版本的源码快照。
正常构建 SereinFlow 无需额外检出 SereinScript 仓库，也无需设置其路径环境变量。

## 本地启动

### 1. 启动 API

在仓库根目录运行：

```powershell
dotnet run --project .\src\SereinFlow.Api\SereinFlow.Api.csproj
```

默认情况下，API 和 HTTP MCP 服务会在 `http://127.0.0.1:8188` 提供服务，MCP
端点为 `http://127.0.0.1:8188/mcp`。首次启动会在配置的 `data` 目录下初始化本地
数据。

### 2. 启动前端

打开第二个终端并运行：

```powershell
Set-Location .\frontend\sereinflow-web
npm ci
npm run dev
```

在浏览器中打开 Vite 输出的地址，通常为 `http://localhost:5173`。开发服务器默认会
将 `/api` 和 `/hubs` 请求代理到 `http://127.0.0.1:8188`。如 API 使用其他地址，
启动前端前设置 `VITE_API_PROXY_TARGET`。

## 配置 MCP 服务

以下流程为本机运行的 SereinFlow 配置 HTTP MCP 客户端。请在 API 与前端均已启动后
继续操作。

### 1. 生成管理员密钥

在 Web 控制台主页菜单中打开“环境设置”，找到“MCP 访问密钥”，然后选择“生成首个
密钥”。保存显示的管理员密钥；其格式通常为 `sfk_...`，完整 Secret 只会在创建或
轮换时显示一次。

### 2. 创建环境变量

在 Windows 的“编辑用户环境变量”界面新增用户变量，名称为
`SEREINFLOW_MCP_API_KEY`，值为刚生成的完整密钥。也可以在 PowerShell 中执行以下
命令；请将示例值替换为实际密钥：

```powershell
setx SEREINFLOW_MCP_API_KEY "sfk_replace_with_your_complete_secret"
```

执行 `setx` 或保存系统设置后，关闭并重新打开终端和 AI 工具。已有进程不会自动获得
新变量。

### 3. 为本机 AI 工具制作 Skill

让所使用的 AI 工具根据
[`plugins/sereinflow-ai-toolkit`](plugins/sereinflow-ai-toolkit) 目录中的内容创建适用于
本机的 Skill。该目录中的 [`.mcp.json`](plugins/sereinflow-ai-toolkit/.mcp.json) 定义
本机 HTTP MCP 地址和 `SEREINFLOW_MCP_API_KEY` 凭据变量；
[`SKILL.md`](plugins/sereinflow-ai-toolkit/skills/sereinflow/SKILL.md) 定义连接、
诊断、能力发现和安全操作规则。

制作 Skill 时保留以下连接约定，不要将实际密钥写入仓库、Skill 文件或日志：

```json
{
  "mcpServers": {
    "sereinflow": {
      "type": "http",
      "url": "http://127.0.0.1:8188/mcp",
      "bearer_token_env_var": "SEREINFLOW_MCP_API_KEY"
    }
  }
}
```

### 4. 验证 MCP 服务

先确认路由可达。API 正常启动后，下面命令预期输出 `405`；这表示 `/mcp` 路由已
监听，而 MCP 请求必须使用 `POST`。

```powershell
try {
  Invoke-WebRequest -Method Get http://127.0.0.1:8188/mcp -ErrorAction Stop
} catch {
  [int]$_.Exception.Response.StatusCode
}
```

然后在 AI 工具中新建会话，连接 `sereinflow` MCP 服务，并执行能力发现：
`tools/list`、`resources/list`、`resources/templates/list` 和 `prompts/list`。
成功完成初始化并返回这些目录，即表示 MCP 鉴权和服务工作正常。

- 返回 `401`：地址与路由通常可达，但密钥缺失、无效、过期或已撤销。
- 连接被拒绝或超时：检查 API 是否正在监听 `127.0.0.1:8188`。

### 5. 在 Codex 中使用 MCP

Codex 默认的安全策略会阻止环境变量传递到它启动的 Shell/命令行环境。若要在 Codex
中使用 SereinFlow MCP，请在 `%USERPROFILE%\.codex\config.toml` 添加以下配置。
如果该配置表已存在，只添加变量行，不要重复声明表：

```toml
[shell_environment_policy.filters]
SEREINFLOW_MCP_API_KEY = "include"
```

完全重启 Codex 后再启动新任务。此规则允许 Codex 启动的 Shell 接收密钥变量；MCP
HTTP 客户端仍需使用上节所示的 `bearer_token_env_var` 配置读取该变量。

### 6. 完成

当 AI 工具可以完成初始化并发现 MCP 工具、资源和提示词时，本机 MCP 配置即已完成。

## 本地构建与验证

```powershell
dotnet restore SereinFlow.sln
dotnet build SereinFlow.sln --no-restore
dotnet test SereinFlow.sln --no-build

Set-Location frontend/sereinflow-web
npm ci
npm run build
```

需要验证上传库的完整发布布局时，可发布
[`tests/SereinFlow.TestLibrary`](tests/SereinFlow.TestLibrary/README.md)。该命令会创建
`artifacts/libraries/SereinFlow.TestLibrary-1.6.1.zip`，其中包含完整发布输出和所需的
归档结构：

```powershell
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
```

## 文档

- [文档索引](docs/zh-CN/README.md)
- [MCP 服务参考](docs/zh-CN/mcp-readonly-server.md)
- [.NET 客户端 SDK](docs/zh-CN/client-sdk.md)
- [Worker 协议（v2 当前、v1 历史兼容）](docs/zh-CN/worker-protocol.md)
- [English documentation index](docs/en/README.md)

## 安全说明

MCP 密钥是凭据，不是项目配置。不要将 `sfk_...` 密钥提交到 Git、写入 `.env` 示例、
测试数据、Skill 文件、提示词或日志。密钥泄露、遗失或不再使用时，请在“环境设置”中
轮换或撤销它。
