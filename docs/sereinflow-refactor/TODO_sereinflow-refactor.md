# SereinFlow 重构待办与决策清单

> 状态：P0/P1 产品与架构决策已完成；用户已批准进入 Automate，T0-T6 与 S5 基础壳已交付，剩余事项按依赖顺序实施。

实施目标：`D:\Project\dotnet\SereinFlow`。

## 已确认决策

- [x] 全部新后端/Runtime/Worker 使用 `net10.0`。
- [x] Worker 内允许 file、network、process、timer、动态程序集和 CLR 类型。
- [x] 外部 DLL 与脚本只在独立 Worker Runner 执行，API 不加载不受信任代码。
- [x] 不兼容旧 `.dnf`，不建设 LegacyAdapter。
- [x] 前端使用 Vue 3 + TypeScript；其余采用已建议的 Vue 生态栈。
- [x] 后端使用 ASP.NET Core Web API + SignalR，并提供 SSE 降级。
- [x] 第一期不实现登录、角色、项目权限、审计、多租户和 API Key。
- [x] 不保留 Workbench 和离线模式。
- [x] 不保留流程图导出 C# 和旧 NetScript 执行路径。
- [x] 项目和运行数据使用 SQLite + SqlSugar，支持 Linux 部署。
- [x] 允许保存 `.ssc` 编译制品；每次项目加载自动清理并全量重建。

## Approve 门禁

- [x] 用户批准建立 Git 仓库并开始执行开发计划。
- [x] `DEVELOPMENT_PLAN_sereinflow-refactor.md` 与 `TASK_sereinflow-refactor.md` 的任务边界、依赖和发布门禁已作为实施合同。
- [x] 当前阶段已从 Approve 切换到 Automate，并已完成 T0/T1 的第一轮落地。

旧 SereinFlow solution、旧目录和六个作废项目不会被新仓库修改；SereinScript 在其独立仓库完成生产化并通过内部 NuGet 包交付。

## T0/T1 实施记录

- [x] 固定 .NET 10 SDK、Node.js 24.19.0、pnpm 11.19.0 和 Linux CI 基础环境。
- [x] 建立 `global.json`、集中包版本、NuGet lock file、前端 lock file 和可重复构建命令。
- [x] 从固定 SereinScript 提交生成 `Serein.ScriptLang.0.1.0-sf.3.nupkg`，通过 `NuGet.config` 本地源接入新仓库（T5/T6）。
- [x] 创建新 solution、12 个源项目、8 个测试项目和 Vue 工程。
- [x] 扫描并记录废止边界，建立 API 静态引用与废止组件零引用架构测试。
- [ ] 盘点 NodeGenerator 生成的 partial 属性/通知，并在迁移相关行为前建立等价测试（T2）。
- [ ] 盘点未纳入旧 solution 的目录，只记录，不自动删除（T1 迁移清单补充项）。

## T2 Domain 与 Contracts 实施记录

- [x] 创建 `FlowDefinition`、`CanvasDefinition`、`NodeDefinition`、`ConnectionDefinition`、`ScriptNodeDefinition`、`PluginManifest` 和 `FlowRun`。
- [x] 增加节点 ID、画布 ID、连线端点、参数完整性、入口节点和运行终态规则。
- [x] 创建 REST DTO、Worker 请求/响应、版本化事件信封和 ProblemDetails DTO；DTO 不包含 CLR 类型、反射对象、委托或本地路径。
- [x] Domain 测试 9 项通过；OpenAPI 暴露和 TypeScript 客户端生成留在 T8。

## T3 SQLite 与 SqlSugar 实施记录

- [x] 添加 `SqlSugarCore 5.1.4.217`、`Microsoft.Data.Sqlite 10.0.11` 的集中版本和锁文件。
- [x] 创建 `SchemaMigrations`、`Projects`、`FlowDefinitions`、`FlowDefinitionVersions`、`PluginManifests`、`FlowRuns`、`FlowRunEvents` 表的显式 v1 迁移。
- [x] 启用 WAL、foreign keys、busy timeout；实现项目 Repository、事件存储和乐观版本更新。
- [x] 实现 SQLite 一致性备份 API，并验证备份文件可重新打开和读取。
- [ ] 继续补充流程定义/运行摘要 Repository、并发写重试、迁移升级/降级阻断和 Linux 恢复演练。

## T4 Runtime 实施记录

- [x] 创建 `Runtime.Abstractions` 的执行上下文、节点执行器、运行事件和结果合同。
- [x] 创建 `ExecutionPlanBuilder`、`FlowExecutionSession`、`NodeExecutorRegistry`、`FlowRunner` 和 `ResourceLeaseRegistry`。
- [x] 每次会话独占 CTS、上下文、事件 sequence 和资源清理表；重复取消/重复清理幂等。
- [x] Runtime 测试 5 项通过；完整 Init/Loading/Exit 生命周期、错误分支和 FlowCall/Script 执行待后续 T7 垂直切片。

## ScriptLang 与 Worker

- [x] 将 CancellationToken 接入 ScriptTask、VM 主循环、import 和 timer sleep；函数/异步模块剩余合作式检查在 T6。
- [ ] 完成编译期参数登记与执行期独立值绑定。
- [x] 将生产路径的 `GlobalSlotRegistry` 改为 Engine-owned `GlobalSlotTable`，清缓存不影响其他 Engine；旧静态 API 仅保留兼容演示并标记 Obsolete。
- [ ] 完成确定性 CLR 重载解析和值转换测试矩阵。
- [x] 定义并实现 Worker Protocol v1：版本、握手、deadline、heartbeat/ack、取消确认、事件 sequence、同步背压和 1 MiB 最大消息（见 `WORKER_PROTOCOL_V1.md`）。
- [x] 实现 Supervisor 与一次性 Runner；Action/Script、取消、过期 deadline、异常退出和进程树终止代码路径已有进程级测试。
- [ ] 补充外部 DLL、`Environment.Exit`、不合作 CLR 调用、心跳失联和真实子进程树的黑盒隔离测试。
- [ ] 配置 API/Supervisor/Runner 独立 UID、挂载和 secret；验证 Worker 无法读取 SQLite 与 API 配置。
- [x] 实现 `.ssc` 临时目录全量编译、原子切换和失败不回退（T6 第一轮；项目目录清理、hash 校验和损坏制品拒绝已覆盖）。

## 数据、API 与实时

- [ ] 定义 SQLite schema 与 SqlSugar 映射，建立显式迁移版本。
- [ ] 验证 WAL、busy timeout、并发写、有界重试、备份与恢复。
- [ ] 冻结 v1 OpenAPI 与 Worker DTO，生成严格 TypeScript 客户端。
- [ ] 实现项目/流程/插件/校验/运行/取消/查询 API。
- [ ] 实现 SignalR、SSE fallback、断线重连和 sequence 补偿。
- [ ] 定义运行事件保留期、日志脱敏、指标告警和 SQLite 清理策略。

## Vue 工作台

- [x] 引入 `@vue-flow/core` 1.48.2，完成 T10 本地多画布图编辑器与编辑历史切片：节点添加/拖拽/选择/删除、执行与数据两类连接、参数来源检查器、中文/英文切换、Init/Loading/Exit 生命周期画布、50 步撤销/重做、快捷键和版本化 localStorage 工作副本保存恢复。
- [ ] 建立 Vue Router、Pinia、Vue Query、Tailwind、shadcn-vue/Reka UI，并将 Vue Flow 状态接入 API/OpenAPI 客户端。
- [x] 建立 Swiss Design Token、12 栏工作台壳、Lucide 图标和 Minimalism & Swiss Style 视觉基线（S5 第一轮）。
- [ ] 建立核心 Story/视觉回归。
- [ ] 完成 API 驱动的项目/画布/节点库/检查器/参数编辑、服务端版本冲突处理、复制粘贴/对齐和校验。
- [ ] 完成 Monaco + ScriptLang LSP 网关。
- [ ] 完成运行/停止、SignalR/SSE 状态、日志、时间线和运行详情。
- [ ] 验证 WCAG 2.2 AA、键盘流、375/768/1024/1440 响应式和性能基线。

## 质量与发布

- [ ] 建立 Domain、Runtime、ScriptAdapter、Worker、API、数据库、前端和架构测试流水线。
- [ ] 建立依赖漏洞、容器、secret 和发布包扫描。
- [ ] 运行 SQLite 备份恢复、Runner 崩溃隔离和 API 持续可用演练。
- [ ] 生成 Linux 部署、升级、备份、恢复和回滚手册。
- [ ] 每个迭代同步更新 `ACCEPTANCE`、`FINAL` 和本 TODO。

## 当前无产品级阻断项

完整手机编排不在第一期，默认 `>=1024px` 支持完整编辑，小屏支持查看、运行和监控。若后续扩大到手机完整编辑，应作为独立变更进入下一轮 Align，而不是隐式扩展本计划。
