# 在流程中使用 SereinScript

脚本节点适合计算、条件处理和数据整理。下面从输入数字 `41`、返回 `42` 开始，再介绍日志、运行变量和常用语法。

## 在控制台编辑并运行

1. 在项目画布的 **节点库** 中拖入“脚本”节点。独立运行这个节点时，在右侧 **节点检查器** 勾选 **流程入口**。
2. 在检查器的 **脚本输入** 区域点击添加按钮。把输入名称改为 `amount`，值类型填 `System.Int32`，勾选 **必需**，数据来源选 **字面量**，值填 `41`。
3. 点击脚本区域的 **打开沉浸式脚本编辑器**，用下面的代码替换默认的 `return 0`。
4. 点击 **应用代码**（或 `Ctrl+Enter`，macOS 使用 `Cmd+Enter`），再点击画布顶部的 **保存**。应用代码只更新当前编辑内容，项目保存是另一项操作。
5. 点击 **运行流程**，在 **运行输出** 查看日志，在运行记录中查看节点输出；结果应为 `42`。

```sereinlang
import { log } from "sereinflow"
let result = amount + 1
log.info(result)
return result
```

**取消** 或 `Esc` 会关闭编辑器并丢弃尚未应用的修改。编辑器提供代码高亮，语法是否正确仍应以编译诊断和实际运行结果为准。

## 输入怎样进入脚本

检查器中的输入**名称**就是脚本中可直接读取的变量名，无需再次声明 `amount`。名称应唯一，使用字母或下划线开头，后续可用字母、数字、下划线；避免使用 `return`、`let` 等关键字或与导入成员重名。

输入可以来自字面量、项目输入、表达式或前一节点。要传递上游结果，连接上游输出到脚本输入，并用流程连接安排执行顺序。数据连接提供值，流程连接决定何时执行。

**必需**表示执行请求必须提供这个输入，并不表示值一定非 null。未提供的可选输入按 `null` 注入，可自行给出默认值：

```sereinlang
let actual = if amount == null then 0 else amount
return actual + 1
```

名称与连接器 ID 的区别、类型转换规则见[ScriptAdapter 的输入输出契约](/script/script-adapter#输入输出契约)。

## 返回结果与输出连接器

`return` 返回当前脚本的结果。正常返回 `false`、`null` 或 `{ success = false }` 仍走节点的 **Success** 分支；返回值不会自动选择 **Failure** 分支。未处理的编译、运行异常走 **Error** 分支。

| 输出契约 | 返回值如何映射 |
| --- | --- |
| 没有输出 | 返回值不形成节点输出。 |
| 一个输出 | 整个返回值交给该连接器，包括对象或数组。 |
| 多个输出 | 必须返回对象，按输出契约的 `name` 取对应属性；缺失属性映射为 null。 |

例如已声明两个输出 `total`、`accepted` 时：

```sereinlang
return { total = amount + 1, accepted = amount >= 0 }
```

默认节点使用单一输出。当前检查器支持增删脚本输入，多个输出契约需通过流程定义/API 或二次开发配置；仅在代码中增加对象属性不会增加画布连接器。

## 常用语法

| 写法 | 含义 |
| --- | --- |
| `let x = 1` | 声明不可重新赋值的变量。 |
| `var x = 1`、`x = x + 1` | 声明并更新可变变量。 |
| `42`、`42L`、`3.14f`、`3.14`、`10.5m` | int、long、float、double、decimal 数值。 |
| `"text"`、`true`、`false`、`null` | 字符串、布尔值、空值。 |
| `[1, 2, 3]`、`items[0]` | 数组及从零开始的索引。 |
| `{ name = "Ada", count = 3 }`、`item.name` | 对象及属性访问；对象属性使用 `=`。 |
| `+ - * / %`、`== != < <= > >=` | 算术与比较。 |
| `&&`、`\|\|`、`!` | 逻辑运算。 |
| `if test { ... } else { ... }` | 条件分支。 |
| `if test then a else b` | 返回一个值的条件表达式。 |
| `for item in items { ... }` | 遍历集合。 |
| `let add = (a, b) => a + b` | 定义函数，使用 `add(1, 2)` 调用。 |
| `import { log } from "sereinflow"` | 从模块导入成员。 |

例如累计数组中的值：

```sereinlang
let values = [1, 2, 3]
var total = 0
for value in values {
    total = total + value
}
if total > 0 {
    return total
} else {
    return 0
}
```

## 日志与当前运行信息

```sereinlang
import { log, env } from "sereinflow"
log.info(env.nodeId)
if env.isCancellationRequested {
    return null
}
return env.projectId
```

`log.info(value)`、`log.error(value)` 写入本次运行的结构化日志，返回 null。`log.error` 的日志级别为 error，调用它本身不会让节点失败。消息最多保留 16 Ki 个字符，超出后追加省略号。

`env` 提供以下只读属性：

| 属性 | 含义 |
| --- | --- |
| `runId`、`projectId`、`flowId` | 当前运行、项目、流程的 ID 字符串。 |
| `canvasId`、`nodeId` | 当前画布与节点的 ID 字符串。 |
| `frameDepth` | 当前调用帧深度，整数。 |
| `isCancellationRequested` | 当前调用是否已收到取消请求，布尔值。 |

## 共享本次运行的变量

```sereinlang
import { variables, log } from "sereinflow"
variables.set("batch.count", 42)
variables.set("batch.note", null)
if variables.contain("batch.count") {
    log.info(variables.get("batch.count"))
}
return variables.get("batch.count")
```

`set(name, value)` 新增或覆盖变量；`get(name)` 在名称不存在或值为 null 时返回 null；`contain(name)` 判断名称是否存在，已存入 null 也返回 true。名称区分大小写，不能是空字符串或纯空白。

同一次运行的脚本、C# 类库节点、监听触发器与 FlowCall 调用帧共用变量表，不同运行隔离。数组和对象转换时会复制，修改后需再调用 `set` 保存；CLR 对象句柄保留原引用。函数、方法值和循环引用不能存储；最多嵌套 32 层，每个容器最多 10,000 项。单次读写受锁保护，先 `get` 再 `set` 并非原子更新。

在 **流程变量** 面板查看实时值，运行结束后查看最终快照。与 C# 节点共用变量的方法见[内置服务](/library/built-in-services#共用流程变量)，接口见[运行 API](/api/runs#流程变量表)。

## 使用语言内置模块

语言引擎还提供 `system` 模块，导出 `file`、`network`、`console`、`path`、`json`、`timer`、`crypto`、`process`。例如解析 JSON：

```sereinlang
import { json } from "system"
let payload = json.parse("{\"count\":41}")
return payload.count + 1
```

文件路径与外部调用发生在 Worker 所在的环境中。运行日志使用 `sereinflow.log`；`system.console` 面向进程控制台。具体系统模块方法可在仓库 `src/ThirdParty/SereinScript/ScriptLang/System/` 查看。

## 常见问题

| 现象或错误码 | 检查方法 |
| --- | --- |
| `script.input_missing` | 必需输入是否配置了数据来源，执行请求是否提供对应连接器的值。 |
| `script.input_unknown` | 执行请求是否仍在使用已删除或改动的输入 ID。 |
| `script.compile_failed` | 检查未声明变量、对象属性的 `=`、括号及诊断中的行列位置。 |
| `script.output_shape_invalid` | 多输出契约必须返回对象。 |
| `script.value_conversion_failed` | 下游 .NET 参数类型是否能接收结果；字符串 `"42"` 不会自动转成整数。 |
| `script.value_unsupported` | 检查不支持的值转换；自定义宿主调用 `variables` 时需提供运行级变量表。 |
| `script.cancelled` | 本次脚本调用已取消，查看运行的停止状态。 |

开发者可用 `ISereinLangCompiler.CompileAsync` 获取结构化编译诊断，它只编译、不执行脚本；当前诊断入口接受语言版本 `0.1`。限制与示例见[诊断编译](/script/script-adapter#诊断编译)。
