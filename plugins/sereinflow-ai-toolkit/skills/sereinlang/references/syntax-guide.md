# SereinLang 语法指南

本指南根据 SereinScript 的 Lexer、Parser、AST 和项目语言参考整理，面向
SereinFlow 脚本节点编写。SereinLang 采用表达式驱动模型：声明、控制流、
代码块和函数调用都可以产生值。实际编译时，以
`sereinflow_compile_sereinlang` 返回的诊断和当前运行时版本为最终依据。

## 目录

- [词法](#词法)
- [值和字面量](#值和字面量)
- [变量和赋值](#变量和赋值)
- [运算符](#运算符)
- [函数和闭包](#函数和闭包)
- [控制流](#控制流)
- [数组和对象](#数组和对象)
- [模块导入](#模块导入)
- [内置函数和成员](#内置函数和成员)
- [CLR 互操作](#clr-互操作)
- [SereinFlow 编写约束](#sereinflow-编写约束)

## 词法

- 源文件使用 UTF-8；标识符和关键字区分大小写。
- 空格、制表符和回车会被忽略；换行用于行号计数，不是必须的语句终止
  符。优先使用换行分隔顶层表达式，不要依赖分号。
- 只使用单行注释：`//` 到行尾。不支持跨行的 `/* ... */` 注释。
- 标识符以字母、`_` 或 `@` 开头，后续可以包含字母、数字和 `_`。
  `@` 用于避开与 C# 关键字冲突的名称，例如 `@class`。
- 保留关键字包括 `let`、`var`、`if`、`then`、`else`、`when`、`for`、
  `in`、`return`、`import`、`from`、`true`、`false` 和 `null`。

常用标点和操作符如下：

```text
( )    参数、分组和调用
[ ]    数组、索引
{ }    代码块、对象和 when 子句
. ?.   成员访问和安全成员访问
, :    元素分隔、导入别名
=> ->  Lambda
=      赋值或对象属性定义
+ - * / %
== != < <= > >=
! && ||
```

## 值和字面量

### 数字

```js
42          // int，必要时扩展为 long 或 double
42L         // long
3.14f       // float
3.14d       // double
10.5m       // decimal
0xFF        // 十六进制
0b1010      // 二进制
```

无后缀整数优先解析为 `int`，超出范围时使用 `long`，再根据数值范围回退
到 `double`。小数默认是 `double`。后缀不区分大小写，可使用 `L`、`F`、
`D`、`M`。

### 字符串、布尔值和空值

```js
"Hello World"
"line1\nline2"
"quote: \"text\""
true
false
null
```

字符串使用双引号，不能跨行。常用转义包括 `\n`、`\r`、`\t`、`\"` 和
`\\`；未知转义会保留为反斜杠和字符。

### 数组和对象

```js
let numbers = [1, 2, 3]
let person = {
    name = "Alice",
    age = 30,
    active = true
}
```

数组可以嵌套。对象键可以是标识符或字符串；对象属性使用 `=`，不是
JavaScript 风格的 `:`。当属性值与变量同名时可以省略赋值部分：

```js
let name = "Bob"
let age = 25
let user = { name, age }
```

日期时间和时间跨度没有专用字面量，通过内置函数创建：

```js
let nowValue = now()
let day = date("2025-06-15 12:00:00")
let duration = timespan(3, "days")
```

## 变量和赋值

`let` 创建不可重新绑定的名称，`var` 创建可以重新赋值的名称：

```js
let limit = 10
var count = 0
count = count + 1
```

`let` 绑定的对象或数组内部仍可修改。赋值目标可以是变量、索引或成员：

```js
items[0] = "updated"
person.name = "Carol"
person?.address?.city = "Hangzhou"
```

名称在声明所在的代码块内可见。Lambda 可以捕获外部名称；同一作用域不能
重复声明同名 `let` 或 `var`。

## 运算符

从低到高的优先级如下。复杂表达式优先使用括号表达意图：

| 优先级 | 操作符 | 结合性 |
| --- | --- | --- |
| 1 | `=` | 右结合 |
| 2 | `||` | 左结合 |
| 3 | `&&` | 左结合 |
| 4 | `==`、`!=` | 左结合 |
| 5 | `<`、`<=`、`>`、`>=` | 左结合 |
| 6 | `+`、`-` | 左结合 |
| 7 | `*`、`/`、`%` | 左结合 |
| 8 | `!`、一元 `-` | 右结合 |
| 9 | 调用、索引、成员访问 | 左结合 |

```js
10 + 5
10 / 3
!enabled
left && right
person?.address?.city
```

加法支持数字、字符串、数组以及日期时间相关运算。字符串乘整数表示重复，
数组加数组表示拼接：

```js
"ab" * 3                 // "ababab"
[1, 2] + [3, 4]           // [1, 2, 3, 4]
date("2025-06-15") + timespan(3, "days")
date("2025-06-20") - date("2025-06-15")
```

最后一个表达式的结果为 `TimeSpanValue`。日期时间支持与时间跨度的加减和
比较；时间跨度提供 `days`、`hours`、`totalDays` 等成员。

## 函数和闭包

Lambda 支持无参数、单参数和多参数形式，函数体可以是表达式或代码块：

```js
let add = (a, b) => a + b
let square = x => x * x
let getPi = () => 3.14159

let factorial = (n) => {
    if n <= 1 then 1 else n * factorial(n - 1)
}
```

`=>` 和 `->` 都可作为 Lambda 箭头。Lambda 是一等值，可以作为参数或返回
值，并且捕获创建位置的变量：

```js
let makeAdder = (x) => (y) => x + y
let addTen = makeAdder(10)
addTen(5)
```

对象属性也可以保存 Lambda，从而形成对象方法：

```js
let counter = { value = 0 }
let api = {
    increment = () => counter.value = counter.value + 1,
    current = () => counter.value
}
api.increment()
api.current()
```

## 控制流

### If

`if` 是表达式，`then` 可写可省略，`else` 可省略；省略时结果为 `null`：

```js
let status = if score >= 60 then "pass" else "fail"

if value > 0 {
    print("positive")
    value
} else {
    0
}
```

### When

`when` 根据值匹配多个子句，子句用逗号分隔，`_` 可作为默认分支：

```js
let label = when code {
    200 => "ok",
    404 => "missing",
    _ => "other"
}
```

子句主体可以是代码块。模式也可以使用表达式或 Lambda；使用编译器验证
复杂模式的实际匹配行为。

### For

使用 `for name in iterable` 遍历数组、`range` 结果或对象键：

```js
var total = 0
for value in range(1, 4) {
    total = total + value
}
total
```

`for` 是表达式，通常返回最后一次循环主体的值；需要明确节点输出时，应在
循环之后显式返回目标值。

### Return 和代码块

`return expression` 提前返回一个值，单独的 `return` 返回 `null`。代码块
的值是最后一个表达式：

```js
{
    let a = 1
    let b = 2
    a + b
}
```

## 数组和对象

数组支持索引、长度、追加和拼接：

```js
let values = [1, 2, 3]
values[0]
values.length
values.add(4)
values.last()
values.join(",")
```

对象使用字符串键和值：

```js
let result = {
    ok = true,
    message = "done"
}
result.ok
result["message"]
result.has("ok")
keys(result)
```

使用 `?.` 进行安全访问；中间值为空时返回 `null`，避免直接访问引发运行时
错误。

## 模块导入

从脚本文件导入指定成员：

```js
import { createStore } from "pinia.script"
import { store : appStore, view } from "./state.script"
```

导入路径相对于当前脚本解析。被导入脚本的最终值应是对象，导入列表从中
解构成员；别名使用冒号。保持模块依赖路径明确，避免循环导入。

## 内置函数和成员

### 基础函数

| 函数 | 用途 |
| --- | --- |
| `print(a, b, ...)` | 输出多个值 |
| `typeof(value)` | 返回 `int`、`string`、`array` 等类型名 |
| `bool(value)` | 转换为布尔值 |
| `double(value)` | 转换为 double |
| `range(start, end)` | 生成 `[start, end)` 整数序列 |
| `keys(object)` | 返回对象键集合 |
| `len(collection)` | 返回集合长度 |

### 日期和时间

| 函数或成员 | 用途 |
| --- | --- |
| `now()` | 当前本地时间 |
| `date(text)` | 解析日期时间 |
| `timespan(number, unit)` | 创建时间跨度 |
| `DateTimeValue.year` 等 | 年、月、日、时、分、秒等部分 |
| `DateTimeValue.ticks` | 100 纳秒单位的 tick |
| `TimeSpanValue.totalDays` 等 | 总天数、小时、分钟、秒和毫秒 |
| `.toString(format)` | 以指定格式输出 |

时间跨度单位包括 `days`、`hours`、`minutes`、`seconds`、`milliseconds` 和
`ticks`。

### 常见成员

字符串常用 `.split()`、`.toUpper()`、`.toString()`；数组常用 `.add()`、
`.count`、`.last()`、`.join()`、`.toList()`；对象常用 `.has(key)` 和
`.keys`。具体成员仍由运行时实现决定，先用编译器验证。

## CLR 互操作

宿主可以将 CLR 对象暴露给脚本，脚本通过属性和方法访问：

```js
let person = new_Person()
person.Name = "Zhang San"
person.Age = 18
let name = person.Name
let nextAge = person.AddYears(1)
```

`new_Person` 只是宿主提供的示例工厂，不是语言内置函数。不要猜测可用的
CLR 类型、构造函数或程序集；只有当前 SereinFlow 节点输入契约和宿主模块
明确提供时才使用。

## SereinFlow 编写约束

1. 先读取目标脚本节点的输入、输出和语言版本契约，使用契约中的精确参数
   名称。不要将自然语言字段名自行改成其他变量名后忘记映射。
2. 以最终表达式或显式 `return` 产生节点输出。多分支必须保证每条需要执行
   的路径都有符合输出契约的值。
3. 通过 MCP 编译 Tool 迭代诊断。编译成功只表示语法和配置可编译，不表示
   脚本已经执行或流程已经修改。
4. 脚本源码变更必须通过 `replace_script_source` 的流程补丁预览、用户确认
   和应用流程；不要把编译 Tool 当成写入授权。
5. 不要在脚本中引入未经契约声明的文件、网络、环境变量或任意程序集访问。
   不要把敏感字面量放入诊断、日志或普通回答。
