# SereinFlow 重构共识

> 状态：需求共识已形成，开发计划已批准并进入 Automate。
>
> 决策日期：2026-08-22

新项目根目录：`D:\Project\dotnet\SereinFlow`。旧项目 `D:\Project\C#\DynamicControl\SereinFlow\scr\dotnet` 只读；ScriptLang 改造源码位于 `D:\Project\C#\SereinScript\SereinScript`。

## 1. 需求描述

SereinFlow 重构为前后端分离的流程编排与运行平台：

- 浏览器中的 Vue 3 工作台负责项目、流程编辑、脚本编辑、运行控制和监控。
- ASP.NET Core API 负责项目/流程数据、版本并发、运行编排、查询与实时事件出口。
- 独立 Linux Worker 负责流程 Runtime、SereinScript 脚本、外部 DLL、动态程序集和 CLR 调用。
- 项目与运行数据由 SQLite + SqlSugar 持久化。
- 前端遵循 UI/UX PRO MAX 的 Minimalism & Swiss Style。

## 2. 已确认技术决策

| 范围 | 结论 |
| --- | --- |
| TFM | 新后端、Runtime、Worker 和测试统一 `net10.0` |
| 前端 | Vue 3 + TypeScript + Vite |
| 后端 | ASP.NET Core Web API + OpenAPI |
| 实时 | SignalR 主通道，SSE 降级 |
| 数据 | SQLite + SqlSugar |
| 部署 | Linux |
| 脚本与插件 | 只在独立 Worker Runner 加载和执行；API 进程不得加载 |
| 脚本能力 | Runner 内允许 file/network/process/timer/动态程序集/CLR 类型 |
| 缓存 | 每次项目加载清理并全量重建 `.ssc`；源码是唯一事实来源 |
| 兼容 | 不兼容旧 `.dnf`，不建设 LegacyAdapter |
| 桌面端 | 不保留 Workbench，不提供离线模式 |
| 代码生成 | 不保留流程图导出 C#；不保留 NetScript 执行 |
| 身份功能 | 第一期不实现登录、角色、项目权限、审计、多租户和 API Key |

“允许脚本能力”不表示 API 与 Worker 共享宿主权限。安全边界由独立进程、独立 Linux 身份/命名空间、受控挂载、无 API secret、无 SQLite 文件权限、资源配额和进程树终止提供。API 与 Runner 使用同一 UID、同一文件挂载或同一环境变量集，不满足本共识。

## 3. 任务边界

### 3.1 纳入第一期

- 新 schema 的项目、流程、画布、节点、连线和参数模型。
- Action、FlowCall、GlobalData、Flipflop、Init/Loading/Exit 等运行语义。
- 使用新 SereinScript 语义的完整脚本、条件、取值和表达式节点。
- 外部程序集 manifest、Worker 加载与节点目录。
- 项目/流程 CRUD、校验、运行、取消、状态、事件和日志 API。
- Vue 画布编辑、属性检查、Monaco/LSP、运行监控和错误诊断。
- SQLite 数据迁移、备份恢复与 Linux 发布。

### 3.2 明确排除

- 六个作废项目：
  `Serein.Library.NodeGenerator`、
  `Serein.Proto.HttpApi`、
  `Serein.Proto.Modbus`、
  `Serein.Proto.WebSocket`、
  `Serein.Workbench.Avalonia`、
  `Serein.CollaborationSync`。
- 旧 `Serein.Script`、WPF Workbench、Avalonia 客户端和 CollaborationSync。
- 旧 `.dnf` 读取、迁移、回写或兼容测试。
- 旧脚本语法自动转换、旧 NetScript/C# 脚本执行、流程图 C# 导出。
- 登录、授权、审计、多租户和 API Key 管理。

## 4. 目标模块

```text
SereinFlow.Domain               纯领域模型与规则
SereinFlow.Contracts            REST DTO、错误与实时事件
SereinFlow.Application          项目/流程/运行用例和端口
SereinFlow.Infrastructure       SqlSugar、SQLite、配置与日志
SereinFlow.Runtime.Abstractions Application 到执行端的稳定合同
SereinFlow.Runtime              执行计划、会话、节点调度和生命周期
SereinFlow.ScriptAdapter        ScriptLang 封装、转换、诊断和 .ssc 制品
SereinFlow.Worker.Protocol      版本化 IPC 合同
SereinFlow.Worker.Client        API 侧 Worker 端口实现
SereinFlow.Worker.Supervisor    进程管理、心跳、deadline 和崩溃恢复
SereinFlow.Worker.Runner        Runtime、脚本和外部 DLL 的执行进程
SereinFlow.Api                  ASP.NET Core、OpenAPI、SignalR/SSE
frontend/sereinflow-web         Vue 3 工作台
tests/*                         单元、集成、架构、E2E 和发布测试
```

Domain 与 Contracts 相互独立，由 Application/API 映射。API 不引用 Runtime 实现、ScriptAdapter、ScriptLang 或插件程序集；Runner 不访问 SQLite。旧 Library/NodeFlow 的有效业务语义按上述新边界重写，不把旧工程直接复制进新 solution。

## 5. ScriptLang 接入共识

ScriptLang 当前不能直接作为生产引擎，必须先完成：

1. `CancellationToken` 贯穿 ScriptTask、VM、循环、函数、import 与异步模块。
2. 外部参数名称参与编译，值在每次执行时独立绑定。
3. `GlobalSlotRegistry` 从进程级静态状态改为 Engine/ExecutionContext 实例状态；`ClearCache()` 不影响其他会话。
4. CLR 重载解析和值转换确定化，覆盖 decimal、enum、Nullable、Guid、集合、异步返回和异常封装。
5. 字节码元数据版本化，并从固定提交生成内部 NuGet 包供 SereinFlow 使用。

新脚本节点使用显式输入/输出合同，不复刻旧 `ParserScript(...): Type` 推导。IPC 只传递 DTO，不传递 `Type`、`MethodInfo`、委托、CLR 对象或本地路径。

`.ssc` 是可丢弃制品。每次项目加载必须清理该项目脚本制品，在临时目录全量重建并原子切换；编译失败时禁止运行旧缓存。缓存身份包含源码/import hash、参数与输出合同、Host API ABI、ScriptLang/编译器/字节码版本、RID 与 CPU 架构。

## 6. 验收标准

### 6.1 架构

- 全部新 C# 项目目标 `net10.0`，Linux 构建和发布通过。
- solution、项目引用、发布目录和进程加载列表中没有作废项目、`Serein.Script`、Workbench、LegacyAdapter 或 C# 导出器。
- API 架构测试证明它不引用或加载 ScriptLang、ScriptAdapter、插件和外部 DLL。
- Runner 崩溃、`Environment.Exit`、超时或进程树终止不影响 API 可用性。

### 6.2 数据与 API

- 新项目、流程版本、插件 manifest、运行和有序事件可通过 SQLite/SqlSugar 正确持久化。
- 乐观并发、事务回滚、WAL 并发写、迁移、备份和恢复测试通过。
- REST 合同覆盖创建、读取、保存、校验、运行、取消和查询；冲突使用 `409`，错误使用 ProblemDetails。
- SignalR 正常连接；WebSocket 被阻断时 SSE 降级；断线后按 sequence 补偿。

### 6.3 Runtime 与脚本

- 每次运行拥有独立取消、上下文、事件序列和资源清理。
- 并发 Engine/会话以及 `ClearCache()` 不串值或清空其他运行。
- 无限循环、timer 和 import 可取消；不合作 CLR/子进程由 Supervisor 终止 Runner 进程树。
- file/network/process/timer/动态程序集/CLR 在 Runner 内可用，API 路径不可用。
- 每次项目加载均重建 `.ssc`；损坏、过期、跨 RID 和编译失败场景不会执行旧制品。

### 6.4 Vue 工作台

- 使用 Vue 3 Composition API、严格 TypeScript、Vue Router、Pinia、Vue Query 和 Vue Flow，不存在 React 依赖。
- 完成项目创建、流程保存、节点编辑、脚本诊断、校验、运行、停止、日志和运行详情 E2E。
- UI 使用 12 列/8px Swiss 网格、单一交互强调色、`0-4px` 普通圆角、Lucide、无渐变/玻璃态/装饰阴影。
- WCAG 2.2 AA；375/768/1024/1440 无元素重叠和页面级横向滚动。
- 1000 节点/1500 连线首次可交互 <= 3 秒，平移缩放 p95 >= 50 FPS；100 事件/秒呈现 p95 <= 250ms。

## 7. 集成原则

- OpenAPI 是 API 与 Vue 的唯一业务合同来源。
- Worker Protocol 是 API/Application 与执行侧的唯一合同来源。
- SQLite 只在 API/Infrastructure 边界访问；Worker 通过运行快照和事件 DTO 工作。
- SignalR 与 SSE 使用同一事件信封：`runId`、`sequence`、`timestamp`、`type`、`nodeId`、`payload`。
- 取消幂等；重复取消返回当前状态。运行最终状态由持久化记录确认，不以浏览器连接状态推断。
- 第一期无应用内鉴权，因此部署必须通过受控网络或反向代理限制可达范围；这不是新增登录功能。

## 8. Approve 条件

需求、边界、验收标准和技术路线已明确。用户已批准 `DEVELOPMENT_PLAN_sereinflow-refactor.md` 与 `TASK_sereinflow-refactor.md`，当前进入 Automate；T0/T1 的工程基线和架构门禁已经落地。
