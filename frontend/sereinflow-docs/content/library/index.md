# 编写节点类库

画布上的脚本节点适合快速试验。如果一段功能需要重复使用，或者要调用设备、使用已有 .NET 代码，就可以把它写成**节点类库**。它是一个普通 C# 类库：一个公开的方法对应画布上的一个节点。

```text
写 C# 类库 → 编译并打成 ZIP → 上传到 SereinFlow → 加入项目 → 在画布中使用
```

## 从哪里开始

1. [写出第一个节点](/library/first-node)：从一个计算方法开始，了解编译、上传和引用。
2. [节点参数与执行方式](/library/node-contracts)：决定画布上显示什么、节点何时继续执行。
3. [注册自己的服务](/library/services)：多个节点共用设备连接或业务对象时，了解构造函数注入。
4. [使用内置服务](/library/built-in-services)：接收消息、保存文件，以及获取本次运行的信息。
5. [加载 native DLL](/library/native-dll)：使用依赖操作系统的 `.dll`、`.so`、`.dylib`。
6. [打包、上传与排错](/library/package-upload)：检查 ZIP 的目录、项目引用和常见报错。

## 先记住三个位置

| 位置 | 放什么 |
| --- | --- |
| 你的节点类库 | `[FlowLibrary]`、`[FlowNode]`、`[FlowService]` 和随包上传的依赖。 |
| SereinFlow 宿主 | 保存项目、提供 HTTP 接口、启动 Worker。宿主的服务注册见[服务与接入](/integration/)。 |
| 正在运行的 Worker | 在一次流程运行中创建节点对象，提供消息、文件输出和 native DLL 加载服务。 |

这些位置各有用途。节点类库中的构造函数只能取得 Worker 提供的服务，以及**同一上传程序集**用 `[FlowService]` 注册的服务。宿主项目里注册的任意服务不会自动传入节点。
