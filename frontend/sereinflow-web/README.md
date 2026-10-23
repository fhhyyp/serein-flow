# SereinFlow web workspace

Vue 3 and TypeScript frontend for the SereinFlow flow editor, debugger, and run console.
SereinFlow 流程编辑、调试与运行控制台前端，使用 Vue 3 和 TypeScript。

Use Node.js 24.19 or later within the 24.x release line and npm 11.6.2 or later within 11.x.
使用 Node.js 24.19 及以上的 24.x 版本，以及 npm 11.6.2 及以上的 11.x 版本。

From this directory / 在本目录执行：

```powershell
npm ci
npm run dev
```

The development server proxies `/api` and `/hubs` to `http://127.0.0.1:8188`.
Set `VITE_API_PROXY_TARGET` before starting it to use another backend address.
开发服务器将 `/api` 和 `/hubs` 转发到 `http://127.0.0.1:8188`，
启动前可设置 `VITE_API_PROXY_TARGET` 指定其他后端地址。

```powershell
npm test
npm run build
```

The build checks TypeScript and writes deployable output to `dist/`.
构建会检查 TypeScript，并将部署产物写入 `dist/`。

See the repository [README](../../README.md) / [中文说明](../../README.zh-CN.md)
for backend startup and authentication setup.
后端启动与鉴权配置请参阅仓库说明。
