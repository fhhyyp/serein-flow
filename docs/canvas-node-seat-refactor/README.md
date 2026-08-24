# Canvas, Node, and Project Interaction Refactor

本次重构将参考 `D:\Project\TRAE\SereinFlow\client` 的连接席位思路迁移到现有 Vue Flow 工作台，同时保留 SereinFlow 当前 Minimalism & Swiss Style 视觉语言。

- 节点连接席位：流程调度输入/输出、方法参数输入、数据结果输出。
- 绘制方式：节点库支持点击添加，也支持拖拽到画布坐标创建节点。
- 连接约束：流程席位只接受流程语义，数据输出只接受参数席位；参数席位默认单连接。
- 画布：继续支持多画布、切换时重建 Vue Flow 内部状态、独立保存节点与连接。
- 项目：顶部项目选择器支持已有项目切换，并可创建新的纯净项目草稿。

