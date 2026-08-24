# SereinFlow 重构文档索引

本目录是新项目 `D:\Project\dotnet\SereinFlow` 的重构分析与开发计划。旧项目 `D:\Project\C#\DynamicControl\SereinFlow\scr\dotnet` 只作为只读参考；SereinScript 生产化改造在其独立仓库完成并以内部 NuGet 包接入新仓库。

文档阅读顺序：

1. `ALIGNMENT_sereinflow-refactor.md`：原始需求、项目事实和边界。
2. `CONSENSUS_sereinflow-refactor.md`：已经确认的技术与产品共识。
3. `DESIGN_sereinflow-refactor.md`：目标架构、模块、契约、数据流和 Vue Swiss 设计。
4. `DEVELOPMENT_PLAN_sereinflow-refactor.md`：分期开发路线、排期、关键路径和发布门禁。
5. `TASK_sereinflow-refactor.md`：T0-T12 原子任务、输入/输出合同、依赖和验收。
6. `REFRACTOR_ANALYSIS_sereinflow.md`：旧项目与 ScriptLang 的现状分析证据。
7. `ACCEPTANCE_sereinflow-refactor.md`：本轮交付验收和构建基线。
8. `FINAL_sereinflow-refactor.md`：分析阶段结论。
9. `TODO_sereinflow-refactor.md`：已确认决策、Approve 门禁和实施待办。

P0/P1 决策已经确认，用户已批准进入 Automate。T0-T6 基础切片、ScriptAdapter 第一轮和 S5 Vue Swiss 工作台基础壳已完成；后续任务仍不得修改旧 SereinFlow solution 或作废项目。
