# SereinFlow

SereinFlow is a web-first workflow orchestration system. The ASP.NET Core API coordinates project data and execution requests, while untrusted scripts and plug-ins run only in isolated Worker Runner processes.

The refactoring architecture, approved scope, and incremental delivery plan are documented in [docs/sereinflow-refactor](docs/sereinflow-refactor/README.md).

## Local verification

```powershell
dotnet restore SereinFlow.sln
dotnet build SereinFlow.sln --no-restore
dotnet test SereinFlow.sln --no-build

Set-Location frontend/sereinflow-web
pnpm install --frozen-lockfile
pnpm build
```

Use .NET SDK `10.0.300`, Node.js `24.19.0`, and pnpm `11.19.0`. `global.json` and `.node-version` are the canonical local and CI toolchain definitions.
