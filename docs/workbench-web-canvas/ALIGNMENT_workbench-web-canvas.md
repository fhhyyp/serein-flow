# Web 工作台多画布与国际化需求对齐

> 阶段：Align 完成。用户已明确授权自动执行，本文和后续 Consensus 作为本轮实施依据。
>
> 日期：2026-08-22

## 原始需求

1. 参考旧 `D:\Project\C#\DynamicControl\SereinFlow\scr\dotnet\Workbench` 的画布设计，实现多画布、节点拖拽和节点连线。
2. 节点连接必须区分流程调度连接与方法参数来源连接。
3. 门户/工作台需要中文语言配置并支持中英文切换。

## 现有项目理解

- 新项目是 Vue 3 + TypeScript + Vite 前端，当前 `App.vue` 仅为静态节点和 SVG 连线示意，不具备真实图编辑能力。
- 旧 Workbench 的 `FlowEditView` 以标签页承载多个 `FlowCanvasView`；每个画布独立维护节点和连接。
- 旧 `INodeJunction` 明确定义四类连接点：`ExecuteJunction`、`NextStepJunction`、`ArgDataJunction[]`、`ReturnDataJunction`。旧画布分别创建 Invoke（流程调度）和 Arg（参数来源）连接。
- 新 Domain 已有 `CanvasDefinition`、`ConnectionDefinition`、`ConnectionKind.Execution` 与 `ConnectionKind.Data` 的目标语义，本轮前端模型应与其保持一致。

## 本轮边界

包括：

- 在当前单页工作台中引入 `@vue-flow/core`，提供可拖拽、缩放、平移、选择、删除和连线的真实编辑画布。
- 为 Main、Init、Loading 三个独立 Canvas 提供切换和分别保存的节点/边工作副本。
- 将流程调度边和参数来源边以不同端口、线型、标签和检查器描述表达。
- 为方法参数提供 `literal`、`previousNode`、`projectInput`、`expression` 四种来源；图数据连接只表达 `previousNode`。
- 为全部新/修改工作台文案提供 `zh-CN` 与 `en-US` 字典，并从顶栏即时切换。
- 维持已确认的浅色 Minimalism & Swiss Style 设计令牌与 Lucide 图标体系。

不包括：

- API 流程 CRUD、后端持久化、SignalR/SSE、Monaco/LSP、真实脚本运行、撤销重做和路由/Pinia 重构。
- 旧 `.dnf`、WPF、Workbench、旧脚本和 C# 导出兼容。
- 本期不在小于 1024px 视口提供完整编排；小屏保留查看、画布切换、选择、参数查看和运行入口。

## 风险与假设

| 项目 | 处理 |
| --- | --- |
| Vue Flow 的端口模型与 Domain 不完全同构 | 用稳定的 handle ID 前缀将 UI 语义映射为 `execution` 或 `data`，不把 Vue Flow 类型传递到 Domain。 |
| 当前没有 API 保存端点 | 本轮每个画布状态保留在前端内存；后续 T8 用 REST DTO 替换初始化数据。 |
| 用户未要求复刻 WPF 的全部鼠标/快捷键行为 | 保留其多画布、节点拖拽和两类连接核心语义；采用 Web 标准选中、删除、平移缩放交互。 |
| 术语不一致造成误连 | 用端口形状、线型、边标签、检查器来源标签共同传达语义，禁止仅依赖颜色。 |

## 待确认问题

无阻断问题。用户已要求自动执行；前端持久化与完整移动端编辑留在已规划的 T8/T10 后续切片。
