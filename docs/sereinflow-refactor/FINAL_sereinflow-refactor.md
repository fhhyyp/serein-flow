# SereinFlow 重构规划阶段最终结论

> 交付日期：2026-08-22
>
> 交付类型：架构分析、目标设计与开发计划，以及 Automate T0-T7 第一轮 / S5 基础壳进展；不是代码迁移完成报告。

## 1. 结论

SereinFlow 将在 `D:\Project\dotnet\SereinFlow` 以绿地方式重构，不把旧 solution 直接升级。目标固定为：

- 全部新 C# 工程 `net10.0`，Linux 部署。
- Vue 3 + TypeScript Minimalism & Swiss Style 工作台。
- ASP.NET Core REST/OpenAPI + SignalR，SSE 降级。
- SQLite + SqlSugar 保存项目、流程版本、运行和事件。
- API 与不受信任执行严格分离；Supervisor 创建一次性 Runner。
- Runner 内允许 file/network/process/timer/动态程序集/CLR。
- 新 SereinScript 语义的脚本/条件/取值/表达式节点。
- 每次项目加载清理并重建 `.ssc`，失败不回退旧制品。

明确不做：旧 `.dnf` 兼容、Workbench、离线模式、旧 `Serein.Script`、NetScript、流程图导出 C#、登录/角色/权限/审计/多租户/API Key。

## 2. 关键技术判断

1. 旧 Runtime 的共享 IOC、对象池、CTS 和清理不适合服务端并发，必须重写 `FlowExecutionSession` 所有权。
2. ScriptLang 第一轮生产化已接通 ScriptTask/VM/循环/import/timer 取消并将全局槽位改为 Engine-owned；ScriptAdapter 已完成 JSON 边界、脚本节点执行和 `.ssc` 重建第一轮，确定性 CLR 重载/转换和 Worker 级终止仍是 T7 P0。
3. 全能力脚本意味着进程拆分必须配合独立 UID/namespace/mount/secret 和资源限制。API 或 Supervisor 不能加载 ScriptLang/插件。
4. SQLite 只由 API 访问；Worker 接收版本化运行快照并返回 DTO/事件。
5. 前端使用 Vue Flow、Pinia、Vue Query、Monaco/LSP 和虚拟列表；Swiss 设计服务于高密度操作工作台，不采用营销页或卡片门户。

## 3. 交付状态

- Align：完成。
- Consensus：完成。
- Architect：完成。
- Atomize：完成，T0-T12。
- Approve：已完成，用户已批准进入 Automate。
- Automate：T0-T6、T7 Worker 第一轮、S5 Vue Swiss 工作台基础壳及 T10 本地图编辑器/编辑历史切片已完成；T7 外部插件/Linux 强隔离与 T8-T12 的完整范围尚未完成。
- Assess：T0-T7 第一轮、S5 基础壳及 T10 的多画布/双连接语义/中英文/本地编辑历史切片已完成阶段性验收，端到端 API/实时闭环仍未完成。

旧 SereinFlow 和 SereinScript 的既有构建基线均成功。新仓库 T0-T7 第一轮已完成：锁定还原、Release 构建 0 警告/0 错误，后端全量测试通过（Domain 9、Runtime 5、Infrastructure 4、ScriptAdapter 6、Worker 6、架构门禁 16 项），前端冻结安装与生产构建通过，Vite 开发服务可用；SereinScript 新增隔离测试 2 项通过并以 `Serein.ScriptLang 0.1.0-sf.3` 接入。新增的 T10 切片以 `@vue-flow/core` 完成多画布图编辑、执行/参数数据两类连接和中英文切换，并进一步交付生命周期画布增删、50 步编辑历史、快捷键和版本化 localStorage 工作副本；本轮通过 TypeScript、Vite、HTTP 与历史/存储模块自动检查。没有修改或删除旧 SereinFlow 项目。

## 4. 下一步

T7 已完成协议与一次性 Runner 第一条垂直切片，合同记录于 `WORKER_PROTOCOL_V1.md`。下一步完成 T7 的外部 DLL/CLR/Linux 强隔离门禁，并进入 T8 将 SQLite 流程快照、Worker Client、运行事件持久化和 REST API 接通；随后把现有 Vue Flow 编辑器接入保存、校验和 SignalR/SSE。首个完整纵向切片仍是：新建项目 -> SQLite 保存流程 -> Runner 执行 Action + Script 节点 -> API 持久化事件 -> Vue 实时展示。
