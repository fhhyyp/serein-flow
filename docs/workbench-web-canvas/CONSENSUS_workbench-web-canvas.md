# Web 工作台多画布与国际化共识

> 阶段：Consensus 完成。用户的“无需向我确认，自动执行”授权构成 Approve 门禁通过。

## 需求描述

SereinFlow Web 工作台应提供旧 Workbench 核心能力的现代 Web 实现：一个流程可包含多个画布；每个画布可独立编排节点和连接；连接分为流程调度与方法参数来源两种语义；操作门户支持简体中文和英文。

## 验收标准

- 初始显示 `Main`、`Init`、`Loading` 三个可切换画布，切换后节点和边不混合。
- 用户可从左侧节点库添加节点，拖拽节点改变位置，选择节点，并删除选中的节点或边。
- `exec-success`、`exec-failure`、`exec-error` 仅可连至 `exec-in`，创建为带对应 `Success`、`Failure`、`Error` 分支的 `execution` 边。
- `data-out` 仅可连至目标节点参数 `param-*`，创建为 `data` 边，使用虚线和“Value”标签，并将目标参数来源更新为 `previousNode`。
- 不允许不同语义交叉连接、同节点自连接，或为同一执行输出重复创建同一目标流程边。
- 选择节点时，检查器可编辑名称、描述及其参数来源；切换参数来源会移除相应的数据边；删除数据边会回退参数来源。
- 顶栏可在中文/英文间切换，画布、节点库、检查器、状态和无障碍标签即时更新，`document.documentElement.lang` 与当前语言一致。
- `pnpm build` 通过，且应用在 375、768、1024、1440px 的布局没有页面级水平滚动或面板重叠。

## 技术方案

- 使用 `@vue-flow/core` 处理节点位置、画布平移缩放、连接手势、边渲染与选中/删除事件。
- 以 `CanvasState` 隔离每一个画布的节点和边，当前 Canvas 通过 ID 选择；不共享可变 nodes/edges 数组。
- 采用自定义 Vue Flow 节点组件，把执行入口/出口、数据输出和参数入口表示为明确 Handles。
- 用轻量 `i18n.ts` 消息字典和 `ref<Locale>` 实现本轮国际化；所有新增/替换工作台文案经 `t()` 输出。
- 遵循项目 Swiss 令牌：白色表面、浅灰画布、`#0369A1` 主交互色、8px 间距、0-4px 圆角、无渐变/发光/装饰性阴影。UI/UX PRO MAX 的实时监控建议已作为参考；因本工作台既定的浅色 Minimalism & Swiss 基线，本切片不采用其默认的 OLED 深色方案。

## 集成方案

前端现有 mock graph 是 T10 的本地工作副本。未来 REST 读取将映射为 `CanvasState`，保存时再映射回 Domain DTO；映射规则如下：

| Web 模型 | Domain 目标 |
| --- | --- |
| `edge.data.semantic = 'execution'` | `ConnectionKind.Execution` + `ExecutionBranch` |
| `edge.data.semantic = 'data'` | `ConnectionKind.Data` + `DataSource.PreviousNode` |
| `literal` / `projectInput` / `expression` | `DataSource.Literal` / `ProjectInput` / `Expression`，无图边 |

API、脚本运行和不受信任代码继续停留在既定 Worker 边界；浏览器不加载脚本或插件程序集。
