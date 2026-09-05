# SereinFlow

[中文文档 | Chinese documentation](README.zh-CN.md)

SereinFlow is a web-first workflow orchestration and execution system. It
combines a visual web console, an ASP.NET Core API, isolated Worker Runner
processes, and an MCP service for AI-tool integration.

## Project Overview

SereinFlow is used to create, maintain, and run project workflows. The web
console manages projects, flows, versions, runs, and environment settings. The
API coordinates persisted data and execution requests, while untrusted scripts
and plugins run only in separate Worker Runner processes. The MCP service
exposes controlled tools, resources, and prompts so AI clients can work with
SereinFlow safely.

| Path | Purpose |
| --- | --- |
| `src/SereinFlow.Api` | ASP.NET Core API and the HTTP/stdio MCP host. |
| `frontend/sereinflow-web` | Vue + Vite web console. |
| `plugins/sereinflow-ai-toolkit` | Local MCP client and Skill source material. |
| `docs` | Maintained technical references. |

## Prerequisites

- .NET SDK `10.0.100` or a compatible SDK selected by
  [`global.json`](global.json).
- Node.js `24.19.0`, as defined in [`.node-version`](.node-version), and npm
  `11.6.2`.
- Windows is assumed for the environment-variable and Codex instructions
  below. The API and frontend commands are otherwise standard .NET and npm
  commands.

Projects that use SereinLang rely on the pinned source snapshot under
`src/ThirdParty/SereinScript`. A normal SereinFlow build needs neither a
separate SereinScript checkout nor a path environment variable.

## Run Locally

### 1. Start the API

From the repository root, run:

```powershell
dotnet run --project .\src\SereinFlow.Api\SereinFlow.Api.csproj
```

By default, the API and HTTP MCP service listen on `http://127.0.0.1:8188`,
with MCP available at `http://127.0.0.1:8188/mcp`. The first start initializes
local data under the configured `data` directory.

### 2. Start the Web Console

Open a second terminal and run:

```powershell
Set-Location .\frontend\sereinflow-web
npm ci
npm run dev
```

Open the URL printed by Vite, usually `http://localhost:5173`. The development
server proxies `/api` and `/hubs` to `http://127.0.0.1:8188` by default. Set
`VITE_API_PROXY_TARGET` before starting the frontend when the API uses another
address.

## Configure the MCP Service

The following workflow configures an HTTP MCP client for a locally running
SereinFlow instance. Start both the API and the web console before continuing.

### 1. Generate the Administrator Key

In the web console's home menu, open **Environment settings**, find **MCP
access keys**, and select **Generate first key**. Save the administrator key
shown there. It normally begins with `sfk_...`, and the complete Secret is
shown only once when it is created or rotated.

### 2. Add the Environment Variable

In Windows **Edit environment variables for your account**, add a user variable
named `SEREINFLOW_MCP_API_KEY` whose value is the complete key you just
generated. Alternatively, run this PowerShell command, replacing the example
value with the real key:

```powershell
setx SEREINFLOW_MCP_API_KEY "sfk_replace_with_your_complete_secret"
```

After running `setx` or saving the system setting, close and reopen terminals
and AI tools. Existing processes do not receive the new variable automatically.

### 3. Create a Local Skill for Your AI Tool

Ask the AI tool you use to create a local Skill from the contents of
[`plugins/sereinflow-ai-toolkit`](plugins/sereinflow-ai-toolkit). Its
[`.mcp.json`](plugins/sereinflow-ai-toolkit/.mcp.json) defines the local HTTP
MCP endpoint and the `SEREINFLOW_MCP_API_KEY` credential variable, while
[`SKILL.md`](plugins/sereinflow-ai-toolkit/skills/sereinflow/SKILL.md) defines
the connection, diagnostics, capability-discovery, and safe-operation rules.

Preserve the following connection contract when creating the Skill. Never put
the real key in the repository, a Skill file, or logs:

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

### 4. Verify the MCP Service

First confirm that the route is reachable. With the API running, the following
command is expected to print `405`. That means the `/mcp` route is listening;
MCP requests must use `POST`.

```powershell
try {
  Invoke-WebRequest -Method Get http://127.0.0.1:8188/mcp -ErrorAction Stop
} catch {
  [int]$_.Exception.Response.StatusCode
}
```

Then start a new AI-tool session, connect to the `sereinflow` MCP server, and
discover `tools/list`, `resources/list`, `resources/templates/list`, and
`prompts/list`. A successful initialization and returned catalogs confirm that
MCP authentication and the service are working.

- A `401` normally means the endpoint and route are reachable but the key is
  missing, invalid, expired, or revoked.
- Connection refusal or a timeout means the API is not reachable at
  `127.0.0.1:8188`.

### 5. Complete Setup

Local MCP setup is complete once the AI tool can initialize and discover the
MCP tools, resources, and prompts.

## Use SereinFlow MCP in Codex

Codex's default safety policy can block this environment variable from being
passed to Shell and command-line environments it launches. To use the
SereinFlow MCP service in Codex, add the following to
`%USERPROFILE%\.codex\config.toml`. If this table already exists, add only the
variable line and do not declare the table twice:

```toml
[shell_environment_policy.filters]
SEREINFLOW_MCP_API_KEY = "include"
```

Fully restart Codex and start a new task. This rule allows Codex-launched
Shells to receive the variable; the MCP HTTP client must still use the
`bearer_token_env_var` configuration shown above to read it.

## Build and Verify Locally

```powershell
dotnet restore SereinFlow.sln
dotnet build SereinFlow.sln --no-restore
dotnet test SereinFlow.sln --no-build

Set-Location frontend/sereinflow-web
npm ci
npm run build
```

To verify the full publish layout for an upload library, publish
[`tests/SereinFlow.TestLibrary`](tests/SereinFlow.TestLibrary/README.md). The
command creates `artifacts/libraries/SereinFlow.TestLibrary-1.6.1.zip` with the
complete publish output and required archive layout:

```powershell
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
```

## Documentation

- [Documentation index](docs/en/README.md)
- [MCP service reference](docs/en/mcp-readonly-server.md)
- [Worker protocol v2](docs/en/worker-protocol-v2.md)
- [Worker protocol v1 (historical)](docs/en/worker-protocol-v1.md)
- [中文文档索引 | Chinese documentation index](docs/zh-CN/README.md)

## Security Notes

An MCP key is a credential, not project configuration. Do not commit an
`sfk_...` key to Git or place it in `.env` examples, test data, Skill files,
prompts, or logs. Rotate or revoke a key from **Environment settings** when it
is exposed, lost, or no longer needed.
