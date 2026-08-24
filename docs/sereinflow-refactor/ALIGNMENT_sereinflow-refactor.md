# SereinFlow 重构需求对齐

> 文档状态：Align 已完成；需求与边界无产品级歧义，已获批准并进入 Automate。
>
> 调查对象：`D:\Project\C#\DynamicControl\SereinFlow\scr\dotnet`。
>
> 新项目：`D:\Project\dotnet\SereinFlow`。
>
> 新脚本引擎源码：`D:\Project\C#\SereinScript\SereinScript`。

## 1. 原始需求

1. 分析现有 SereinFlow 项目架构并生成重构分析报告。
2. 重构为前后端分离架构。
3. 前端遵循 UI/UX PRO MAX 的 Minimalism & Swiss Style。
4. 停止维护旧 `Serein.Script`；脚本节点和执行引擎改用 SereinScript/ScriptLang。
5. 识别旧项目角色：Library 是基础依赖，NodeFlow 是运行环境，Workbench 是 WPF 客户端。
6. 六个指定项目作废，不进入新 solution、发布包或运行依赖。
7. 新项目全部落在 `D:\Project\dotnet\SereinFlow`，旧目录只读。

## 2. 用户补充并确认的需求

- 后端、Runtime、Worker 和测试统一使用 `net10.0`。
- Worker 内允许脚本使用 file、network、process、timer、动态程序集和 CLR 类型。
- 外部 DLL 与脚本必须在独立 Worker 进程运行；API 不加载不受信任代码。
- 不兼容旧 `.dnf`。
- 前端使用 Vue 3 + TypeScript，其余采用建议的 Vue 生态技术栈。
- 后端使用 ASP.NET Core Web API + SignalR，并提供 SSE 降级。
- 第一期不需要登录、角色、项目权限、审计、多租户和 API Key。
- 不保留 Workbench 和离线模式。
- 不保留流程图导出 C#。
- 项目和运行数据使用 SQLite，ORM 使用 SqlSugar，支持 Linux 部署。
- 项目允许保存 `.ssc`；每次项目加载自动清理并全量重建缓存。

## 3. 项目理解

### 3.1 当前结构

| 模块 | 当前职责 | 重构判断 |
| --- | --- | --- |
| `Library` | 节点接口、流程模型、上下文、IOC、工具 | 领域、运行时、序列化和编辑概念混合；只迁移必要语义，不直接复制项目 |
| `NodeFlow` | 节点、画布操作、DLL 加载、流程调度、脚本、C# 生成 | 是行为参考，但共享状态和职责过重；Runtime 需重写会话所有权 |
| `Workbench` | WPF 画布、项目保存、脚本编辑、运行控制 | 不进入新架构；其三栏信息架构只作为 Web UX 参考 |
| `FlowStartTool` | 装配现有运行环境 | 由 API + Worker Supervisor/Runner 取代 |
| `Serein.Script` | 旧解析、解释、类型推导和 C# 转换 | 完全退出新生产路径 |
| `SereinScript/ScriptLang` | 新 Lexer/Parser/Compiler/ByteCode/VM/CLR/LSP | 目标引擎，但需先完成取消、参数绑定、状态隔离和 CLR 绑定改造 |

### 3.2 已验证事实

- 旧 `SereinFlow.sln` 仍包含六个作废项目和 `Serein.Script`。
- `Library` 仍把 `Serein.Library.NodeGenerator` 作为 Analyzer；生成的 partial 属性/通知仍被生产代码使用，不能仅删除引用。
- `NodeFlow` 仍引用 NodeGenerator、旧脚本和 Library；Workbench 仍引用旧脚本。
- 旧脚本入口不只 `SingleScriptNode`，还包括 `SingleConditionNode`、`SingleExpOpNode`、`IScriptFlowApi`、编辑期重解析和 obsolete `SingleNetScriptNode`。
- `FlowCoreGenerateService` 依赖旧脚本方法信息和脚本转 C#；该整条能力按用户决策删除。
- 旧 Runtime 的 `FlowControl`、`FlowWorkManagement`、IOC、对象池、CTS 和清理存在跨会话共享，不能直接包装为服务端并发 Runtime。
- 旧项目没有正式自动化测试套件；当前测试基线为零。
- 旧 solution 在具备 Windows SDK 权限的环境构建成功，约 1002 个 warning；ScriptLang 核心构建成功，约 83 个 warning。
- 旧项目存在弱版本字段和两套保存路径，但由于明确不兼容 `.dnf`，这些只作为现状证据，不形成迁移任务。

### 3.3 ScriptLang 已验证缺陷

- `ScriptTask.Cancel()` 只取消自身 CTS；`RunAsync()`、VM 指令循环、import 和 CLR 调用不读取该 token。
- `CreateTaskFromSource(..., Scope)` 调用的外部 Scope 注册逻辑没有真正把参数名称和值接入 Compiler/VM。
- `GlobalSlotRegistry` 是无锁的进程级静态状态；多个 Engine/会话共享，`ClearCache()` 可重置其他会话。
- CLR 方法访问依赖同名反射查找，重载不确定；值转换对 decimal、enum、Nullable、Guid 和集合不足。
- 默认模块已经暴露 file/network/process/timer 等高权限能力。用户允许这些能力，因此目标不是在产品层默认禁用，而是确保它们只存在于隔离 Runner。

## 4. 任务边界

### 4.1 本轮文档交付

- 旧架构、依赖、Runtime、脚本、持久化和 UI 现状分析。
- 已确认的目标架构、模块边界、API/Worker/事件合同。
- ScriptLang 生产接入前置任务。
- Vue 3 Minimalism & Swiss Style 设计基线。
- 开发迭代、原子任务、依赖、验收和发布门禁。

### 4.2 Automate 阶段包含

- 全新 `net10.0` solution 和 Vue 3 工程。
- 新 Domain、Contracts、Application、Infrastructure、Runtime 和 Worker。
- SQLite/SqlSugar、ASP.NET Core REST/OpenAPI、SignalR/SSE。
- 新 SereinScript 脚本节点、条件/取值/表达式节点、Monaco/LSP。
- 外部 DLL manifest、Worker 加载、Linux 进程隔离和发布。
- 单元、集成、架构、E2E、无障碍、性能和恢复测试。

### 4.3 明确不包含

- 旧 `.dnf` 读取、迁移、回写和兼容承诺。
- Workbench、Avalonia 或其他桌面客户端。
- 离线模式。
- 旧脚本源码自动转换和旧 NetScript 执行。
- 流程图导出 C#。
- 登录、角色、项目权限、审计、多租户和 API Key 管理。
- 修改或删除旧项目目录，除非未来收到单独明确授权。

## 5. 风险与约束

| 等级 | 风险/约束 | 处理方式 |
| --- | --- | --- |
| P0 | 全能力脚本可读取文件、网络、环境并创建/终止进程 | API/Supervisor/Runner 不同边界；Runner 无 SQLite/API secret；独立 UID/namespace/mount；资源配额和进程树终止 |
| P0 | ScriptLang 取消链路不可用 | 先修改 ScriptLang；VM/循环/import/模块读取 token；不可协作调用由 Supervisor 终止 Runner |
| P0 | Scope 未接线、全局槽位静态共享 | 编译期登记参数，运行期独立绑定；符号和值实例化；并发与清缓存压力测试 |
| P0 | API 误引用 ScriptLang 或插件会破坏隔离承诺 | 项目依赖和进程加载架构测试；API 只依赖 Worker.Client/Protocol |
| P1 | NodeGenerator 作废但生成代码仍被引用 | 先盘点生成输出与行为测试，再用显式代码替代 |
| P1 | 旧 Runtime 共享状态无法满足服务端并发 | 重写 `FlowExecutionSession` 所有权，不复用全局清理语义 |
| P1 | SQLite 单写者与高频事件可能产生 busy | WAL、busy timeout、短事务、事件批量、有界重试和恢复测试 |
| P1 | Worker IPC/崩溃恢复是新增复杂度 | 版本握手、deadline、心跳、sequence、背压、最大消息、Supervisor/Runner 分离 |
| P1 | `.ssc` 可能过期、损坏或跨平台不可用 | 每次加载强制清理重建；身份包含源码/依赖/ABI/引擎/RID；失败不回退 |
| P1 | 第一期无身份体系，任何可达客户端都可操作系统 | 作为已接受范围；生产通过受控网络/反向代理限制入口 |
| P2 | Web 画布和实时日志性能风险 | Vue Flow、浅响应式、增量命令、批量刷新、虚拟列表和固定性能样例 |

## 6. 假设

- Linux 运行环境支持 .NET 10、SQLite 文件锁、Unix domain socket、独立 UID 和 cgroup/container 资源限制。
- SereinScript 可以在其独立仓库完成必要修改并产出固定版本内部 NuGet 包。
- 新项目从空白 schema 创建，不需要导入历史项目数据。
- 完整流程编排第一期面向 `>=1024px`；小屏用于查看、运行和监控。
- 第一阶段只有一个可信部署域，不建立应用内用户身份。

## 7. 确认状态

产品与架构 P0/P1 已全部确认，没有剩余需求歧义。唯一流程门禁是：用户批准 `DEVELOPMENT_PLAN_sereinflow-refactor.md` 与 `TASK_sereinflow-refactor.md` 后，才进入 Automate。

## 8. 对齐结论

需求、任务边界、非目标、主要风险和验收方向已经明确。后续以 `CONSENSUS`、`DESIGN`、`DEVELOPMENT_PLAN` 和 `TASK` 为实施基线；旧分析报告中的未决选项如与这些文件冲突，以本轮已确认共识为准。
