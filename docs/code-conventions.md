# 自有代码规范

适用于仓库内的自有 Vue、TypeScript、C# 和 CSS，包括测试、示例、文档站及检查工具。第三方源码、生成文件、依赖与构建产物不纳入调整。

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

- 调用参数中的函数或构造调用，先存入有意义的临时变量。回调自身不算参数嵌套，但回调内部仍受检查。
- C# 和 TypeScript 允许链式调用，不要求仅为拆开调用链而引入中间变量。LINQ、普通流式 API、集合创建及 JSON 枚举后接查询、`(await ReadAsync()).Where(...)`、可空条件调用链均允许。调用链中各次调用的参数和回调内部仍按原规则检查；返回结果遵守下一条规则。优先按完整的查询或操作表达业务含义，复杂时再按语义拆分。
- 块体内函数、构造和 await 调用的返回结果先赋值再 return；Lambda 和 TypeScript 箭头函数也遵守此规则。C# 方法、局部函数及属性等成员的 `=>` 表达式体允许直接返回调用结果，包括 `await`；调用参数中的嵌套调用仍受检查。
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

C# 必须以表达式形式交给框架处理的表达式树登记在 `eng/code-conventions/exceptions.json`，包含文件、精确表达式和原因。例外只跳过对应表达式，不豁免整个文件或普通 LINQ to Objects。

应优先把表达式树中可提前计算的值提取到查询外。本次重构后的 C# 例外清单为空；现有查询保留表达式树，只把 GUID、枚举等值的转换移到明确命名的变量中。

检查器分别使用 TypeScript、Vue 编译器、PostCSS 和随 .NET SDK 提供的 Roslyn 解析源码。测试中的违规示例存放在字符串内，不会被当作待检查代码。
