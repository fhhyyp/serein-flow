# SereinFlow 重构方案验收记录

> 阶段：Automate / T0-T7 第一轮、S5 基础壳验收。本轮验收对象是新仓库工程基线、Domain/SQLite/Runtime、ScriptAdapter、Worker 第一条进程隔离切片和 Vue Swiss 工作台基础壳；API/实时业务闭环仍按 T8-T12 逐步实现。
>
> 记录日期：2026-08-22

## 1. 文档交付

| 交付物 | 状态 | 说明 |
| --- | --- | --- |
| `ALIGNMENT_sereinflow-refactor.md` | 通过 | 原始需求、确认决策、现状、边界和风险 |
| `CONSENSUS_sereinflow-refactor.md` | 通过 | 已确认的产品和技术共识 |
| `DESIGN_sereinflow-refactor.md` | 通过 | 架构、模块、合同、数据流、异常、Vue Swiss 与 Linux 隔离 |
| `DEVELOPMENT_PLAN_sereinflow-refactor.md` | 通过/实施中 | S0-S7 排期、并行路径和发布门禁 |
| `TASK_sereinflow-refactor.md` | 通过/实施中 | T0-T12 输入/输出/约束/验收/依赖 |
| `REFRACTOR_ANALYSIS_sereinflow.md` | 通过 | 旧项目与 ScriptLang 综合分析 |
| `TODO_sereinflow-refactor.md` | 通过 | 已确认决策、Approve 门禁和实施待办 |
| `WORKER_PROTOCOL_V1.md` | 通过 | JSONL v1 信封、状态机、上限、错误码、背压和隔离边界 |

## 2. 调查验收

- [x] 确认旧项目、新项目和 SereinScript 的实际路径。
- [x] 确认六个作废项目与 `Serein.Script` 仍存在于旧构建/引用图。
- [x] 确认 NodeGenerator 虽作废，但生成成员仍被旧生产代码使用。
- [x] 检查 Library、NodeFlow、Workbench、FlowStartTool 的职责和耦合。
- [x] 覆盖 `SingleScriptNode`、条件/表达式节点、Host API、NetScript 和 C# 生成路径。
- [x] 确认旧 Runtime 的 CTS、IOC、对象池和清理不能直接包装为并发服务。
- [x] 确认 ScriptLang 的取消链路、Scope 参数、静态槽位和 CLR 重载问题。
- [x] 确认 ScriptLang file/network/process/timer 等能力必须只存在于 Runner。
- [x] 确认旧项目没有正式自动化测试套件。
- [x] 使用 UI/UX PRO MAX 复核 Minimalism & Swiss Style，并按 Vue 3 栈改写。
- [x] 将用户确认的 `.NET 10`、Worker、SQLite/SqlSugar、Linux、SSE、无兼容和无身份体系决策写入基线。

## 3. 构建基线

### 3.1 旧 SereinFlow

```text
dotnet build D:\Project\C#\DynamicControl\SereinFlow\scr\dotnet\SereinFlow.sln --no-restore --nologo -v:minimal
```

既有验证结果：成功，0 errors，约 1002 warnings。它只是旧代码参考基线，不是新项目发布门槛。

### 3.2 SereinScript

```text
dotnet build D:\Project\C#\SereinScript\SereinScript\ScriptLang\ScriptLang.csproj --no-restore --nologo -v:minimal
```

既有验证结果：成功，0 errors，约 83 warnings，目标 `net10.0`。当前能构建不代表取消、隔离和参数绑定已满足生产要求。

### 3.3 新仓库 T0/T1

```text
dotnet restore SereinFlow.sln --locked-mode       成功
dotnet build SereinFlow.sln -c Release --no-restore  成功，0 警告，0 错误
dotnet test SereinFlow.sln -c Release --no-build --no-restore  成功，Domain 9、Runtime 5、Infrastructure 4、ScriptAdapter 6、Worker 6、Architecture 16 项通过
pnpm install --frozen-lockfile                  成功
pnpm build                                      成功
GET http://127.0.0.1:5187/healthz              200 {"status":"Healthy"}
```

已创建新 solution、12 个源项目、8 个测试项目、Vue 3 + TypeScript + Vite 前端骨架、SDK/Node/pnpm 固定文件、集中包管理、NuGet 锁文件、CI 和 API/Supervisor 隔离门禁。SereinScript 独立仓库已完成第一轮生产化，并生成 `Serein.ScriptLang.0.1.0-sf.3.nupkg`。

## 4. 已完成决策验收

- [x] 新工程统一 `net10.0`。
- [x] Vue 3 + TypeScript。
- [x] ASP.NET Core REST/OpenAPI + SignalR + SSE fallback。
- [x] SQLite + SqlSugar，Linux 部署。
- [x] API 与不受信任执行分离；Supervisor + 一次性 Runner。
- [x] Worker 内允许 file/network/process/timer/动态程序集/CLR。
- [x] 每次项目加载清理并重建 `.ssc`。
- [x] 不兼容旧 `.dnf`，不保留 Workbench/离线/C# 导出。
- [x] 第一期不实现登录、角色、权限、审计、多租户和 API Key。

## 5. Automate 阶段验收门槛（T0/T1 已通过，T2 基础模型已通过）

- [x] 新 solution、项目引用和 CI 骨架不含作废项目、旧脚本或 Workbench。
- [x] API 架构测试确认不引用 Runtime 实现、ScriptAdapter、ScriptLang 或插件，并对废止组件保持零引用门禁。
- [x] Domain/Contracts 基础模型已覆盖节点、画布、连线、参数、脚本 hash、插件 manifest、运行状态和 Worker 事件 DTO。
- [x] Domain 单测覆盖有效图、重复节点、悬空端点、参数完整性、脚本 hash、终态幂等和 DTO 序列化（9 项通过）。
- [x] SQLite/SqlSugar 基础切片已覆盖显式迁移、WAL/foreign keys/busy_timeout、项目乐观并发、事件 sequence、事务回滚和备份恢复（4 项通过）。
- [x] Runtime 基础切片已覆盖不可变执行计划、会话独立上下文/sequence、取消幂等、资源释放幂等、并发运行隔离和后继优先级（5 项通过）。
- [x] ScriptLang 第一轮生产化：Engine-owned 全局槽位、VM/循环/import/timer 取消链路、原型重复注册幂等、隔离测试 2 项通过；内部 NuGet 包已接入 `SereinFlow.ScriptAdapter`。
- [x] T6 第一轮 ScriptAdapter：JSON/CLR 基础值转换、输入校验、单输出/对象多输出、结构化错误与取消、项目 `.ssc` 全量重建和失败不回退旧制品；适配层测试 6 项通过。
- [x] T7 第一轮 Worker：版本化 UTF-8 JSONL 协议、1 MiB 消息上限、串行 I/O 背压、握手、心跳/确认、deadline、取消确认、严格单调事件 sequence、稳定错误码、一次性 Runner 与进程树终止路径已实现。
- [x] T7 Worker 进程测试：实际 `dotnet` Runner 执行 Action 流程并转发有序事件；ScriptLang 无限循环可由 Supervisor 取消；过期 deadline 不启动 Runner；异常 Runner 退出归类为 `worker.crashed`；协议版本和超限消息拒绝。Worker 集成测试 6 项通过。
- [x] Supervisor 架构门禁：确认不静态引用 Runtime、ScriptAdapter、ScriptLang 或旧 `Serein.Script`；API 仍不静态引用上述不受信任执行组件。
- [x] S5 第一轮 Vue 工作台：命令栏、节点库、流程画布、检查器、事件/载荷输出面板、运行状态切换、节点选择和移动端面板切换；`pnpm build` 通过。
- [x] T10 图编辑器第一切片：以 Vue Flow 实现 `Main`/`Init`/`Loading` 独立画布、节点添加/拖拽/选择/删除、执行调度边、参数来源数据边和参数引用清理；`zh-CN`/`en-US` 切换同步更新根 `lang` 与本地语言偏好。`vue-tsc` 和 Vite 生产构建通过，375px 无页面级横向溢出；详细记录见 `../workbench-web-canvas/ACCEPTANCE_workbench-web-canvas.md`。
- [x] T10 编辑历史切片：实现 `Init`/`Loading`/`Exit` 画布增删、Main 保护、50 步撤销/重做、Ctrl/Cmd 保存和撤销快捷键，以及 `sereinflow.workspace.v1` 的版本化 localStorage 工作副本恢复。历史/存储模块自动检查、`vue-tsc`、Vite 构建和 HTTP 200 通过；详细记录见 `../workbench-editing-history/ACCEPTANCE_workbench-editing-history.md`。

- [ ] SQLite/SqlSugar 全量迁移、并发写压测、有限重试、降级阻断和生产恢复演练通过（基础切片已通过，完整门禁待 T12）。
- [ ] Runtime 会话、取消、生命周期和资源释放测试通过。
- [ ] ScriptLang 确定性 CLR 重载/转换矩阵，以及外部 DLL、`Environment.Exit`、不合作 CLR 调用的隔离与进程树黑盒测试通过（T7 后续）。
- [ ] Linux Runner 独立 UID/mount/namespace、SQLite/API secret 拒绝访问、资源限制和心跳失联门禁通过（T7/T12）。
- [ ] 项目加载无条件重建 `.ssc`，失败不运行旧制品。
- [ ] REST、OpenAPI、SignalR、SSE、重连和 sequence 补偿通过。
- [ ] Vue 工作台的编辑、脚本、运行和监控 E2E 通过。
- [ ] WCAG 2.2 AA、响应式和性能基线通过。
- [ ] Linux 身份、挂载、secret、资源限制和回滚演练通过。

## 6. 验收结论

分析、架构与开发计划文档交付通过；所有用户提供的 P0/P1 选项已经固化，文档之间没有保留旧决策分支。T0-T7 第一轮与 T10 本地图编辑器第一切片已阶段性验收。REST/OpenAPI、持久化保存、实时运行、完整编辑体验和 Linux 发布门禁仍将按依赖顺序继续实现。
