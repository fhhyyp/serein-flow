# SereinFlow API Controller 与 OpenAPI 迁移开发计划

**编制日期：** 2026-08-30
**状态：** 已完成（2026-08-30）
**实施性质：** Web REST 承载层重构与 API 文档补齐，不考虑 Native AOT 兼容
**前置基线：** `SereinFlow.Api` 已完成 endpoint mapping 文件拆分；`Program.cs` 仅承担宿主组合
**关联文档：** [sereinflow-web-mcp-unification-development-plan-2026-08-30.md](sereinflow-web-mcp-unification-development-plan-2026-08-30.md)、[sereinflow-task-exposure-remediation-plan-2026-08-30.md](sereinflow-task-exposure-remediation-plan-2026-08-30.md)

## 1. 目标与决策

将 `SereinFlow.Api` 的普通 Web REST API 从 Minimal API endpoint mapping 迁移为 ASP.NET Core MVC Controller，并提供标准 OpenAPI 文档与 Swagger UI。目标是提高大型 API 的可维护性、可发现性、可测试性和响应合同可读性。

本计划已锁定以下决策：

- REST API 使用 `[ApiController]`、attribute routing、构造函数注入和 `ActionResult`；不为 AOT 保留 Minimal API 路由实现。
- OpenAPI 作为 Web REST API 的公开机器可读合同；Swagger UI 是其浏览、调试和下载入口。
- MCP 继续由 `SereinFlow.Mcp` 的 `MapSereinFlowMcp()` 承载，不转为 Controller，也不出现在 REST Swagger 文档中。
- `--mcp-stdio` 继续由 `McpStdioHost` 承载，不建立 MVC、HTTP listener、Swagger 或 SignalR 生命周期。
- SignalR `RunEventsHub` 继续通过 `MapHub` 映射；SSE 继续是 HTTP REST action 的流式响应，不伪造为 Swagger 可执行请求。
- 现有路由、HTTP 方法、成功/失败状态码、JSON 字段命名、ProblemDetails 扩展字段和行为语义必须保持不变。
- flow patch v2/v1 输入兼容、library node template、类库 attach/detach 的独立 preview/apply 边界、Worker 与持久化逻辑均不在本次改造范围内。

本次不保留旧 REST Minimal API 的双注册或 URL 兼容 wrapper。Controller action 使用完全相同的现有 URL；同一条路由在迁移后只存在一个注册来源。

## 2. 当前基线

当前 Web 宿主已按功能拆出 endpoint mapping 文件，`Program.cs` 负责 DI、middleware、MCP mapping、各模块挂载与 `app.Run()`。当前路由清单包含 65 个 HTTP/Hub 注册，按以下职责分组：

| 当前模块 | 当前职责 | 迁移结果 |
| --- | --- | --- |
| `CoreEndpointMapping` | `/healthz` | `HealthController` 或保留极小 infrastructure mapping |
| `ProjectEndpointMapping` | 项目查询、创建、重命名、归档 | `ProjectsController` |
| `FlowDefinitionEndpointMapping` | Flow 读取/保存、版本、发布、回滚 | `FlowsController`、`FlowVersionsController` |
| `FlowExecutionStartEndpointMapping` | built-in node catalog、开始运行、创建调试会话 | `NodeCatalogController`、`FlowExecutionController` |
| `RunEndpointMapping` | run、debug、snapshot、output、SSE | `RunsController`、`DebugSessionsController` |
| `LibraryEndpointMapping` | 项目类库、目录、上传、族/生命周期、reindex | `ProjectLibrariesController`、`LibrariesController`、`LibraryFamiliesController` |
| `EnvironmentEndpointMapping` | environment settings、flow interfaces | `EnvironmentController`、`FlowInterfacesController` |
| `PublicFlowEndpointMapping` | public flow invoke、任务查询 | `PublicFlowInvocationController` |
| `ApiEndpointHelpers` | DTO/Problem/SSE 转换 | `ApiDtoMapper`、`ApiResponseMapper` 或 Controller 基类 |
| `McpStdioHost` | stdio transport | 保持不变 |

迁移开始前必须将当前路由清单、主要成功响应和失败响应固化为自动化基线。Controller 化不能依赖“看起来类似”的实现判断兼容性。

## 3. 目标架构

### 3.1 Web 与协议入口

Web 模式的组合根目标如下。确切包/API 以 .NET 10 已验证的版本为准，但职责边界不变：

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(...);
builder.Services.AddCors(...);
builder.Services.AddSereinFlowMvcJson();
builder.Services.AddControllers(options =>
{
    // Preserve the existing endpoint-specific validation/error contract.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddSereinFlowOpenApi();
builder.Services.AddSignalR();

builder.Services.AddSereinFlowStorage(storageOptions);
builder.Services.AddSereinFlowApplication();
builder.Services.AddSereinFlowMcp(builder.Configuration);
builder.Services.AddSereinFlowExecution(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseSereinFlowOpenApiUi();
app.MapControllers();
app.MapSereinFlowMcp();
app.MapHub<RunEventsHub>("/hubs/runs");
app.Run();
```

`--mcp-stdio` 分支必须在 `WebApplication.CreateBuilder` 之前继续短路至 `McpStdioHost.RunAsync(args)`。它只注册 storage、Application、MCP 与 stdio 所需服务；不得因为新增 MVC/OpenAPI 依赖而启动 HTTP listener、Swagger UI、SignalR 或 API-only hosted services。

### 3.2 JSON 与错误响应

当前 Web API 使用 camelCase enum、`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 和定制 `ProblemDetails`。MVC 默认 JSON 配置不能替代现有 `HttpJsonOptions` 而造成 JSON 细节漂移。

新增统一扩展 `AddSereinFlowMvcJson`：

1. 将现有 `ConfigureHttpJsonOptions` 的 serializer 设置提取为共享配置方法。
2. 同时配置 `Microsoft.AspNetCore.Http.Json.JsonOptions` 与 `Microsoft.AspNetCore.Mvc.JsonOptions`。
3. 继续使用 `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`，不得因 MVC 默认设置输出 PascalCase 或数值 enum。
4. 保持 `ProblemDetails` 的 5xx 脱敏标题与 detail。

`[ApiController]` 的自动 ModelState 400 行为在第一阶段明确关闭。原因是当前接口对 invalid body、业务校验、`code`、`currentVersion` 和 bilingual title 有既有形状；直接启用默认 `ValidationProblemDetails` 会导致无意的公共合同变更。Controller action 先保留现有显式检查与 `Problem(...)` 返回；后续只有在独立合同版本中，才评估统一自动模型验证。

新增 `ApiControllerBase` 或等价 mapper 时只承载以下横切行为：

- `ProjectLibraryOperationResult`、`LibraryUpgradeOperationResult`、run/debug submit result 到 `ActionResult` 的转换；
- `ProblemDetails.Extensions["code"]`、`["currentVersion"]` 等既有扩展字段；
- DTO 映射和安全 JSON parsing；
- SSE event 序列化与 flush。

它不得调用 Repository、Application 服务或包含项目/流程业务规则。

### 3.3 OpenAPI 与 Swagger UI

采用标准 OpenAPI 3 文档与 Swagger UI，具体实现使用与 .NET 10 兼容并经恢复验证的版本：

- 通过 `Microsoft.AspNetCore.OpenApi` 或已验证的 Swashbuckle generator 产生单一 `v1` 文档；不得并行产生字段可能不同的两份 API 描述。
- Swagger UI 使用 Swashbuckle UI 或等价成熟 UI，固定挂载在 `/swagger`；OpenAPI JSON 固定为 `/openapi/v1.json` 或 `/swagger/v1/swagger.json` 中的一种，并在实现前将最终 URL 写入 API 文档与集成测试。优先保留 `/openapi/v1.json` 作为机器入口，Swagger UI 仅引用该文档。
- API 文档启用由服务器端 `SereinFlow:ApiDocumentation:Enabled` 配置控制。Development 默认启用；非 Development 默认禁用，生产环境必须显式开启。
- Swagger 文档只包含 REST Controller；`/mcp`、stdio、SignalR Hub、SSE stream 与内部 health/probe endpoint 按协议性质标注或排除。
- 每个 action 声明 `ProducesResponseType`；包含 request body 的 action 声明 `Consumes`/`Produces`。文件上传使用 `multipart/form-data` 模型和明确的 response contract。
- 文档不得包含 API key、数据库路径、libraries 目录、staging 路径、脚本 literal、内部异常细节或其他服务端机密。

若普通 REST API 未来增加认证，在同一变更中加入准确的 OpenAPI security scheme；不得把 MCP API key scheme 错标为 REST API 的认证要求。

## 4. Controller 目录与边界

在 `src/SereinFlow.Api/Controllers/` 下按资源和使用者边界创建 Controller。单个 Controller 优先保持在约 250 行以内；复杂 action 的纯转换/流式辅助逻辑移入同命名 support 文件，不创建“通用业务 Controller”。

| Controller | 路由根 | 负责 action | 不负责内容 |
| --- | --- | --- | --- |
| `HealthController` | `/healthz` | health read model | MCP health/session |
| `ProjectsController` | `/api/projects` | list/create/rename/archive | flow definition、library attachment |
| `FlowsController` | `/api/projects/{projectId}/flows` | read/update flow definition | publish/rollback、run query |
| `FlowVersionsController` | `/api/projects/{projectId}/flows/{flowId}/versions` | list/get/publish/rollback | flow body save |
| `NodeCatalogController` | `/api/node-catalog` | built-in catalog | scanned library nodes |
| `FlowExecutionController` | `/api/projects/{projectId}/flows/{flowId}` | create run/create debug session | run lifecycle command |
| `RunsController` | `/api/runs` | list/overview/read/snapshot/outputs/cancel/interrupt/events/SSE | debug command actions |
| `DebugSessionsController` | `/api/debug-sessions` | session read/wait/continue/step/stop | session creation route |
| `ProjectLibrariesController` | `/api/projects/{projectId}` | library refs/usage/upgrade preview/apply | global catalog mutation |
| `LibrariesController` | `/api/libraries`、legacy `/api/library`、environment library aliases | catalog/list/get/upload/archive/family/lifecycle/reindex | project attachment state |
| `LibraryFamiliesController` | `/api/library-families` | family read/artifact list | archive/upload |
| `EnvironmentController` | `/api/environment/settings` | execution settings read/write | library catalog aliases |
| `FlowInterfacesController` | `/api/environment/interfaces` | interface CRUD | public invoke |
| `PublicFlowInvocationController` | `/api/public` | invoke/task read | project management |

保留现有 `/api/library`、`/api/libraries/upload-zip` 与 environment library alias，直到单独批准 REST API v2 破坏性变更。它们应是同一 Controller 的显式 attribute route，而不是转发/HTTP self-call wrapper。

## 5. 分阶段实施任务

### Phase 0：冻结合同与准备测试夹具

**目标：** 建立可执行兼容基线，避免 Controller 迁移引入隐蔽行为变化。

**任务：**

1. 为当前 65 个 REST/Hub mapping 导出 route + HTTP method 清单；Controller 迁移完成后逐项比对。
2. 在 `SereinFlow.Api.IntegrationTests` 增加代表性契约测试：项目、flow 版本、run/debug、library 上传/错误、environment、public invoke、SSE event replay。
3. 为 `ProblemDetails` 固化 status、title、detail、`code`、`currentVersion` 与 5xx 脱敏行为。
4. 记录当前 JSON enum、JSON encoder、上传 `multipart/form-data`、SSE headers 和响应 body 形状。
5. 明确 Controller 化不得改变 MCP `/mcp`、`--mcp-stdio`、`/hubs/runs` 行为，并保留已有 MCP/P1 回归测试作为守卫。

**退出条件：** 每个 Controller 批次都能以路径、方法、状态码和 JSON 形状验证为通过标准，而不是仅以编译成功为标准。

### Phase 1：引入 MVC、共享响应层和文档基础设施

**目标：** 让 Controller 可以与现有 Minimal API 暂时并存，但不重复注册同一路由。

**任务：**

1. 在 `SereinFlow.Api.csproj` 和 `Directory.Packages.props` 中以中央版本管理加入经验证的 OpenAPI/Swagger UI package；锁文件同步更新。
2. 新增 `AddSereinFlowMvcJson`，确保 MVC 和 MCP/Minimal HTTP JSON 配置保持现有序列化结果。
3. 注册 `AddControllers`、API explorer、OpenAPI generator、Swagger UI，并配置开发环境默认文档与生产显式开关。
4. 通过 `ApiBehaviorOptions.SuppressModelStateInvalidFilter` 保持当前手工 invalid-request 响应，创建共用 `ApiControllerBase`/mapper，逐步替换 `ApiEndpointHelpers`。
5. 加入 `app.MapControllers()`；保留 `MapSereinFlowMcp()` 和 `MapHub<RunEventsHub>`。
6. 为 OpenAPI 文档添加标题、版本、描述、tag 策略、XML comments（如仓库决定启用）和统一 ProblemDetails schema；不得在 OpenAPI operation 中暴露服务器本地路径。
7. 增加 `GET /swagger`、OpenAPI JSON、文档开关和 MCP 不被列入 Swagger 的集成测试。

**退出条件：** 不迁移业务 action 时，Web 模式可提供可解析的 OpenAPI 文档和 Swagger UI；stdio 模式无 Web listener；文档内无 MCP/秘密数据。

### Phase 2：迁移低风险读取与项目生命周期 Controller

**目标：** 验证 attribute route、依赖注入、JSON 结果和 ProblemDetails mapper 的通用模式。

**任务：**

1. 创建 `HealthController`、`NodeCatalogController`、`ProjectsController`。
2. 将项目 list/create/rename/archive 从 `ProjectEndpointMapping` 原样迁移，使用 `[FromQuery]`、`[FromRoute]`、`[FromBody]` 明确绑定来源。
3. 为成功与典型 400/404/409 响应添加 `ProducesResponseType`。
4. 移除同一路由的 mapping 注册；不得靠 endpoint order 掩盖重复路由。
5. 在 Swagger UI 验证 schema、必填标注、enum camelCase 描述与实际 response 一致。

**退出条件：** 项目生命周期 read/write 的 URL、HTTP 方法和失败合同无差异；新 Controller 无直接 Sqlite/Repository 绕过 Application 服务的新增逻辑。

### Phase 3：迁移流程定义、版本与启动 Controller

**目标：** 将 flow 资源模型按读取/写入/版本/执行启动职责清晰分离。

**任务：**

1. 创建 `FlowsController`，迁移 flow definition read/update，继续复用 `FlowDefinitionWriteService`。
2. 创建 `FlowVersionsController`，迁移 versions list/get、publish、rollback；保留 existing `flow.version_track_invalid`、乐观并发 `currentVersion`、archived project 和 library validation 响应。
3. 创建 `FlowExecutionController`，迁移 `POST .../runs` 和 `POST .../debug-sessions`；继续使用 `RunSubmissionService` 和 `FlowDebugSessionService`。
4. 为 flow definition DTO、version DTO、run submission、debug session 和 validation response 补充精确 OpenAPI metadata。
5. 验证 flow patch/MCP 预览不经由 REST Controller 改变其 v2 canonical response，library attach/detach 仍是独立 MCP preview/apply 流程。

**退出条件：** 版本发布、回滚、提交 run、创建 debug session 的所有冲突、验证和 accepted response 保持兼容。

### Phase 4：迁移运行、调试与流式接口

**目标：** 在 Controller 中保留运行生命周期与 SSE 的细粒度行为。

**任务：**

1. 创建 `RunsController`，迁移 run list/overview/read/snapshot/outputs/cancel/interrupt/events。
2. 创建 `DebugSessionsController`，迁移 session get/wait/continue/step/stop。
3. 将 `/api/runs/{runId}/events/stream` 实现为明确的 action，保留 `text/event-stream`、`Cache-Control: no-cache`、Last-Event-ID、持久化 replay、subscribe-before-read、序列去重与 request abort 行为。
4. 流式 action 使用 `IActionResult`/直接写 `HttpResponse` 的局部实现；在 OpenAPI 标为 stream/不可交互，避免 Swagger UI 发起错误的 JSON 请求。
5. `RunEventsHub` 继续在 `Program.cs` 通过 `MapHub` 映射，不迁移至 Controller。

**退出条件：** debug 状态等待、cancel/interrupt 冲突、SSE terminal replay 与活动订阅的集成测试均通过；SignalR 地址不变。

### Phase 5：迁移类库、环境与公开接口 Controller

**目标：** 完成剩余 REST surface 迁移，并将上传与 legacy aliases 文档化。

**任务：**

1. 创建 `ProjectLibrariesController`，迁移 project library ref、usage 与 library upgrade preview/get/apply/apply-batch。
2. 创建 `LibrariesController` 和 `LibraryFamiliesController`，迁移 global catalog、archive、family/lifecycle、upload、upload-zip、singular aliases、environment library aliases 与 reindex。
3. 对上传 action 定义 `[Consumes("multipart/form-data")]`、file field `file`、最大允许大小、`LibraryUploadException` 到安全 ProblemDetails 的转换；不得在 Swagger sample 中嵌入文件内容。
4. 创建 `EnvironmentController`、`FlowInterfacesController`、`PublicFlowInvocationController`，迁移 environment settings/interface 和 public invoke/task API。
5. 为 legacy route 添加 `Obsolete`/OpenAPI deprecation 标记（仅文档标识，不改变运行时可用性），明确 canonical route。

**退出条件：** library/upload/public surface 的 route、multipart、状态码、错误正文与当前实现兼容；类库合同扫描、attach/detach、node template 与 Worker 执行没有新增 Controller 直连路径。

### Phase 6：删除 Minimal REST mapping 并收敛测试/文档

**目标：** 使 Controller 成为唯一 REST 承载方式，消除双实现风险。

**任务：**

1. 删除已完全迁移的 `*EndpointMapping.cs` 文件和仅为其服务的代码。
2. 将 `ApiEndpointHelpers` 按职责收敛为 DTO mapper、response mapper 与 SSE support；删除无调用的静态 helper。
3. `Program.cs` 最终仅保留：stdio 分支、Web host/DI/middleware、`MapControllers`、`MapSereinFlowMcp`、`MapHub`、OpenAPI UI mapping 与 `app.Run`。
4. 在 Architecture Tests 中禁止 `SereinFlow.Api` 中新增 `MapGet`/`MapPost`/`MapPut`/`MapPatch`/`MapDelete` 的 REST mapping（MCP、Hub、health/probe 的明确例外以白名单列出）。
5. 在 Architecture Tests 中要求 REST controllers 位于 `Controllers` 命名空间、继承 `ControllerBase`、使用 attribute route，并禁止直接引用 Infrastructure persistence 实现。
6. 输出并对比最终 OpenAPI 文档，确保每个 REST route 被精确描述一次，无重复 operationId 或重复 route/method。

**退出条件：** 所有普通 REST 路由只来自 Controller；MCP/SignalR/stdio 维持专用 transport；不会通过一个残留 Minimal API 重新引入维护分叉。

## 6. 合同与兼容要求

### 6.1 必须保持的 REST 合同

- 所有当前 URL、HTTP method、route constraint（`guid`、`long` 等）和 legacy alias 保持有效。
- 输入 DTO 的 JSON 字段与 enum 始终为 canonical camelCase；错误大小写或未知值仍按现有控制器外的 contracts 规则处理。
- `202 Accepted` 的 `Location`、`201 Created`、400/404/409/500 状态码与现有响应形状保持不变。
- `ProblemDetails` 的 bilingual title、5xx 脱敏 detail、`code`、`currentVersion` extension 保持不变。
- 文件上传仍接受当前 form field 与别名路径；上传错误不泄露存储路径、堆栈或内部异常。
- SSE payload、event id、event type、headers、断开与 cancellation 行为保持不变。

### 6.2 明确不迁移的协议

- `/mcp` 的 JSON-RPC session、API key、限流、超时、大小限制、CORS、稳定 `mcp.*` 错误与 resource/tool contracts。
- `SereinFlow.Api --mcp-stdio` 的 stdout 纯 JSON-RPC、stderr 日志、显式 API key 与 EOF 退出行为。
- `/hubs/runs` SignalR Hub transport。
- flow patch v2 schema、legacy v1 reader、normalized preview persistence、library node template、library attach/detach preview/apply。

## 7. 测试计划

### 7.1 单元与结构测试

- MVC/HTTP JSON options 与当前 Minimal HTTP JSON options 输出等价：camelCase、enum、encoder、null/JSON element 行为。
- `ApiResponseMapper` 对 run/debug/library/project result 的状态码、headers 和 ProblemDetails extensions 与旧实现等价。
- OpenAPI generator 的 document name、title、版本、tags、schema 和不含 MCP endpoint 的行为。
- 开发/生产 API documentation 开关、Swagger UI 和 JSON 文档的访问策略。
- Architecture Tests：REST Controller 的命名空间、依赖边界、route uniqueness；不允许重复 Minimal REST mapping。

### 7.2 API 集成测试

| 场景 | 断言 |
| --- | --- |
| Route parity | 迁移前后 65 条 route/method（含 Hub）清单一致；REST action 不重复注册 |
| Project/flow | list/create/rename/archive、definition read/save、version/publish/rollback status/body 等价 |
| Run/debug | accepted location、state wait、command conflict、cancel/interrupt 等价 |
| SSE | replay、Last-Event-ID、terminal run、active subscription、content type 和 event sequence 等价 |
| Libraries | project refs、upload multipart、legacy aliases、family/lifecycle/reindex 错误不泄露路径 |
| Environment/public | settings/interface/public invoke/task 所有 route 返回等价 |
| OpenAPI | `/swagger` 可访问，JSON 可解析，Controller routes 与 `ProducesResponseType` 可见，MCP/stdio/Hub 不作为 REST operation 暴露 |
| MCP regression | `/mcp`、HTTP session、stdio tools/list/read-only tool 继续通过，且不依赖 MVC 路由 |
| Security | 文档和错误响应不包含 API key、数据库/类库/staging 绝对路径或内部 stack |

### 7.3 人工验证

1. 启动 Development Web host，访问 `/swagger` 并下载 OpenAPI JSON。
2. 使用 Swagger UI 调用一个只读项目 API、一个 flow version API、一个 run read API 和带 404/409 的失败场景。
3. 使用 HTTP MCP client 对 `/mcp` 执行 `initialize`、`tools/list`、一个只读 Tool；确认它不出现在 Swagger REST operation 列表。
4. 使用 `SereinFlow.Api --mcp-stdio` 执行 `initialize`/`tools/list`；确认 stdout 无 Swagger 或 ASP.NET 日志。
5. 验证 SignalR `/hubs/runs` 与 SSE stream 仍能接收 run events。

### 7.4 验证命令

正常重建须等待正在运行的用户进程释放 DLL 锁；实施期间不得终止用户进程。锁未释放时，先执行静态检查和可运行的无重建测试，并清楚记录未运行项。

```powershell
dotnet build SereinFlow.sln
dotnet test tests/SereinFlow.Application.Tests/SereinFlow.Application.Tests.csproj
dotnet test tests/SereinFlow.Mcp.Tests/SereinFlow.Mcp.Tests.csproj
dotnet test tests/SereinFlow.Api.IntegrationTests/SereinFlow.Api.IntegrationTests.csproj
dotnet test tests/SereinFlow.ArchitectureTests/SereinFlow.ArchitectureTests.csproj
git diff --check
```

## 8. 文件变更清单

| 类型 | 文件/目录 | 计划动作 |
| --- | --- | --- |
| 修改 | `src/SereinFlow.Api/Program.cs` | 注册 MVC/OpenAPI，使用 `MapControllers`，保留 MCP/Hub/stdio 边界 |
| 新增 | `src/SereinFlow.Api/Controllers/` | 按第 4 节创建 REST Controllers |
| 新增 | `src/SereinFlow.Api/Controllers/ApiControllerBase.cs` | 受限的 ProblemDetails/response 基类或等价 mapper |
| 新增/修改 | `src/SereinFlow.Api/OpenApi/` | OpenAPI、Swagger UI、统一 MVC/HTTP JSON 注册扩展 |
| 修改/删除 | `src/SereinFlow.Api/*EndpointMapping.cs` | 按批次迁移后删除，不保留双路由 |
| 修改/拆分 | `src/SereinFlow.Api/ApiEndpointHelpers.cs` | 移入 DTO/response/SSE support，删除 endpoint-specific helper |
| 修改 | `src/SereinFlow.Api/SereinFlow.Api.csproj` | 引入验证后的 OpenAPI/Swagger package reference |
| 修改 | `Directory.Packages.props` | 集中管理新增 package 版本 |
| 修改 | `tests/SereinFlow.Api.IntegrationTests/` | route/response/SSE/OpenAPI/MCP regression |
| 修改 | `tests/SereinFlow.ArchitectureTests/` | Controller-only REST 与依赖边界守卫 |
| 修改 | API 启动/运维文档 | 记录 `/swagger`、文档开关与 MCP 不属于 Swagger 的边界 |

不修改 `SereinFlow.Application`、`SereinFlow.Infrastructure`、`SereinFlow.Mcp` 的业务行为来适配 Controller。若某项当前 result type 不足以表达已有 HTTP 语义，只能在 API adapter 层补齐映射；不得将 HTTP transport 依赖反向引入 Application。

## 9. 风险与控制措施

| 风险 | 控制措施 |
| --- | --- |
| `[ApiController]` 自动 400 改变已有错误格式 | 第一阶段关闭 ModelState invalid filter；用契约测试后再单独规划统一 validation |
| MVC JSON 配置与 Minimal API 不一致 | 统一 JSON 配置扩展，同时测试 enum、encoder、ProblemDetails、JSON element |
| Controller 与旧 mapping 重复注册路由 | 按批迁移后即移除对应 mapping；Route parity/uniqueness 测试阻止重复 |
| Swagger 暴露 MCP 或敏感配置 | REST-only API explorer、文档安全扫描、生产显式开关 |
| 大型 Controller 再次膨胀 | 按资源拆分，单 Controller 目标不超过约 250 行，转换逻辑下沉到 support 类 |
| 为 Controller 化改动 Application/Worker 业务 | 建立 Architecture Test，Controller 只调用已有 Application abstraction；不直接访问实现仓储 |
| SSE/SignalR 被普通 JSON Controller 误处理 | SSE 专项集成测试；Hub 继续 `MapHub`，不迁移 |
| stdio 因 MVC 启动而污染 stdout 或打开 listener | 保持最早 short-circuit，进程级 stdio regression test |
| 未提交变更被覆盖 | 在每个批次前检查 diff，只在相关文件合并；不使用 reset、checkout、clean |

## 10. 完成定义

只有以下条件全部满足，才将本计划标记完成：

- 所有普通 Web REST API 由 Controllers 提供，且没有同 route/method 的 Minimal API 副本。
- `Program.cs` 继续是小型宿主组合根，不重新成为 endpoint/business logic 聚合文件。
- Swagger UI 和单一 OpenAPI v1 文档在允许的服务器环境可用，内容准确、无敏感路径，并明确排除 MCP/stdio/SignalR protocol surfaces。
- 现有 URL、HTTP 方法、状态码、JSON camelCase、ProblemDetails extensions、SSE 和 legacy library aliases 保持兼容。
- MCP HTTP、MCP stdio、flow patch v2/v1、normalized preview、library node template、attach/detach 独立流程和 SignalR 未发生合同变化。
- Application、MCP、API integration、Architecture 测试通过，且 OpenAPI/Swagger 与 route parity 有自动化覆盖。
- 实施期间未终止用户进程、未删除数据库或 `libraries`、未覆盖任何用户未提交变更。

## 11. 实施结果（2026-08-30）

本计划已完成，普通 Web REST API 已迁移至 `SereinFlow.Api/Controllers/` 下的 MVC Controller，且已删除原 REST `*EndpointMapping.cs` 文件。`Program.cs` 只承担 stdio 早期分支、Web host/DI 组合、middleware、OpenAPI、`MapControllers`、MCP 和 SignalR 的协议挂载。

- 已注册 MVC，且 MVC 与既有 Minimal/MCP JSON 共用 camelCase enum 与 encoder 配置；`ApiBehaviorOptions.SuppressModelStateInvalidFilter` 保持原有显式 ProblemDetails 合同。
- 已提供单一 OpenAPI v1 JSON：`/openapi/v1.json`，以及 Swagger UI：`/swagger`。`SereinFlow:ApiDocumentation:Enabled` 可显式控制启用；Development 环境默认启用，其他环境默认关闭。
- Swagger 只描述 REST Controller；`/mcp`、`--mcp-stdio`、`/hubs/runs` 和 SSE stream 不出现在 REST OpenAPI operations 中。MCP JSON-RPC、stdio、SignalR、flow patch v2/v1、library node template 与类库 attach/detach preview/apply 均未迁移或改变合同。
- 已保留全部现有 REST URL、HTTP 方法、legacy library aliases、状态码、ProblemDetails extensions、上传表单字段和 SSE 实现路径；没有保留重复的 Minimal API REST 路由。
- 已新增架构与集成测试，分别守卫 Controller-only REST、无残留 endpoint mapping、OpenAPI/Swagger 可访问性，以及 MCP/SignalR/SSE 不进入 REST 文档。

已在不终止用户进程、不删除数据库或 `libraries`、不重置或清理工作树的前提下完成以下验证：

```powershell
dotnet build SereinFlow.sln
dotnet test tests/SereinFlow.Application.Tests/SereinFlow.Application.Tests.csproj --no-restore
dotnet test tests/SereinFlow.Mcp.Tests/SereinFlow.Mcp.Tests.csproj --no-restore
dotnet test tests/SereinFlow.Api.IntegrationTests/SereinFlow.Api.IntegrationTests.csproj --no-restore
dotnet test tests/SereinFlow.ArchitectureTests/SereinFlow.ArchitectureTests.csproj --no-restore
git diff --check
```

结果：解决方案构建成功（0 warnings、0 errors）；Application `78/78`、MCP `21/21`、API integration `4/4`、Architecture `26/26` 均通过。静态检查确认 API 源码中没有 REST Minimal API mapping 或旧 `*EndpointMapping.cs` 源文件；旧 MCP 宿主目录仅残留未跟踪的历史 `bin/obj` 构建产物，未被删除或纳入提交。

## 12. 后续任务（不纳入本计划）

- 在单独的 REST API v2 合同中启用统一 automatic model validation 并弃用历史 alias。
- 引入 REST API 认证、授权与其对应的 OpenAPI security scheme。
- 评估 API versioning、rate limiting、response caching 和 generated client SDK。
- 重新评估 Native AOT 时，再选择支持 trimming/AOT 的 OpenAPI 与 endpoint 方案；该决策不反向约束本次 Controller 化。
