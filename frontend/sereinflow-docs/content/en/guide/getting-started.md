# Quick start

This guide gets SereinFlow running on your computer. At the end, you will have an API service and a web console for drawing flows.

## Prerequisites

- .NET SDK 10.0.100, or a compatible version allowed by the repository's `global.json`.
- Node.js 24.19.0 and npm 11.6.2, as recorded in `.node-version` and the console's `package.json`.
- The SereinFlow source code.

Run the following commands from the repository root.

## 1. Start the API service

```powershell
dotnet run --project .\src\SereinFlow.Api\SereinFlow.Api.csproj
```

The default local API address is `http://localhost:8188`. Open `http://localhost:8188/healthz`; a response containing `Healthy` means the service started. The first run creates local data. Keep this terminal open.

## 2. Start the web console

In another terminal:

```powershell
Set-Location .\frontend\sereinflow-web
npm ci
npm run dev
```

Open the address shown by the terminal, usually `http://localhost:5173`. The console proxies `/api` and `/hubs` to the local API. If the API uses a different address, set `VITE_API_PROXY_TARGET` before starting the console.

## 3. Create a flow

Continue with [Create your first flow](/en/guide/first-flow), which takes you from an empty canvas to a successful run.

::: tip The documentation and console are separate sites
You are reading the documentation site. `frontend/sereinflow-web` is the console for drawing flows.
:::
