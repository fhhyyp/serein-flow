# SereinFlow ASP.NET Core 宿主封装开发方案

状态：实施中。宿主提取、组合入口、认证默认值、资源嵌入和本地包消费已完成 Windows 验证；Linux CI、跨平台发布矩阵和长期兼容性收尾仍待完成。稳定的用户用法已迁入 `docs/zh-CN` 与 `docs/en`。

## 1. 目标与完成定义

让一个全新的 .NET 10 ASP.NET Core Web 项目，安装 SereinFlow 宿主包后，用少量显式代码启动完整的 SereinFlow REST API、Application 服务、持久化、Worker 执行、SignalR 和可选的 HTTP MCP 服务。首版不提供默认类库注册；使用者通过现有 REST/MCP 能力导入类库。SereinFlow 官方宿主继续使用同一套注册和映射入口，不再维护第二份启动逻辑。

目标用法：

```csharp
using SereinFlow.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi();
    options.UseMcp();
});

var app = builder.Build();
app.UseSereinFlow();
app.MapSereinFlow();
app.Run();
```

`AddSereinFlow` 负责服务和配置；`UseSereinFlow` 负责必需的 HTTP 中间件；`MapSereinFlow` 负责端点。调用 `AddSereinFlow` 本身不能使 ASP.NET Core 自动映射 Controller、Hub 和 `/mcp`。两个运行时调用作为公开契约保留，避免隐藏式端点注入。

完成的判定必须同时满足：全新项目仅依赖发布的包即可编译、运行和发布；`/healthz`、REST、Hub、`/mcp` 按选项出现；至少一个流程在独立 Worker 内完成；MCP 可完成认证、`initialize`、`tools/list`、`resources/list` 与 `resources/read`；通过现有 REST/MCP 接口导入的类库可被目录读取且其节点在 Worker 中执行；官方宿主现有测试通过。

## 2. 当前基线与不可改变的边界

- `src/SereinFlow.Api/Program.cs` 目前手工组合 Storage、Application、Web API、MCP、Execution、Kestrel 限额和端点。
- `src/SereinFlow.Api/SereinFlowWebApiExtensions.cs` 中的 Web API 扩展类型是 `internal`，也没有显式指定 SereinFlow Controller 所在程序集；外部入口无法直接复用。
- `src/SereinFlow.Mcp/SereinFlowMcpRegistration.cs` 已公开 `AddSereinFlowMcp` 和 `MapSereinFlowMcp`，但依赖共享的 Storage、Application 与 Execution 图。
- `src/SereinFlow.Api/SereinFlowExecutionRegistration.cs` 在服务注册时解析 Worker Runner 路径；当前源码树搜索不构成外部应用的发布契约。
- `src/SereinFlow.Infrastructure/Configuration/SereinFlowStorageOptions.cs` 解析配置时创建目录；`AddSereinFlowStorage` 随后立即迁移 SQLite。封装时需要确定初始化时机、错误传播和重复注册语义。
- `src/SereinFlow.Mcp/McpAiGuidance.cs` 默认从宿主 ContentRoot 的 `mcp/*.md` 读取引导资源；NuGet 消费者未必拥有这些文件。
- 上传类库只在 API 进程扫描 ZIP/PE 元数据；实际程序集、服务和 Native 依赖由独立 Worker 加载。`AddFlowLibrary<TNode>()` 若对第三方节点类型直接求 `typeof(TNode)`，就会把节点程序集载入 API 进程，因此不能作为普通类库注册实现。
- 官方 `--mcp-stdio` 入口已有独立的进程、鉴权及 stdout 约束。本方案的 `UseMcp()` 指 HTTP MCP；stdio 保持现有入口，后续再独立设计通用宿主 API。

## 3. 公开 API 契约

### 3.1 选项与功能组合

```csharp
public static IServiceCollection AddSereinFlow(
    this IServiceCollection services,
    Action<SereinFlowHostBuilder> configure);

public sealed class SereinFlowHostBuilder
{
    public SereinFlowHostBuilder UseWebApi(Action<SereinFlowWebApiOptions>? configure = null);
    public SereinFlowHostBuilder UseMcp(Action<SereinFlowHttpMcpOptions>? configure = null);
}

public static WebApplication UseSereinFlow(this WebApplication app);
public static WebApplication MapSereinFlow(this WebApplication app);
```

服务扩展不要求调用方传入 `IConfiguration` 或 ContentRoot。注册描述信息先写入 DI；在宿主构建/启动阶段通过 `IConfiguration`、`IHostEnvironment` 和已验证的选项解析路径。将当前同步文件创建、数据库迁移和 Runner 查找从 `AddSereinFlow` 调用点转入有顺序的启动初始化。初始化失败应阻止宿主开始提供端点，并给出明确路径/配置诊断。

功能语义：

| 选择 | 注册和映射 | 不映射 |
| --- | --- | --- |
| `UseWebApi()` | SereinFlow Controller、`/healthz`、两个 SignalR Hub，以及其应用与执行依赖 | `/mcp`，除非同时选择 `UseMcp()` |
| `UseMcp()` | HTTP MCP `/mcp`，以及 MCP 工具所需的应用、存储和执行依赖 | REST Controller 与 Hub，除非同时选择 `UseWebApi()` |
| 两者同时调用 | 一套共享服务、数据库、Worker 队列与后台执行服务 | 重复的数据库或执行器实例 |

没有选择任何对外能力时，在构建/启动时以可读错误拒绝配置。重复调用 `UseWebApi`、`UseMcp` 或 `AddSereinFlow` 的语义固定为“拒绝重复调用并指出来源”，避免重复 HostedService、路由和 CORS Policy。未来若需要增量组合，另行定义明确的合并规则。

`MapSereinFlow` 仅映射已启用的功能；允许与宿主自己的 Controller 和 Minimal API 共存。MVC 的 `MapControllers()` 会映射宿主已注册的属性路由 Controller，文档需说明这一 ASP.NET Core 共享 MVC 行为；新增的 SereinFlow Controller 必须通过 `AddApplicationPart(typeof(AssemblyMarker).Assembly)` 显式发现。检测 SereinFlow 自身的已知路由冲突并在启动时报告。

`UseSereinFlow` 使用命名 CORS 策略，不设置放行所有来源的默认策略；不能更改宿主现有的全局 CORS 和认证策略。Controller 异常应使用专属异常处理机制或明确要求宿主配置全局异常处理，不能悄悄替换宿主的全局处理逻辑。明确记录 `UseSereinFlow()` 与宿主 `UseAuthentication()`、`UseAuthorization()` 的顺序，并通过集成测试固定。

### 3.2 配置与安全默认值

保留已有 `SereinFlow:*` 配置键，包括 `DataRoot`、`WorkerRunnerPath`、`RunExecution`、`Mcp` 和 `Api:RequireAuthentication`。相对路径以宿主 ContentRoot 解析；打包 Runner 默认以 `AppContext.BaseDirectory/sereinflow/worker` 解析。配置的 `WorkerRunnerPath` 优先且必须指向存在的受支持文件，不再向上搜索外部项目附近的源码树。开发仓库模式可在官方宿主中显式配置源码 Runner 路径。

新宿主的认证默认值定为：MCP 始终要求密钥，REST 默认 `RequireAuthentication=true`。REST 现有的 `false` 行为仅可由官方宿主显式配置以保持兼容。发布前全面审计每个 REST 写入和敏感读取端点，确保开启该选项时确实受保护；单个选项不能被误描述为覆盖全部端点的统一授权策略。首次 MCP key 创建仍走既有受控 bootstrap 流程，不自动生成、打印或提交密钥。

REST、MCP 上传体大小、`FormOptions` 与 Kestrel 限额必须匹配；如果宿主已有更严格的 Kestrel 限额，应给出启动警告或配置错误，而非悄悄降低宿主保护。文档说明反向代理限额需要单独配置。Swagger 属于 `UseWebApi` 的可选项，默认仅开发环境启用，并可关闭或更改路径；不把 MCP/Hub 错标为 REST OpenAPI。

### 3.3 首版类库范围

首版的 `SereinFlowHostBuilder` 不提供 `AddFlowLibraryPackage(path)`、`AddFlowLibrary<T>()` 或任何启动时自动导入类库的选项。干净项目启动后，全局类库目录可以为空；用户继续通过现有 REST 上传接口或 MCP 类库导入工具提交符合 ZIP 契约的制品，再由项目显式引用该制品。封装工作只需确保这些既有能力、类库目录和 Worker 执行路径在外部宿主中正常运行。宿主启动不因缺少默认类库而失败。

若后续加入 `AddFlowLibrary<T>()`，需另行决定 `T` 的语义。直接以第三方 `[FlowLibrary]` 节点类型求 `typeof(T)` 会将节点程序集载入 API 进程，违反当前隔离边界；安全的泛型描述符、制品定位、发布资产和幂等导入规则留待后续设计，不进入首版交付与验收。

## 4. 代码与包布局

建议新增 `src/SereinFlow.AspNetCore`，作为对外 NuGet 门面。将目前 `SereinFlow.Api` 内可复用的 Controller、Hub、执行服务、注册扩展、健康接口和相关辅助代码迁入该库；`src/SereinFlow.Api` 保留 `Program.cs`、stdio 进程入口及官方宿主配置。原有 API 集成测试继续覆盖官方宿主，新加外部宿主测试直接引用门面。迁移后检查 `ApiIsolationTests` 的程序集隔离断言，将其指向新的承载程序集。

第一步允许仓库内 `ProjectReference` 消费，以验证公开 API、依赖图和实际端点。第二步完成 NuGet 发布；当前 `SereinFlow.Mcp` 等项目的 `IsPackable=false`，并且 SereinScript 源码快照仍处于项目引用链中。打包工作必须梳理整个传递引用闭包，给需要发布的程序集统一 `PackageId`、版本、依赖和目标框架；不能只给门面项目打开 `IsPackable`。在本地临时 NuGet feed 中执行真正的“空项目 `PackageReference`、restore、publish”验证。`SereinFlow.SDK` 仍是客户端包，和宿主包保持名称及用途区分。

Worker Runner 采用明确的部署契约：在打包阶段从 `SereinFlow.Worker.Runner` 的 `dotnet publish` 输出生成独立运行目录，保留 `.dll`、`.deps.json`、`.runtimeconfig.json`、托管依赖和必要 Native 文件；通过 NuGet 的构建资产将其复制到宿主 build/publish 输出的 `sereinflow/worker/`。先验证无 RID 的框架依赖发布，若某些 Native 包需要 RID，再提供相应 RID 发布矩阵。宿主默认只从这个确定位置启动 Runner，并在启动时验证版本/协议与文件完整性；自定义路径作为高级配置保留。不要依赖现有的 `BuildWorkerRunner` 源码工程 Target 来支撑 NuGet 消费者。

MCP 引导 Markdown 随门面包提供默认资源。推荐把默认内容嵌入程序集，并保持配置指定的宿主 `mcp/*.md` 文件作为显式覆盖；由 `McpAiGuidanceProvider` 统一读取，维持原有 URI、大小限制和动态覆盖语义。官方宿主原有文件部署继续可用。包验证必须在外部应用中读取 `sereinflow://ai/guide` 和至少一个模块资源，不能仅检查 `resources/list`。

## 5. 实施任务与依赖顺序

| 阶段 | 具体改动 | 可独立验收的结果 |
| --- | --- | --- |
| A. 宿主提取 | 新建 `SereinFlow.AspNetCore`；移动 Web API/Hub/Execution 可复用代码；增加 MVC ApplicationPart；官方 `Program.cs` 暂用提取后的底层注册 | 官方 REST、Hub、MCP 路由与旧行为一致；架构测试通过 |
| B. 启动生命周期 | 延迟存储目录/SQLite 初始化；启动验证选项与 Runner；确保初始化先于队列、调试和重索引 HostedService | 无配置副作用发生在 `AddSereinFlow` 调用期；错误在监听前确定性失败 |
| C. 组合 API | 实现 `UseWebApi`、`UseMcp`、`UseSereinFlow`、`MapSereinFlow`；收敛 JSON、CORS、上传限额和 Swagger；官方 `Program.cs` 切换到门面 | Web-only、MCP-only、组合模式分别通过内存宿主测试 |
| D. 资源和发布 | 嵌入默认 MCP 指引；整理包依赖闭包；发布并复制 Runner 目录 | 全新目录的项目仅靠本地 NuGet feed 可 restore/build/publish/run |
| E. 类库链路验证 | 从外部宿主通过现有 REST/MCP 接口导入 ZIP、显式关联项目、运行节点 | API 只看到元数据，Worker 执行节点；没有默认类库也能启动 |
| F. 文档与兼容 | 中英文接入指南、配置表、部署说明；官方宿主回归测试 | 现有 CLI、stdio、SDK 客户端和 API URL 保持兼容 |

每阶段结束都可独立提交，提交信息遵循仓库约定，例如 `feat: add reusable SereinFlow ASP.NET Core hosting`。不应在未完成 D 阶段的外部包消费验证时宣称“新项目只需安装一个 NuGet 包”。

## 6. 验收测试清单

使用 `WebApplicationFactory` 或 `TestServer` 测服务图与路由，用真实 `dotnet publish` 输出测试 Runner 和类库执行。至少覆盖：

1. 全新项目调用目标 API 后，`/healthz`、`/api/projects`、`/hubs/runs/negotiate`、`/hubs/workspace/negotiate` 可用；组合模式下 `/mcp` GET 返回 405。
2. Web-only 没有 `/mcp`；MCP-only 没有 REST 与 Hub；两个模式的后台服务都可成功启动，组合模式每类服务只有一个实例。
3. 未认证 MCP POST 返回 401；认证后 `initialize` 返回会话 ID，后续 `tools/list`、`resources/list`、`resources/read` 成功；非法会话和 Origin 策略保持原语义。
4. SereinFlow Controller 出现在外部宿主的 OpenAPI；MCP 与 Hub 不出现在 REST 文档；宿主自己的 Controller 与 SereinFlow Controller 共存。
5. 无默认类库配置时外部宿主可正常启动。通过现有 REST/MCP 接口上传仓库测试类库的正式 ZIP，显式关联项目，再在独立 Worker 中执行节点并取得结果；API 进程没有加载该类库程序集。
6. Runner 缺失、数据目录不可写、数据库迁移失败、路由冲突和重复注册均给出可定位的启动错误；失败前不开始监听。损坏类库包仍由既有上传接口返回明确错误，不影响宿主启动。
7. 新项目从本地 NuGet feed 安装包，分别执行 `dotnet build`、`dotnet publish`、运行发布输出；Windows 与 Linux CI 至少各验证一次框架依赖 Runner 路径。
8. 官方 `SereinFlow.Api --mcp-stdio` 仍不监听 HTTP、stdout 仅有协议输出，认证和工具清单不变；现有 API、MCP、Worker、架构测试全绿。

必要命令示例：

```powershell
dotnet restore SereinFlow.sln
dotnet build SereinFlow.sln --no-restore
dotnet test SereinFlow.sln --no-build
./eng/pack-aspnetcore.ps1
```

`dotnet pack` 成功不是终点；还必须使用**独立于本仓库**的临时 ASP.NET Core 项目和本地 feed 完成包消费测试，避免 ProjectReference 或源码目录意外补齐缺失资产。

## 7. 兼容性、发布与回退

- 保持现有 REST 路径、`/mcp`、Hub 路径、MCP 会话协议、Worker v2 协议和 SQLite 数据格式；内部程序集迁移若影响反射/测试，需要在发布前逐项修复。
- 官方宿主仍使用现有 `appsettings.json`；对外包采用安全默认值。若默认值变化会影响既有官方部署，由官方宿主显式写出旧值，不在通用包内隐藏分支。
- 首版不新增启动时类库导入行为。回退宿主包版本时保持数据库和已上传的类库文件；若实现需新 SQLite migration，按现有迁移策略验证旧版宿主是否可启动，并提供明确版本兼容说明。
- 发布门槛为“外部空项目测试全通过 + 官方宿主全量回归通过 + 包内 Runner/引导资源校验通过”。若门槛未达到，先交付源码级复用版本，不发布不完整的 NuGet 门面。

## 8. 需要在实现中明确记录的设计决策

1. 认证策略：REST 各端点现有鉴权覆盖审计结果及新宿主默认值。不能只修改一个配置默认值而假定所有 Controller 已受保护。
2. Runner 包发布形式：无 RID 包是否覆盖当前 Native 依赖；如不覆盖，列出支持的 RID 和构建矩阵。
3. 后续版本是否需要 `AddFlowLibrary<T>()`，以及类型参数表示节点类型还是可信包描述符；此决策不阻塞首版宿主封装。
4. MCP 指引覆盖的优先级与热更新语义：宿主文件优先于包内默认资源，并与既有 `AiGuidance` 配置兼容。

## 9. 2026-09-23 实施记录

- 已提取 `SereinFlow.AspNetCore`，官方 `Program.cs` 改用同一组合入口。外部宿主可选择 Web API、HTTP MCP 或两者；首版未增加启动时类库注册。
- 注册时不创建数据目录或迁移数据库。启动时检查 Runner、SQLite、重复 REST 路由与 Kestrel 上传限额。外部 REST 默认密钥鉴权；未实现项目级权限的操作仅允许管理员密钥。MCP 指引优先读宿主文件，缺失时读嵌入资源。
- Windows 本地验证：`dotnet test SereinFlow.sln` 共 397 项通过；本地 NuGet feed 的 `0.1.3` 测试版本已通过仓库外空白项目 restore、publish 和运行烟雾测试（REST、Hub、MCP 指引）。另在仓库外发布宿主中，REST 上传测试类库 ZIP、关联项目、保存节点、提交运行，独立 Worker 返回成功状态与节点输出。
- 已把 Windows/Linux 的本地包消费烟雾测试加入 CI 工作流；该工作流尚未在本次本地会话执行，Linux 运行结果仍待 CI 确认。未推送 NuGet 包，也未做 RID 专属 Native 依赖矩阵验证。测试包版本号不代表正式发布版本。
