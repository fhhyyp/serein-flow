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

- 调用参数中的函数或构造调用，以及调用结果上的链式调用，均先存入有意义的临时变量。回调自身不算参数嵌套，但回调内部仍受检查。
- 函数、构造和 await 调用的返回结果先赋值再 return；箭头函数也遵守此规则。
- 每行只声明或初始化一个变量，多变量解构拆分处理。每行一条语句，`for` 头部的分隔符除外。
- `if`、`else if`、`else` 分行书写；分支体始终加大括号。
- 拆分时保留求值顺序、短路分支、异常时机、异步行为、查询延迟执行、资源生命周期及响应性。

## 常量与人工审查

业务阈值、协议值和重复的语义值使用 C# `const`/`static readonly` 或 TS 模块级 `const`。来源明确的默认值、配置、展示文本及类型字面量可保留。禁止通过临时变量简单转存字面量规避规则。

常量命名、来源及业务含义由人工审查；自动检查通过不代表这些语义事项已经验收。审查时也要确认调用拆分没有移动副作用、改变短路条件或扩大变量生命周期。

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
