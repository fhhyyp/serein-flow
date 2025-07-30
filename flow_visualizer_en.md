# 📘 Documentation: Flow Visualization Framework Based on .NET 8

## 🧩 Project Overview

This project is a **flow visualization framework** based on **.NET 8**.

- All runtime dependencies, environment, and editor are **open-source**
- Supports **secondary development**, suitable for scenarios such as embedded systems, industrial control, and data flow automation

📺 **Bilibili Video Updates**: Project-related videos are periodically updated. Visit [my Bilibili profile](https://space.bilibili.com/33526379) for more.

---

## 🗓️ Planned Tasks (Updated July 28, 2025)

- ✅ Improving **"flowchart to C# code"** feature
- 🔥 Designing **hot update mechanism**
- 🔧 Designing **remote control logic** for multiple clients

---

## 🖌️ How to Draw a Flow?

### 🛠️ Preparation

1. Compile the `Serein.Workbench` project to ensure the workbench is operational
2. Create a class library project and reference `Serein.Library` (can also be acquired via Negut)

### ✨ Start Drawing

1. In the class library, add a class and mark it with the `[DynamicFlow]` attribute
2. Mark methods with the `[NodeAction]` attribute
3. Compile the project and drag the generated DLL into the left "Library" panel of the workbench
4. The workbench will automatically parse and load all node methods

### 🎨 Draw Node Flows

1. Create a new canvas in the workbench
2. Drag methods from the left library panel onto the canvas to generate nodes
3. Drag between node connectors to create links, representing data/control flow
4. Repeat to build a complete flow

### ▶️ Run the Flow

- **Run from start node**:
  - Set a node as "start node" → Click "Run" → Select "Run from current canvas"
- **Run from a specific node**:
  - Select a single node → Press `F5`
- **Run all canvases**:
  - In the "Run" menu, choose "Run all canvases"

### 💾 Save/Load Projects

- **Save Project**: `View` → `Save Project` → Choose save path
- **Load Project**: `View` → `Load Local Project` → Choose project file

### 🩵 View Output Logs

- View logs, exceptions, and debug info via `View` → `Output Window`

---

## 🧱 How to Make a Method a Flow Node?

- Mark methods with `[NodeAction]` attribute
- Specify the appropriate node type (e.g., Action, Flipflop, Script, etc.)

---

## 📚 Key Interface Overview

### `IFlowContext` (Flow Context)

> Core interface for data transfer between nodes, automatically injected by the runtime environment.

#### 🔑 Key Features

- Global singleton environment interface, supports registration/retrieval (IoC pattern)
- Context isolation per flow instance, preventing dirty data

#### 🔍 Common Properties

| Property          | Type   | Description                                         |
| ----------------- | ------ | --------------------------------------------------- |
| `RunState`        | Enum   | Indicates flow run state (Init, Running, Completed) |
| `NextOrientation` | Enum   | Specifies next branch (Success, Failure, Exception) |
| `Exit()`          | Method | Terminates the current flow early                   |

---

## 🧾 `DynamicNodeType` Enum Explained

### 1⃣ Nodes not Generated (Lifecycle Methods)

| Enum Value | Description                                              |
| ---------- | -------------------------------------------------------- |
| `Init`     | Executes first at program start, suitable for init logic |
| `Loading`  | Called after all DLL `Init`s run, good for business init |
| `Exit`     | Invoked on shutdown, suitable for resource release       |

> Parameters are all `IFlowContext`. Return values can be async but are not handled by the framework.

### 2⃣ Basic Node Types

#### 📌 `FlowCall` (Flow Interface Node)

- Enables logic reuse across canvases
- Supports shared (global sync) or isolated (local) parameter modes

#### 💡 `Script` (Script Node)

- Executes dynamically via AST (Abstract Syntax Tree)
- Built-in functions like `global()`, `now()` (see `Serein.Library.ScriptBaseFunc`)
- Register more functions via:

```csharp
SereinScript.AddStaticFunction("your function name", MethodInfo);
```

**Example:**

```csharp
// Input is an object named "info" with Var, Data properties
class Info {
  string PlcName;
  string Content;
  string LogType;
  DateTime LogTime;
}
plc = global("JC-PLC");
log = new Info() {
  Content = plc + " " + info.Var + " - Status Value: " + info.Data,
  PlcName = plc.Name,
  LogType = "info",
  LogTime = now(),
};
return log;
```

#### 🌐 `GlobalData` (Global Data Node)

- Get/share runtime data across nodes
- Expression usage: `global("DataKey")`

### 3⃣ Nodes Generated from DLL Controls

#### ⚙️ `Action`

- Basic node type, input parameters auto-matched, supports async return

#### 🔁 `Flipflop` (Trigger Node)

- Can be triggered by external events or timeouts
- Supports state returns (Success, Failure, Cancelled) and trigger source type

#### 🧠 `ExpOp` (Expression Node)

- Extracts sub-properties/fields/array/dictionary values
- Examples:
  - `@Get .Array[22]`
  - `@Get .Dict["key"]`

#### 🔍 `ExpCondition` (Conditional Expression Node)

- Conditional logic based on bool/number/string

**Example:**

```text
.Age > 18 && data.Age < 35
or
.Text.StartsWith("1100")
```

> `data` refers to the default name of the input object in expressions.

#### 🖼️ `UI` (Embedded Control)

- Display custom `UserControl` on the workbench
- Must return object implementing `IEmbeddedContent`

**Sample Code:**

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

## ✅ Summary

- This framework provides flexible flow editing, execution, and extension capabilities
- Supports advanced features such as scripting, global data, and sub-flows
- Suitable for AGV scheduling, PLC automation, visual logic control, and more

---

> This documentation helps developers get started quickly and understand interface usage. For more information, refer to video demos or join the community discussion.

