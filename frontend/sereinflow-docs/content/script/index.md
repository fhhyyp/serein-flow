# SereinScript 使用与开发

SereinScript 是脚本节点使用的脚本引擎。你可以在画布上写一段代码，接收节点输入、整理数据、读写本次运行的变量，再把结果交给后续节点。源码和部分接口使用 **SereinLang** 作为语言名称，运行时项目名称为 **ScriptLang**；它们对应这里的同一套脚本能力。

## 按你的目标开始

| 你要做什么 | 阅读 |
| --- | --- |
| 在控制台编写、运行脚本，了解语法与输入输出 | [在流程中使用脚本](/script/usage) |
| 修改脚本如何编译、执行、缓存或与 .NET 节点交换值 | [扩展 ScriptAdapter](/script/script-adapter) |
| 给脚本增加可导入的 C# 方法、属性或模块 | [扩展 ScriptModules](/script/script-modules) |
| 把业务方法作为独立的画布节点提供 | [编写节点类库](/library/) |

## 三个项目各负责什么

```text
脚本节点的源码、输入与输出契约
  → SereinFlow.ScriptAdapter：编译、执行、输入输出映射、.ssc 缓存
    → ScriptLang：解析语法、生成字节码、运行虚拟机
    → SereinFlow.ScriptModules：注册 sereinflow 模块，接入运行日志、环境、变量
```

二次开发涉及 `src/SereinFlow.ScriptAdapter` 和 `src/SereinFlow.ScriptModules`。语言引擎与原型源生成器固定在 `src/ThirdParty/SereinScript`，正常构建无需额外下载 SereinScript 源码或设置路径环境变量。当前两个适配项目面向 .NET 10。

控制台自带脚本节点，写脚本无需上传类库。新增脚本模块则需要修改模块注册并重新构建相关宿主与 Worker；上传 `[FlowNode]` 类库不会自动增加脚本的 `import` 模块。

## 从一个结果开始

```sereinlang
import { log, env } from "sereinflow"
log.info("Script started")
return { nodeId = env.nodeId, total = 42 }
```

默认脚本节点只有一个输出，整个返回对象就是该输出的值。多个输出连接器需要配置输出契约，规则见[使用说明](/script/usage#返回结果与输出连接器)。

脚本在独立的 Worker 中执行。控制台、HTTP 和事件记录看到的是用于展示与保存的投影，CLR 对象句柄等值可能只显示类型摘要。自定义模块应遵循[运行上下文与值转换规则](/script/script-modules#运行上下文与值转换)。
