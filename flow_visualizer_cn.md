# 📘 自述文档：基于 .NET 8 的流程可视化框架

## 🧩 项目介绍

本项目是一个基于 **.NET 8** 的流程可视化框架，

- 全部运行依赖、运行环境、编辑器均为 **开源**，
- 支持 **二次开发**，适合用于嵌入式、工控、数据流程自动化等场景。

📺 **Bilibili 视频更新**：不定期更新项目相关内容，可前往我的 [Bilibili 主页](https://space.bilibili.com/33526379) 查看。

---

## 🗓️ 计划任务（更新于 2025 年 7 月 28 日）

- ✅ 正在完善“**流程图转 C# 代码**”功能
- 🔥 构思 **热更新机制**
- 🔧 多客户端 **远程控制逻辑**设计中

---

## 🖌️ 如何绘制流程？

### 🛠️ 准备工作

1. 编译 `Serein.Workbench` 项目，确保工作台可运行。
2. 新建一个类库项目，并添加对 `Serein.Library` 的引用（也可通过 Negut 获取）。

### ✨ 开始绘图

1. 在类库项目中添加一个类，使用 `[DynamicFlow]` 特性标记类。
2. 使用 `[NodeAction]` 特性标记方法。
3. 编译项目并将生成的 DLL 拖入工作台左侧“类库”面板中。
4. 工作台自动解析并加载所有节点方法。

### 🎨 绘制节点流程

1. 在工作台中新建一个画布。
2. 从左侧类库面板中拖拽方法到画布上生成节点。
3. 在节点连接器之间拖拽生成连线，表示数据/控制流。
4. 可重复添加、连接多个节点构建完整流程。

### ▶️ 运行流程

- **从起始节点运行**：
  - 设置某节点为“起始节点” → 点击“运行” → 选择“从当前画布运行”
- **从指定节点运行**：
  - 框选单个节点 → 按 `F5` 运行
- **运行所有画布**：
  - 在“运行”菜单中选择“运行所有画布”

### 💾 保存/加载项目

- **保存项目**：`视图` → `保存项目` → 选择保存路径
- **加载项目**：`视图` → `加载本地项目` → 选择项目文件

### 🪵 查看输出日志

- 在 `视图` → `输出窗口` 中查看异常、日志与调试信息

---

## 🧱 如何让方法成为流程节点？

- 使用 `[NodeAction]` 特性标记方法
- 指定合适的节点类型（如 Action、Flipflop、Script 等）

---

## 📚 关键接口说明

### IFlowContext（流程上下文）

> 节点之间数据传递的核心接口，由运行环境自动注入。

#### 🔑 核心特点

- 全局单例环境接口，支持注册/获取实例（IoC 模式）
- 每个流程实例中上下文隔离，防止脏数据产生

#### 🔍 常用属性

| 属性                | 类型 | 描述                     |
| ----------------- | -- | ---------------------- |
| `RunState`        | 枚举 | 表示流程运行状态（初始化、运行中、运行完成） |
| `NextOrientation` | 枚举 | 指定下一个分支（成功、失败、异常）      |
| `Exit()`          | 方法 | 提前终止当前流程               |

---

## 🧾 DynamicNodeType 枚举说明

### 1️⃣ 控件不生成节点（生命周期方法）

| 枚举值       | 描述                            |
| --------- | ----------------------------- |
| `Init`    | 程序启动时最先执行，适合进行类初始化等操作         |
| `Loading` | 所有 DLL 的 Init 执行完毕后调用，适合业务初始化 |
| `Exit`    | 程序关闭时调用，适合释放线程/关闭服务           |

> 参数均为 `IFlowContext`，返回值支持异步，但框架不处理其结果。

### 2️⃣ 基础节点类型

#### 📎 FlowCall（流程接口节点）

- 实现跨画布复用逻辑
- 支持参数共享模式（全局同步）或参数独立模式（本地隔离）

#### 💡 Script（脚本节点）

- 使用 AST 抽象语法树动态编译运行
- 内置 `global()`、`now()` 等函数，参考 `Serein.Library.ScriptBaseFunc` 类中成员方法。
- 在代码中调用 `SereinScript.AddStaticFunction("your function name", MethodInfo);` 注册更多的内置方法。

示例：

```csharp
// 入参是一个对象，入参名称是 "info" ，包含Var、Data属性。
// 自定义类
class Info{
  string PlcName;
  string Content;
  string LogType;
  DateTime LogTime;
}
plc = global("JC-PLC"); // 获取全局数据节点中的数据
log = new Info() {
  Content = plc + " " + info.Var + " - 状态 Value  : " + info.Data,
  PlcName = plc.Name,
  LogType = "info",
  LogTime = now(),
};
return log;
```

#### 🌐 GlobalData（全局数据节点）

- 获取/共享运行时跨节点数据
- 表达式使用：`global("DataKey")`

### 3️⃣ 从 DLL 生成控件的节点

#### ⚙️ Action

- 最基础的节点类型，入参自动匹配，返回值支持异步

#### 🔁 Flipflop（触发器）

- 可响应外部事件或超时触发，需要有返回值

#### 🧠 ExpOp（表达式节点）

- 提取对象的子属性、字段、数组、字典值
- 示例：`@Get .Array[22]` 或 `@Get .Dict["key"]`

#### 🔍 ExpCondition（条件表达式节点）

- 条件判断节点，支持布尔/数值/文本逻辑判断

示例：

```text
条件表达式：.Age > 18 && data.Age < 35
注意，右侧出现的“data”是表达式数据的默认名称，假如你需要在表达式里其他地方使用入参数据时，就可以使用“data”。

或 .Text.StartsWith("1100")  // 判断文本是否以1100开头
```

#### 🖼️ UI（嵌入控件）

- 显示自定义 `UserControl` 到工作台
- 返回实现 `IEmbeddedContent` 接口的对象

示例代码片段：

```csharp
await context.Env.UIContextOperation.InvokeAsync(() => {
    var userControl = new UserControl();
    adapter = new WpfUserControlAdapter(userControl, userControl);
});
return adapter;
public class WpfUserControlAdapter : IEmbeddedContent
{
   private readonly UserControl userControl;
   private readonly IFlowControl flowControl;
   public WpfUserControlAdapter(UserControl userControl, IFlowControl flowControl)
   {
    this.userControl = userControl;
    this.flowControl= flowControl;
   }
   public IFlowControl GetFlowControl()
   {
    return flowControl;
   }
   public object GetUserControl()
   {
    return userControl;
   }
}
```

---

## ✅ 总结

- 该框架提供灵活的流程编辑、运行与二次扩展能力
- 支持脚本、全局数据、子流程等高级功能
- 适用于 AGV 调度、PLC 自动化、图形化逻辑控制等场景

---

> 本文档适用于开发者快速上手和参考具体接口用法。如需进一步了解，请关注项目演示视频或参与社区讨论。

