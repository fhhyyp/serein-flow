# SereinFlow MCP 与 AI Skill 分析报告

## 1. 结论

当前 SereinFlow 可以在现有流程引擎之上增加 MCP Server。API、Application、Runtime、Worker 和 Persistence 已经形成清晰边界，流程定义、版本、运行实例、运行定义快照、运行事件、节点输出、调试会话和类库 PE 元数据目录均已有基础能力。

当前系统还不能直接作为面向 AI 的 MCP Server 使用。现有 API 主要服务于 Vue 编辑器和运行控制，缺少面向模型的稳定读模型、认证授权、结构化运行诊断、调试会话租约、SereinLang 编译检查服务以及 C# 类库构建流水线。

推荐的数据路径如下：

```text
AI Agent
  -> MCP Tools / Resources
  -> SereinFlow MCP adapter
  -> Application services
  -> Persistence / Worker Client
  -> Supervisor
  -> Worker Runner
  -> Runtime / SereinLang / uploaded DLL
```

MCP Server 不应直接访问数据库、Worker 内存或执行任意 shell 命令。所有能力都应经过 Application 服务和专门的构建、运行安全边界。

## 2. 当前可复用能力

### 2.1 流程与版本

- `FlowDefinitionDto` 已包含画布、节点、端口、参数、连接、脚本、FlowCall 和运行策略。
- 保存前和执行前均有流程校验。
- 服务端会重新计算脚本源哈希，并构建执行计划。
- 开发版本和生产版本已经分轨。
- 已支持历史版本查询、生产发布、开发/生产回滚。
- 回滚创建新的递增版本，不修改历史 JSON。
- 运行时持久化不可变流程定义快照。

相关实现：

- `src/SereinFlow.Application/FlowDefinitionValidation.cs`
- `src/SereinFlow.Infrastructure/Persistence/FlowDefinitionPersistence.cs`
- `src/SereinFlow.Contracts/Dtos.cs`

可作为 Resources：

```text
sereinflow://projects
sereinflow://projects/{projectId}
sereinflow://projects/{projectId}/flows/{flowId}/definition/development
sereinflow://projects/{projectId}/flows/{flowId}/definition/production
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}
sereinflow://projects/{projectId}/flows/{flowId}/versions/{version}
```

### 2.2 运行与调试

当前已经支持普通运行、生产接口调用、运行队列、并发限制、超时、最大步骤数、最大节点访问次数、取消和孤儿运行中断。

调试运行已经具备：

- 断点。
- 节点执行前暂停。
- 继续执行。
- 单步执行。
- 停止调试。
- 全局 Flipflop 触发调试。
- 调试触发队列。
- 调试事件持久化。

相关实现：

- `src/SereinFlow.Api/FlowDebugSessionService.cs`
- `src/SereinFlow.Runtime/DebugExecutionGate.cs`
- `src/SereinFlow.Runtime.Abstractions/ExecutionGating.cs`
- `src/SereinFlow.Worker.Protocol/WorkerMessages.cs`

### 2.3 运行事件、输出和快照

运行事件与节点输出已经分开存储。节点输出包含输入、输出、分支、错误码和错误消息，事件通过 sequence 支持增量读取和 SSE 回放。

当前 `/api/runs/{runId}/snapshot` 返回的是不可变流程定义快照，不是实时 Runtime 状态快照。实时上下文、局部变量、调用栈和当前暂停边界的完整信息尚未形成独立查询模型。

### 2.4 类库扫描与稳定契约

类库上传阶段已经使用 `PEReader` 和 `MetadataReader`，不会加载上传程序集。扫描结果包含类库名、节点方法、参数类型、默认值、可变参数、枚举选项和兼容性 Manifest。

未设置 ID 时的规则已经适合 AI 生成流程：

- `FlowNode.Id` 缺省时，根据 `FlowLibrary.Name` 与 CLR 方法名派生节点契约 ID。
- `NodeParam.Id` 缺省时，使用 CLR 参数名。
- `FlowLibrary.Name` 缺省时，使用类名。

相关实现：

- `src/SereinFlow.Infrastructure/Persistence/LibraryCatalogService.cs`
- `src/SereinFlow.Contracts/LibraryAttributes.cs`

## 3. 必须补齐的功能

### P0

1. **认证、授权和调用者身份**

   当前 API 使用全开放 CORS，未形成管理接口的认证授权边界。MCP 调用者必须至少区分项目读取、流程写入、调试、运行、发布、回滚、类库上传和生产调用权限。Tool 不能依赖 Skill 提示词实现安全控制。

2. **面向 AI 的稳定读模型**

   当前 DTO 偏向前端编辑器，事件载荷主要是 `payloadJson` 字符串。需要独立的 `FlowTopology`、`NodeContract`、`RunTimeline`、`NodeExecutionRecord`、`DebugState` 和 `ValidationDiagnostic`，并为其定义 schema version。

3. **运行定义快照与 Runtime 检查快照分离**

   需要区分不可变流程定义、当前运行状态和断点暂停状态。暂停状态至少应包含当前节点输入、步骤、调用帧、触发实例、最近完成节点和最近节点输出。

4. **AI 可用的调试会话协议**

   当前调试控制依赖 API 进程中的 Worker 句柄和命令序号。MCP 需要会话所有权、租约、幂等键、等待暂停/终止的异步操作模型，以及 API 重启后的明确策略。

5. **Flow Validate / Normalize / Preview 闭环**

   AI 生成流程必须能够先校验、再规范化、再预览，最后经授权保存。错误需要区分 JSON、领域、执行计划、类库绑定、参数和连接问题。

6. **SereinLang 编译检查服务**

   当前脚本主要在 Worker 执行链路内编译或读取 `.ssc`。需要独立的 `compile_script` 和结构化行列诊断，支持 AI 反复修复后再写入流程。

7. **C# 类库工件导入流水线**

   C# 源码生成、项目创建和 DLL 构建属于用户本地具备 VS/.NET 工具链的环境。服务端不执行用户提交的项目、脚本、`dotnet build` 或 MSBuild；它只接收命名合规的预构建 ZIP，完成 ZIP 安全校验、PE 元数据扫描、契约兼容性分析、预览确认和不可变工件导入。

8. **生成代码的执行隔离**

   Worker 进程隔离是基础，但仍需要补充网络、文件系统、环境变量、子进程、程序集加载、CPU、内存、输出大小和进程树回收策略。现有 Worker 文档还列出了 Linux ACL、namespace、cgroup 和恶意 DLL 黑盒隔离测试待完成。

### P1

- 运行路径、节点耗时、失败根因和 FlowCall 层级的统一分析服务。
- 事件和输出按节点、类型、时间范围过滤，并支持分页、摘要、脱敏和大小限制。
- 开发/生产版本和类库工件的结构化 diff。
- 参数语义、节点副作用、幂等性和错误行为等 AI 描述元数据。
- SereinLang 编译器和脚本模块的可部署版本锁定，消除开发机盘符依赖。
- MCP Tool、Resource 和 Skill 的版本化及安装机制。

## 4. 推荐 MCP 能力

### Resources

```text
projects
project detail
flow definition
flow topology
flow version history
flow version detail
library catalog
library manifest
run detail
run definition snapshot
run timeline
run outputs
debug session state
debug session events
validation result
build result
```

### Tools

```text
validate_flow
normalize_flow
inspect_flow_topology
compare_flow_versions
start_debug_session
wait_debug_state
step_debug_session
continue_debug_session
stop_debug_session
run_flow
cancel_run
compile_script
create_library_project
preview_library_package
apply_library_package
preview_project_library_attach
apply_project_library_attach
publish_flow_version
rollback_flow_version
```

生产发布、生产回滚、流程修改、类库导入和项目类库接入应支持预览、`expectedVersion`、`idempotencyKey` 和人工确认。

## 5. Skill 划分

- `SereinFlow Designer Skill`：读取节点目录和流程拓扑，生成或修复 `FlowDefinition`，始终先调用校验工具。
- `SereinLang Authoring Skill`：根据脚本输入输出契约生成源码，调用编译工具并根据行列诊断修复。
- `SereinFlow Runtime Analyst Skill`：读取运行状态、事件、节点输入输出和调试状态，分析实际执行路径与失败原因。
- `SereinFlow Library Builder Skill`：在用户本地 VS/.NET 环境生成 C# 类库项目并构建 DLL/ZIP，再调用 MCP 的 PE 扫描、兼容性预览和导入 Tool；Skill 不授予服务端构建或任意 shell 权限。

Skill 只定义工作流程和知识，不授予权限。权限、参数校验、敏感数据处理和构建隔离必须由 MCP Server 和 Application 服务强制执行。

## 6. 落地顺序

```text
只读 AI 读模型与安全查询
  -> 结构化调试控制
  -> SereinLang 编译检查
  -> FlowDefinition 生成与校验
  -> C# 类库构建与扫描
  -> 经授权的流程执行和生产操作
```

第一阶段先实现只读 AI 读模型、统一错误和查询边界，再接入只读 MCP Resources。这样能复用当前引擎，同时避免 AI 直接接触数据库、Worker 内存和任意命令执行。
