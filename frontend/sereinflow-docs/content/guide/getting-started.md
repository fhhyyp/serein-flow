# 快速开始

这篇文章只做一件事：在自己的电脑上把 SereinFlow 跑起来。完成后，你会有一个 API 服务和一个可以画流程的网页。

## 需要准备什么

- .NET SDK 10.0.100，或仓库 `global.json` 允许的兼容版本。
- Node.js 24.19.0 和 npm 11.6.2；仓库的 `.node-version` 和控制台 `package.json` 记录了版本。
- SereinFlow 源码。

以下命令都从仓库根目录开始。

## 第一步：启动 API 服务

```powershell
dotnet run --project .\src\SereinFlow.Api\SereinFlow.Api.csproj
```

API 的本地默认地址是 `http://localhost:8188`。用浏览器打开 `http://localhost:8188/healthz`；看到包含 `Healthy` 的响应，就表示服务已启动。

第一次启动会创建本地数据。保持这个终端开着，让 API 继续运行。

## 第二步：启动画流程的网页

打开另一个终端：

```powershell
Set-Location .\frontend\sereinflow-web
npm ci
npm run dev
```

打开终端显示的网页地址，通常是 `http://localhost:5173`。网页会把 `/api` 和 `/hubs` 请求转给本机 API。若 API 不在默认地址，启动网页前设置 `VITE_API_PROXY_TARGET`。

## 第三步：创建一个流程

现在可以去[创建第一个流程](/guide/first-flow)。那里会带你从空画布走到一次成功运行。

::: tip 这个文档站与控制台是两个网页
你正在读的是说明网站；`frontend/sereinflow-web` 才是用来画流程的控制台。
:::
