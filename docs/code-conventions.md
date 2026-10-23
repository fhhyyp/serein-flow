# 自有代码规范

适用于仓库内的自有 Vue、TypeScript、C# 和 CSS，包括测试、示例、文档站及检查工具。第三方源码、生成文件、依赖与构建产物不纳入调整。

`SereinFlow.Mcp`、`SereinFlow.Mcp.Framework`、`SereinFlow.Mcp.Generator` 及其三个对应测试项目不适用本文的自定义规则，也不进入 C# 规范检查器。它们使用自然的 C# 表达方式，包括直接返回、表达式体、嵌套调用和完整 LINQ 查询；仍遵守基本 EditorConfig、编译检查及行为兼容要求。其他 C# 项目使用下文规则，前端规则保持不变。

安装 `frontend/sereinflow-web` 的依赖后，从仓库根目录运行：

```powershell
pwsh -File ./eng/check-code-conventions.ps1 -SelfTest
```

使用 `.node-version` 指定的 Node 及主站 `packageManager` 指定的 npm。检查输出包含文件、行列和规则编号；发现违规时以非零状态退出。检查器不会重写源码。

## Vue 与样式

- 每行最多一个标签对、自闭合标签或单独的开始、结束标签。属性超过一个时，各属性独占一行。
- 模板允许变量、属性读取、直接取反和参数简单的单次调用。比较、运算、三元、字符串拼接、数组及对象构造移到 computed 或方法中。循环项派生值使用参数化方法或预先整理的数据。
- CSS 每行一个属性声明。按规则块和 at-rule 花括号计数，顶层为第一层，最多三层。选择器的后代组合不算花括号嵌套。
- 调整标签布局时保留必要的文字空格、`v-if`/`v-else` 关联和事件修饰符。

## 调用、声明与控制流

- C# 允许嵌套调用、直接返回调用或 `await` 结果，以及 Lambda 和成员表达式体。不为单纯转交参数或返回值引入临时变量。仅在表达业务含义、复用结果、固定求值时机或管理资源时保留变量；名称应说明用途，避免 `argument4`、`selectResult5`、`evaluatedValue7` 等机械编号。
- C# 检查器通过 `NAME001` 拦截适用项目 `src/` 下的编号式中转变量，例如 `argument4`、`evaluatedValue7`、`selectResult5`。算法、编码和版本名称如 `sha256`、`packageBase64`、`isV2` 保留。测试代码本次不作命名迁移。
- C# 和 TypeScript 允许链式调用，不要求仅为拆开调用链而引入中间变量。LINQ、普通流式 API、集合创建及 JSON 枚举后接查询、`(await ReadAsync()).Where(...)`、可空条件调用链均允许。优先按完整的查询或操作表达业务含义，复杂时再按语义拆分。
- TypeScript 仍要求把参数中的函数或构造调用先存入有意义的临时变量；块体及箭头函数返回调用或 await 结果时先赋值再 return。回调自身不算参数嵌套，但回调内部仍受检查。
- 普通声明每行只声明或初始化一个变量。C# 允许元组解构，包括 `var (a, b)`、`(int a, string b)`、嵌套解构、丢弃值和 `foreach` 解构，不要求拆成多个变量声明。每行一条语句，`for` 头部的分隔符除外。
- `if`、`else if`、`else` 分行书写；分支体始终加大括号。
- 拆分时保留求值顺序、短路分支、异常时机、异步行为、查询延迟执行、资源生命周期及响应性。

## 常量与人工审查

业务阈值、协议值和重复的语义值使用 C# `const`/`static readonly` 或 TS 模块级 `const`。来源明确的默认值、配置、展示文本及类型字面量可保留。禁止通过临时变量简单转存字面量规避规则。

常量命名、来源及业务含义由人工审查；自动检查通过不代表这些语义事项已经验收。审查时也要确认调用拆分没有移动副作用、改变短路条件或扩大变量生命周期。

## JSON 合同与动态数据

- 固定结构的 JSON 合同优先使用具名类型；不要用 `Dictionary<string, object?>` 隐式声明字段结构。
- MCP 工具 Schema 保持手工声明，使用 `McpJsonSchema` 和现有 Schema 构建方法。动态属性名使用 `Dictionary<string, McpJsonSchema>`，采用集合添加初始化方式检测重复键；对象 Schema 构建时检查 `required` 是否重复或引用未声明属性。调整后使用工具目录兼容性基线验证 JSON 结构。
- API 错误通过 `ApiProblemDetailsFactory` 生成 `ProblemDetails`；控制器使用 `ApiProblemWithCode`、`ApiVersionProblem` 和通用 `ApiProblem`。显式空值与字段缺失分别处理，保留任意扩展数据入口。
- JSON-RPC 成功和错误使用独立响应类型，保证 `result` 与 `error` 互斥。成功的 `result: null`、普通错误的 `data: null` 与超限回退中省略 `data` 的既有合同分别保留。
- Runtime 内部使用具名事件载荷，经 `RuntimeEventFactory` 显式映射为公共 `RuntimeEvent.Payload` 字典；不要直接序列化包含脚本值的载荷模型。调试暂停复用 `WorkerDebugPauseDto`，先投影 `Inputs` 再序列化。
- 节点输入输出、运行变量和脚本对象仍可使用动态字典。它们可以持有 CLR 或脚本实例，不应提前转成 JSON；跨 Worker、持久化和客户端边界时继续执行审计投影。

## 必要语法例外

Vue `withDefaults(defineProps<...>(), defaults)` 是编译宏，Vue 编译器要求 `defineProps` 位于宏参数中，保留其语法结构。仅在 `.vue` 中豁免该第一个参数；默认值中的调用及回调内部仍检查。当前位置如下，均位于 `frontend/sereinflow-web/src/components/`：

| 文件 | 行 |
| --- | ---: |
| `debug/StructuredValueTree.vue` | 6 |
| `debug/NodeExecutionInspector.vue` | 8 |
| `inspector/InspectorPanel.vue` | 14 |
| `library/NodeLibraryPanel.vue` | 8 |
| `runs/RunWorkpiecePanel.vue` | 8 |
| `runs/RunVariablePanel.vue` | 7 |
| `workspace/DockablePanelGroup.vue` | 27 |
| `workspace/FlowDebugPanel.vue` | 9 |
| `workspace/OutputPanel.vue` | 17 |
| `workspace/WorkspacePanelSwitcher.vue` | 13 |

C# 表达式树可以自然保留表达式形式，无需为嵌套调用或直接返回登记例外。既有 `eng/code-conventions/exceptions.json` 保留精确表达式与文件的例外机制，当前清单为空。不要把提供程序需要翻译的表达式改写为块体 Lambda，或将依赖查询项的计算移到查询外。

检查器分别使用 TypeScript、Vue 编译器、PostCSS 和随 .NET SDK 提供的 Roslyn 解析源码。测试中的违规示例存放在字符串内，不会被当作待检查代码。
