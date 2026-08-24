# Web 工作台编辑历史与本地工作副本验收记录

> 阶段：Assess 完成。
>
> 日期：2026-08-22

## 功能验收

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| 编辑历史 | 通过 | `WorkspaceHistory` 保存图结构快照，限制 50 步；支持 undo/redo，新的编辑会清空 redo 分支。 |
| 记录范围 | 通过 | 节点添加、拖拽、删除、连线、参数来源、节点属性和画布增删均在变更前记录；选择、语言和面板切换不入栈。 |
| 生命周期画布 | 通过 | `Init`、`Loading`、`Exit` 可从紧凑菜单补充；每个类型最多一个；`Main` 删除按钮动态禁用。 |
| 快捷键 | 通过 | 注册 Ctrl/Cmd+S、Ctrl/Cmd+Z、Ctrl/Cmd+Shift+Z、Ctrl/Cmd+Y 和 `/` 搜索；输入/文本域/选择框不会被工作区撤销劫持。 |
| 本地保存 | 通过 | `sereinflow.workspace.v1` 保存版本化快照，包含画布、活动画布和节点 ID 序号；刷新时读取并进行最小结构校验。 |
| 错误回退 | 通过 | 存储解析失败、格式版本不匹配或结构不完整时回退初始画布；保存失败不丢失内存状态，并显示明确状态。 |
| 可访问状态 | 通过 | 撤销、重做、删除画布按钮绑定 `disabled`；画布菜单使用 `role="menu"`/`menuitem`，语言与画布菜单 ARIA 展开状态同步。 |

## 自动化验证

在 `frontend/sereinflow-web` 执行：

```powershell
& '.\node_modules\.bin\vue-tsc.cmd' -b
& '.\node_modules\.bin\vite.cmd' build
```

结果：TypeScript 检查和 Vite 生产构建通过。Vite 构建处理 1736 个模块，输出 `index-Cm32K7y8.css`（24.24 kB）和 `index-Bun4kOyN.js`（259.97 kB）。

工作副本模块使用 Node 22 的 TypeScript 类型剥离直接验证：

- 初始状态 -> 修改状态的撤销恢复。
- 撤销后的重做恢复。
- 新编辑后的 redo 分支清空与 2 步测试上限。
- `localStorage` 保存/加载指纹一致。
- 损坏 JSON 返回 `undefined`，由应用回退初始画布。

开发服务 `http://127.0.0.1:5174/` 返回 HTTP 200。

## 未覆盖项

- 本机 Edge 的一次性截图命令未输出图像，因此本轮未建立真实浏览器点击的截图证据，也未将其标记为通过。
- 未建立 Playwright、axe、375/768/1024/1440 视觉回归和完整键盘焦点门禁。
- localStorage 不是 REST/SQLite 保存，不具备版本冲突、跨设备同步、服务端校验或灾难恢复语义。
