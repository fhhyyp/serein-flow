# Web 工作台多画布与国际化验收记录

> 阶段：Assess 完成。
>
> 日期：2026-08-22

## 验收范围

本次验收仅覆盖 Web 工作台本地编辑状态的第一切片：多画布、节点编辑、流程调度连接、方法参数来源连接和中英文切换。流程 REST 持久化、运行和实时事件不在本次验收范围内。

## 功能验收

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| 旧 Workbench 语义映射 | 通过 | 已调研 `FlowEditView`、`FlowCanvasView` 与 `INodeJunction`；多画布和 Execute/Next/Arg/Return 四类端口已写入设计契约。 |
| 多画布隔离 | 通过 | `Main` 新增节点前后由 4 变为 5；切换 `Init` 仍为 2；切回 `Main` 恢复为 5。 |
| 节点编辑 | 通过 | Vue Flow 处理节点创建、拖拽、缩放、平移、选中，以及 Delete/Backspace 删除节点或边。 |
| 流程调度连接 | 通过 | 仅允许 `exec-out -> exec-in`；生成 `execution` 蓝色实线边、Flow 标签和方形端口。 |
| 参数来源连接 | 通过 | 仅允许 `data-out -> param-*`；生成 `data` 紫色虚线边、Value 标签和圆形端口，并同步目标参数为 `previousNode`。 |
| 参数来源一致性 | 通过 | 更改为 literal/projectInput/expression 时移除数据边；删除数据边或相关节点时回退/清理参数引用。 |
| 非法连接保护 | 通过 | 拒绝跨语义连线、自连接和重复的执行边。 |
| 国际化 | 通过 | 默认 `zh-CN`，可切至 `en-US`；同步更新界面文案、`document.documentElement.lang` 和 `localStorage` 中的 `sereinflow.locale`。 |

## 构建与运行验证

在 `frontend/sereinflow-web` 使用已安装的本地二进制执行：

```powershell
& '.\node_modules\.bin\vue-tsc.cmd' -b
& '.\node_modules\.bin\vite.cmd' build
```

结果：TypeScript 检查和 Vite 生产构建均通过。产物为 `dist/assets/index-o1S1O9WU.css`（23.53 kB）和 `dist/assets/index-BQOQ-8Tg.js`（253.23 kB）。

开发服务器 `http://127.0.0.1:5174/` 返回 HTTP 200。无头浏览器运行时验证结果如下：

- 375px CSS 视口中 `innerWidth`、`scrollWidth`、`bodyWidth` 和应用根宽度均为 375，没有页面级横向溢出。
- 切换至 Loading 画布并切换英文后，HTML `lang` 为 `en-US`，项目名显示 `Order pipeline`，活动画布为 `Loading`，该画布独立保留 1 个节点。

## 质量结论

W1-W5 验收通过。实现使用 `@vue-flow/core` 1.48.2 和自定义节点端口，不将 Vue Flow 类型泄漏到 Domain 目标模型；设计保持浅色 Minimalism & Swiss Style，并以线型、端口形状和标签共同区分连接语义。

本轮没有引入旧 Workbench、`.dnf` 兼容、旧 `Serein.Script` 或浏览器端脚本执行。

## 未覆盖项

- 未接入 T8 REST/OpenAPI，因此画布状态目前仅在当前前端会话内保存。
- 未实现 undo/redo、流程校验、路由/Pinia/Vue Query、Monaco/LSP、SignalR/SSE 和运行监控。
- 已做 375px 浏览器验证；768/1024/1440 的正式 Playwright/axe 无障碍和视觉回归门禁尚未建立。
- 小于 1024px 的体验保留查看、画布切换和基础操作，不承诺完整高密度图编排。
