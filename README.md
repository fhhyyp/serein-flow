# SereinFlow

SereinFlow is a web-first workflow orchestration system. The ASP.NET Core API coordinates project data and execution requests, while untrusted scripts and plug-ins run only in isolated Worker Runner processes.

The versioned API-to-Worker IPC contract is documented in [docs/worker-protocol-v1.md](docs/worker-protocol-v1.md).

## Local verification

```powershell
dotnet restore SereinFlow.sln
dotnet build SereinFlow.sln --no-restore
dotnet test SereinFlow.sln --no-build

Set-Location frontend/sereinflow-web
npm ci
npm run build
```

Use .NET SDK `10.0.300`, Node.js `24.19.0`, and npm `11.6.2`. `global.json` and `.node-version` are the canonical local and CI toolchain definitions.

Projects that include SereinLang integration require an explicit SereinScript checkout root. Set the MSBuild property or environment variable in the build environment; no developer-machine path is inferred:

```powershell
dotnet build src\SereinFlow.McpServer\SereinFlow.McpServer.csproj --no-restore -p:SereinScriptSourceRoot='D:\path\to\SereinScript'
# or set SEREINFLOW_SEREINSCRIPT_SOURCE_ROOT before building
```

## Upload-library smoke test

The repository includes [`tests/SereinFlow.TestLibrary`](tests/SereinFlow.TestLibrary/README.md), a dependency-free class library that exercises the upload endpoint and PE metadata node scanner. Build it to create `artifacts/libraries/SereinFlow.TestLibrary-1.0.0.zip` with the required archive layout:

```powershell
dotnet build tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj
```
