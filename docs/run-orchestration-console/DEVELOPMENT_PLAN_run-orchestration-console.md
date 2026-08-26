# SereinFlow 运行编排、Worker 缓存与运行控制台开发计划

## 执行状态（2026-08-26）

- [x] Flow `RunPolicy`、Schema v4、运行快照和 SQLite 独占准入已实现。
- [x] 有界调度队列、全局/项目/监听器并发许可、排队超时、取消和应用重启恢复已实现。
- [x] 每个 `FlowRun` 的 Worker 内类库运行时缓存已实现，Action 与 Flipflop 共享程序集、类型与方法元数据缓存。
- [x] 运行控制台已成为 Web 默认入口；它展示队列、运行中、近期完成和跨项目目录，项目目录与运行概览同步刷新。
- [x] 运行控制台在活动运行时使用 SignalR 优先、SSE 降级，并在离开控制台或运行终态后释放订阅。
- [x] Worker Supervisor 的标准输入、输出和错误流强制 UTF-8，避免中文诊断破坏 JSON 协议帧。
- [x] 已完成 Worker 协议/取消、队列/SSE、持久化、前端构建及最小真实运行链路验证。

本轮不创建 Git 提交；临时 API 验证数据只写入系统临时目录，不会进入项目工作树。

## 1. 目标与边界

本迭代将流程运行从“单消费者后台队列 + 直接进入画布编辑器”升级为可配置、可观测且受控的运行平台。

### 已确认的产品入口

Web 前端的默认且唯一首屏是**运行控制台**，不是流程编辑器。应用初始化时可以在后台加载项目目录和当前项目摘要，但不得自动渲染节点画布、节点库或检查器。用户通过以下明确操作进入编辑器：

- 在项目列表中选择“编辑流程”；
- 从运行记录进入其对应项目的流程；
- 新建项目后进入新项目编辑器。

编辑器提供返回运行控制台的导航。运行控制台是跨项目运行态的总览，不能因为切换项目而丢失其他项目的排队或运行中实例。

不变的安全边界：每个 `FlowRun` 启动一个独立 Worker 进程；API 进程不加载外部 DLL 和不受信任脚本；不同运行实例不共享用户类库对象或静态状态。

运行链路：

```text
运行控制台 / 编辑器运行命令
  -> Flow 运行策略准入
  -> 有界等待队列
  -> 全局、项目、监听器并发许可
  -> 单个 FlowRun 的专属 Worker
  -> Worker Run 内类库程序集与反射元数据缓存
  -> 事件持久化、SignalR、SSE
```

## 2. Flow 运行策略

每个 Flow 保存 `RunPolicy`，并随 FlowRun 快照持久化。

| 策略 | 运行语义 | 适用场景 |
| --- | --- | --- |
| `Parallel` | 同一 Flow 可存在多个运行实例，受平台资源许可约束。 | API 调用、查询、计算、可重入业务。 |
| `ExclusiveReject` | 同一 Flow 有 `Queued` 或 `Running` 实例时，新请求立即返回冲突，不进入等待队列。 | 串口、PLC、单连接设备、不可重入 SDK。 |

本期不提供 `SerialQueue`。设备独占场景不能通过隐式排队掩盖资源争用。

旧 Schema 流程不兼容：Flow Schema 版本递增；缺少 `RunPolicy` 的定义保存或运行时返回 `flow.schema_unsupported` 或 `flow.run_policy_missing`。

## 3. 持久化和原子准入

`FlowRuns` 增加：

```text
ConcurrencyMode
ExclusivityKey
IsListenerRun
QueuedAt
StartedAt
```

`ExclusiveReject` 运行使用 `ExclusivityKey = FlowId`。SQLite 在 Infrastructure 迁移中建立部分唯一索引：

```sql
CREATE UNIQUE INDEX UX_FlowRuns_ActiveExclusiveFlow
ON FlowRuns(ExclusivityKey)
WHERE ExclusivityKey IS NOT NULL
  AND Status IN ('Pending', 'Running');
```

`IFlowRunStore` 在一个 `IUnitOfWork` 事务内创建 FlowRun 与不可变 FlowRunDefinition 快照，并将唯一约束冲突映射为：

```text
flow.run_already_active
The flow already has an active run and does not allow concurrent execution. 该流程已有活动运行实例，不允许并发执行。
HTTP 409 Conflict
```

Application/API 不直接使用 SqlSugar、SqliteDatabase 或原始 SQL。

## 4. 受控并发调度器

新增 `RunExecutionOptions`：

```json
{
  "QueueCapacity": 100,
  "MaxConcurrentRuns": 4,
  "MaxConcurrentListenerRuns": 1,
  "MaxConcurrentRunsPerProject": 2,
  "QueueWaitTimeoutSeconds": 60,
  "ShutdownGracePeriodSeconds": 10
}
```

调度器由有界 `Channel<RunWorkItem>`、等待容量许可、全局 Worker 许可、监听器许可、项目许可及活跃运行注册表组成。

规则：

1. 队列容量已满时，在创建运行记录前返回 `429` / `run.queue_full`。
2. 排队任务持有容量许可，直到真正获得运行许可，避免调度器的内存待处理集合无限增长。
3. 调度器选择最早的“可同时获得全部许可”的任务；因同项目配额或监听器配额阻塞的任务暂时跳过，避免队头阻塞其他项目。
4. 启动 Worker 后状态从 `Pending` 转为 `Running`，并释放等待队列容量许可。
5. Worker 结束、取消、超时、崩溃或持久化失败时，所有许可必须在 `finally` 中释放。
6. 无上级 Execution 连接的 Flipflop 使 `IsListenerRun = true`，它既占用全局 Worker 许可，也占用监听器许可。
7. 排队超时从 `QueuedAt` 开始计算；流程执行超时从 `StartedAt` 开始计算，二者独立。

取消规则：已排队任务直接标记取消且不启动 Worker；运行中任务下发 `worker.cancel`，宽限期结束后由 Supervisor 终止进程树。关闭 API 时停止接收新任务、取消活跃运行并等待宽限期。

## 5. Worker Run 内类库缓存

Worker Runner 为每个请求创建 `WorkerLibraryRuntimeCache`，Action 与 Flipflop 执行器共享它。

缓存范围仅限当前 Worker Run：

| 资源 | 缓存键 |
| --- | --- |
| 解压后的类库根目录 | `LibraryId` |
| collectible `AssemblyLoadContext` 与 `Assembly` | `LibraryId + DllName` |
| CLR 类型 | `LibraryId + DllName + ClassName` |
| `MethodInfo` | `LibraryId + DllName + ClassName + 完整参数签名` |
| 构造函数激活器 | CLR 类型 |

实现要求：

- `ConcurrentDictionary` + `Lazy<Task<T>>` 确保并发首次加载同一 DLL 仅执行一次。
- 使用 `AssemblyDependencyResolver` 从解压目录定位托管依赖。
- 默认每次节点调用仍创建类库实例；不缓存用户实例，防止全局 Flipflop 与主流程发生状态串扰。
- Worker Run 结束时统一卸载全部 AssemblyLoadContext，并尽力清理临时目录；清理失败只写诊断，不改变既有运行结果。
- stdout 始终只承载 Worker JSON 协议，诊断只进入 stderr/Supervisor 日志。

## 6. API 与运行控制台

新增运行查询接口：

```text
GET /api/runs/overview
GET /api/runs?status=Pending,Running&projectId={projectId}
```

`overview` 返回队列容量、等待数量、活动 Worker 数、监听器数量、并发限制及近期运行摘要。运行列表返回运行策略、是否监听型、排队/开始/结束时间和可取消状态。

前端首次打开显示“运行控制台”，而不是流程编辑器。控制台包含：

```text
顶部：项目切换、队列容量、活动 Worker、监听器计数、语言切换
主体：等待队列、正在运行、近期结束运行
操作：查看运行、取消运行、打开项目、打开对应 Flow 编辑器
```

编辑器仍是完整工作区，但只在用户选择“编辑流程”、从运行实例打开其项目/Flow，或新建项目后进入。运行控制台使用现有 SignalR 优先、SSE 降级机制刷新活动状态，并在首次加载时通过 REST 获取基线数据。活动运行的订阅必须在离开控制台或运行变为终态后释放。

视觉约束：沿用 Minimalism / Swiss Style，信息采用紧凑表格与状态色，不使用营销式首屏、装饰性大卡片或固定遮挡面板；移动端使用清晰的视图切换。

## 7. 实施顺序

1. 领域 DTO、Schema、持久化记录与 SQLite 迁移。
2. 原子独占准入、运行概览和队列查询 API。
3. 有界调度器、全局/项目/监听器许可、取消、超时、恢复。
4. WorkerLibraryRuntimeCache 及 Action/Flipflop 执行器重构。
5. 运行控制台首屏、编辑器路由状态、Flow 运行策略编辑器与本地化。
6. 单元、集成、并发、Worker 协议和前端回归验证。

## 8. 验收标准

- 同一 `ExclusiveReject` Flow 的并发启动请求仅成功一个，其他请求返回 409，且不会入队。
- `Parallel` Flow 可同时运行，任意时刻 Worker 数不超过 `MaxConcurrentRuns`。
- 长期 Flipflop 运行不超过监听器配额，且不会让普通流程永久饥饿。
- 队列满、排队超时、取消、API 重启、Worker 崩溃后不遗留永久 `Pending`、`Running` 或独占锁。
- 同一个 Worker Run 内同一 DLL 只加载一次，Action 与 Flipflop 共享程序集和方法缓存。
- 浏览器刷新后仍首先进入运行控制台，不会自动打开画布编辑器；控制台可显示等待、运行和最近结束的实例，能够取消实例并进入流程编辑器。
- 所有开发者错误提示遵循英文在前、中文在后；前端界面按当前语言单独显示。

## 9. 企业控制台增量实施（2026-08-26）

本增量将原有的单页运行控制台重构为企业后台工作台，默认入口仍然是跨项目运行视图，流程画布只在明确打开项目后出现。

### 已完成

- [x] 固定左侧导航：控制台主页、项目清单、流程队列、环境设置、环境接口；窄屏自动改为横向导航。
- [x] 控制台主页提供队列容量、活动 Worker、监听型 Worker、单项目上限和最近运行的快速预览。
- [x] 项目清单改为项目卡片，显示项目名称、流程数量、画布数量、节点数量和创建时间，可直接进入编辑器。
- [x] 流程队列按创建时间倒序显示，提供排队中、运行中、已完成、失败等状态的可视化，以及只读运行快照弹窗。
- [x] 环境设置由 SQLite 持久化，支持在线调整队列容量、普通 Worker 上限、监听型 Worker 上限、单项目上限、排队超时、关闭宽限期和同步接口等待上限。
- [x] 调度器采用逻辑容量预留和动态许可校验；调低上限不会中止既有 Worker，只会阻止新实例获取执行位。
- [x] 环境接口支持发布、编辑、启停、删除及调用地址复制；同步调用在等待期内返回 `taskId` 和节点输出数组，超时后返回可轮询任务；异步调用立即返回 `202` 与 `taskId`。
- [x] 增加 `RunEnvironmentSettings`、`FlowInterfaces` 数据表和迁移版本 `8`，Infrastructure 通过仓储抽象完成存取。
- [x] 前端持续使用 SignalR 优先、SSE 降级的活动运行订阅；界面文本和运行提示随当前中英文语言模式即时切换。

### 本轮验证

- `dotnet build src/SereinFlow.Api/SereinFlow.Api.csproj --no-restore`：通过，0 errors。
- `dotnet test tests/SereinFlow.ArchitectureTests/SereinFlow.ArchitectureTests.csproj --no-restore`：20/20 通过。
- `dotnet test tests/SereinFlow.Infrastructure.Tests/SereinFlow.Infrastructure.Tests.csproj --no-restore`：10/10 通过。
- Vue `vue-tsc -b` 和 Vite production build：通过。
- 隔离本地链路验证：项目创建、运行快照、同步/异步第三方接口调用、项目卡片、队列表格、快照只读弹窗、环境设置和桌面/390px 响应式布局均已检查。
