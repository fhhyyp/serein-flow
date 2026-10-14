# ScriptModules：使用与扩展脚本模块

脚本模块为 SereinScript 提供可以直接调用的功能。例如 `log.info("Started")` 会写一条运行日志，`variables.set("order.code", "A001")` 会保存一个本次运行共享的变量。使用者在脚本中导入模块；二次开发者在 C# 中实现功能，并把它注册到脚本引擎。

| 你的目标 | 从哪里开始 |
| --- | --- |
| 在画布脚本里使用日志、运行信息和变量 | [用户：使用内置模块](#用户-使用内置模块) |
| 给脚本增加自己的方法和属性 | [开发教程：实现 example 模块](#开发教程-实现-example-模块) |
| 已经在本地验证，希望画布脚本也能调用 | [让流程节点使用新模块](#让流程节点使用新模块) |
| 方法需要写运行日志、访问调用信息或响应取消 | [运行上下文与值转换](#运行上下文与值转换) |

脚本编辑、输入输出和语言语法见[脚本使用指南](/script/usage)。本页围绕“调用一个模块”和“做出一个能调用的模块”展开。

## 用户：使用内置模块

### 在画布上跑通第一个例子

内置 `sereinflow` 模块已由流程执行器注册，无需安装包或上传 DLL。

1. 向画布拖入一个脚本节点，选中它并设为 **流程入口**。
2. 在右侧检查器打开脚本编辑器，粘贴下面的代码。这个例子不需要配置脚本输入。
3. 点击 **应用代码**，再点击画布顶部的 **保存** 和 **运行流程**。
4. 在运行日志中查看 `A001`，在节点输出中查看 `orderCode` 和 `nodeId`，在 **流程变量** 面板查看 `order.code`。

```sereinlang
import { log, env, variables } from "sereinflow"
variables.set("order.code", "A001")
let code = variables.get("order.code")
log.info(code)
return { orderCode = code, nodeId = env.nodeId }
```

节点输出是一个对象：`orderCode` 为 `"A001"`，`nodeId` 为当前脚本节点的 ID。默认脚本节点的单一输出接收整个对象，不会自动增加两个输出连接器。

### 看懂 import 和成员调用

以 `import { log } from "sereinflow"` 和 `log.info("A001")` 为例：

| 写法 | 这里指什么 |
| --- | --- |
| `"sereinflow"` | 模块名，由宿主注册。内置模块名不是 DLL 路径。 |
| `{ log }` | 要导入的成员名；这里只导入日志对象，也可以同时导入 `env`、`variables`。 |
| `log.info("A001")` | 调用日志对象的方法，括号里是脚本提供的参数。 |
| `env.nodeId` | 读取环境对象的属性；属性访问不加 `()`。 |

内置 `sereinflow` 模块目前提供以下能力：

| 成员 | 用法 | 结果与作用 |
| --- | --- | --- |
| `log` | `log.info(value)`、`log.error(value)` | 写 info/error 级别的运行日志，返回 null；写 error 日志本身不会让节点失败。 |
| `env` | `env.nodeId`、`env.canvasId`、`env.projectId`、`env.flowId`、`env.runId` | 读取当前调用的 ID 字符串。 |
| `env` | `env.frameDepth`、`env.isCancellationRequested` | 读取调用帧深度和当前取消状态。 |
| `variables` | `variables.set(name, value)` | 新增或覆盖变量，返回 null。 |
| `variables` | `variables.get(name)` | 读取变量；不存在或已存为 null 时返回 null。 |
| `variables` | `variables.contain(name)` | 检查名称是否存在，已存为 null 也返回 true。方法名是 `contain`。 |

变量名区分大小写，不能是空字符串或纯空白。同一次运行的脚本、C# 类库节点、触发器和 FlowCall 调用帧共享这张表；重新运行会使用另一张表。数组和对象在转换时复制，修改后需再调用 `set` 保存。更完整的规则见[共享运行变量](/script/usage#共享本次运行的变量)。

本页后面的 `example` 是开发教程创建的模块，标准安装并不自带。需要开发者完成注册和部署后，用户才能在画布上 `import { text } from "example"`。

## 开发前：模块、导出对象和原型的关系

假设要提供 `text.addTag("A001")`，返回 `"batch:A001"`。这涉及三个不同名称：

```text
import { text } from "example"
          │              └─ 模块名：注册到 ImportResolver
          └─ 导出名：模块导出表中的 "text"
text.addTag("A001")
     └─ 方法名：原型把 C# AddTag 暴露为 addTag
```

**模块**是一组可以导入的成员。**导出对象**是脚本拿到的值，本例为包装 C# 实例的 `ClrObjectValue`。**原型**定义这个值可以调用哪些方法、读取哪些属性。

因此需要完成两次注册：

| 注册操作 | 解决的问题 | 缺少时的表现 |
| --- | --- | --- |
| `engine.PrototypeManager.Register(instance)` | 引擎怎样处理该对象的 `addTag`、`tag` 等成员。 | 即使拿到了对象，也找不到自定义成员。 |
| `engine.ImportResolver.RegisterBuiltinModule("example", exports)` | `import` 从哪里取得 `text`。 | 运行时找不到模块，可能继续尝试解析同名文件。 |

`src/SereinFlow.ScriptModules` 负责这些脚本能力；`src/SereinFlow.ScriptAdapter` 负责流程节点的编译、执行和输入输出映射。希望在节点库中增加一个可拖拽的业务节点时，使用[节点类库](/library/)；希望现有脚本调用一个业务函数时，扩展脚本模块。上传 `[FlowNode]` 或 `[FlowService]` 类库不会自动注册脚本模块。

## 开发教程：实现 example 模块

这个例子先在本地控制台验证，完成后再接入 Worker。最终会支持：

```sereinlang
import { text } from "example"
return text.addTag("A001")
```

### 1. 创建验证项目并配置引用

在 **SereinFlow 仓库根目录** 执行，需要安装 .NET 10 SDK：

```powershell
dotnet new console --name ScriptModuleDemo --output examples/ScriptModuleDemo --framework net10.0
```

将 `examples/ScriptModuleDemo/ScriptModuleDemo.csproj` 替换为以下内容。路径以这个项目所在目录为基准；如果放到别处，需要同步调整。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/SereinFlow.ScriptModules/SereinFlow.ScriptModules.csproj" />
    <ProjectReference Include="../../src/ThirdParty/SereinScript/ScriptLang.Generator/ScriptLang.Generator.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

第一项引用提供 SereinFlow 模块、运行上下文及其依赖的 ScriptLang 类型；第二项让原型源生成器在这个验证项目中运行。`Analyzer` 引用用于编译时生成代码，不作为业务程序集调用。

如果直接在已有的 `src/SereinFlow.ScriptModules` 中开发，运行时和 Analyzer 引用已经具备，无需修改该项目文件。下面先把两个 C# 文件放入验证项目，便于单独运行。

### 2. 实现方法和属性

新增 `examples/ScriptModuleDemo/ExampleTextModule.cs`：

```csharp
using ScriptLang;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptModules
{
    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    internal sealed partial class ExampleTextModule
    {
        private readonly string _tag;
        public ExampleTextModule(string tag) => _tag = tag;

        public partial bool IsTarget(Value value)
            => value is ClrObjectValue clr && clr.Value is ExampleTextModule;

        [PrototypeFunction]
        public static StringValue AddTag(ExampleTextModule target, StringValue text)
            => StringValue.Create(target._tag + ":" + text.Value);

        [PrototypeProperty]
        public static StringValue Tag(ExampleTextModule target)
            => StringValue.Create(target._tag);
    }
}
```

把这段代码和脚本写法逐项对应起来：

| C# 写法 | 脚本中如何体现 |
| --- | --- |
| `[PrototypeExtension(...)]`、`partial` | 生成器为这个类补齐 `IPrototype` 等实现，使它可以注册到原型管理器。 |
| `IsTarget(...)` | 只让这个原型处理包装了 `ExampleTextModule` 的值。 |
| `PushThis = true` | 引擎把 `text` 背后的 C# 对象作为 `target` 传入；脚本无需传这个参数。 |
| `NamingFormat.Js` | `AddTag`、`Tag` 对应 `addTag`、`tag`。 |
| `[PrototypeFunction] AddTag(...)` | `text.addTag("A001")`；脚本只传一个字符串。 |
| `[PrototypeProperty] Tag(...)` | `text.tag`；即使 C# 用方法实现，脚本也按属性读取。 |
| `StringValue text`、`text.Value` | 脚本字符串包装及其内部的 C# `string`。 |
| `StringValue.Create(...)` | 把 C# 字符串包装成脚本可接收的返回值。 |

当前源生成器通过块作用域命名空间定位类，保持示例中的 `namespace ... { }` 写法。原型类的 `partial` 不能省略；`IsTarget` 的定义声明由生成器产生，示例提供实现部分。生成代码会参与本次构建，不需要手写接口实现。

### 3. 注册原型并导出对象

新增 `examples/ScriptModuleDemo/ExampleModuleRegistration.cs`：

```csharp
using ScriptLang;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptModules
{
    public static class ExampleModuleRegistration
    {
        public static void Register(ScriptEngine engine)
        {
            var text = new ExampleTextModule("batch");
            engine.PrototypeManager.Register(text);
            engine.ImportResolver.RegisterBuiltinModule(
                "example",
                new ObjectValue(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    ["text"] = new ClrObjectValue(text)
                }));
        }
    }
}
```

`"batch"` 是本次实例使用的标签；`"example"` 是模块名；字典键 `"text"` 是导出名。这里把同一个 `text` 实例用于原型注册和模块导出。

原型标记定义了这里的脚本方法和属性名称，以及参数检查与调用方式。当前引擎对 `ClrObjectValue` 还有 CLR 反射回退，公开的 C# 属性和方法可能按原名被访问，因此这些标记不是成员访问白名单。用专门的包装对象承载脚本功能，保持公开成员的用途明确。

### 4. 创建引擎并实际调用

将 `examples/ScriptModuleDemo/Program.cs` 替换为以下完整程序：

```csharp
using SereinFlow.ScriptModules;
using ScriptLang;
using ScriptLang.Runtime;

var engine = new ScriptEngine();
ExampleModuleRegistration.Register(engine);
var chunk = engine.CompileSource("""
    import { text } from "example"
    return text.addTag("A001")
    """, "example.serein");
var result = await engine.CreateTask(chunk).RunAsync(CancellationToken.None);
Console.WriteLine(((StringValue)result).Value);
```

在仓库根目录执行：

```powershell
dotnet run --project examples/ScriptModuleDemo/ScriptModuleDemo.csproj
```

程序的结果行应为 `batch:A001`。把脚本最后一行改成 `return text.tag`，应得到 `batch`。这一步验证了“生成原型 → 注册模块 → 导入成员 → 调用 C# → 返回脚本值”的完整路径。

这个控制台示例只给它自己的引擎注册了模块，尚未让运行中的 SereinFlow Worker 认识 `example`。

## 让流程节点使用新模块

### 修改注册入口并重新构建

1. 把教程中的 `ExampleTextModule.cs` 和 `ExampleModuleRegistration.cs` 移到 `src/SereinFlow.ScriptModules/`，从验证项目中移除这两个文件。命名空间保持 `SereinFlow.ScriptModules`；验证项目之后可以通过项目引用调用公开注册类。
2. 打开 `src/SereinFlow.ScriptModules/SereinFlowScriptModule.cs`，在 `SereinFlowScriptModuleRegistration.Register` 方法末尾，内置 `sereinflow` 模块注册完成之后，加入这一行：

```csharp
ExampleModuleRegistration.Register(engine);
```

3. 从仓库根目录构建相关项目：

```powershell
dotnet build src/SereinFlow.Worker.Runner/SereinFlow.Worker.Runner.csproj
dotnet build src/SereinFlow.Mcp/SereinFlow.Mcp.csproj
```

4. 按现有宿主的构建/发布方式更新程序，使运行时实际加载新构建的 Worker 和模块程序集；重启使用旧程序集的宿主。只改源码或只重启门户前端不会更新 Worker 中的模块。
5. 在画布脚本中导入 `example` 并调用 `text.addTag("A001")`，应用代码、保存并运行。节点输出应为 `"batch:A001"`。

独立模块项目也可以使用相同机制，但需保证执行注册入口的项目引用该模块程序集，并在宿主与 Worker 发布时包含它。上面的教程采用已有 `ScriptModules` 项目，避免遗漏这一步。

### 为什么需要检查多个执行入口

| 入口 | 当前怎样使用模块 |
| --- | --- |
| `SereinScriptNodeExecutor.ExecuteAsync` | 为每次调用创建引擎，调用上述统一注册入口，再执行源码或缓存。 |
| `SereinLangCompiler` 诊断编译 | 也调用统一注册入口，但传入没有活动运行的上下文，只编译、不调用模块方法。 |
| `ScriptArtifactStore.RebuildProject` | 使用自己的引擎，将 `import` 编译为运行时指令；当前不会在这里解析模块导出或创建业务模块实例。 |

`.ssc` 保存的是字节码，不包含 C# 对象。加载缓存时仍必须注册模块。新增普通的导入模块可按教程接入统一注册入口；如果还引入编译期全局变量或改变字节码约定，需要同步调整诊断编译、缓存重建和执行注入，详见[ScriptAdapter](/script/script-adapter#字节码缓存)。

诊断编译成功不能证明模块已经部署或方法可以成功执行，应至少在实际 Worker 运行一次。

## 运行上下文与值转换

### 进阶：让自定义方法写入运行日志

上面的 `AddTag` 是纯字符串处理，不依赖流程。需要运行日志或当前调用信息时，通过构造函数接收 `SereinFlowScriptModuleContext`。

在教程的 `ExampleTextModule` 类内保留 `_tag`、`IsTarget`、`AddTag`、`Tag`，**替换原构造函数**，并添加下面的字段和方法：

```csharp
private readonly SereinFlowScriptModuleContext? _context;

public ExampleTextModule(string tag, SereinFlowScriptModuleContext? context = null)
{
    _tag = tag;
    _context = context;
}

[PrototypeFunction]
public static async Task<Value> Report(ExampleTextModule target, StringValue text)
{
    var context = target._context
        ?? throw new NotSupportedException("Report requires a flow module context.");
    if (context.IsCancellationRequested)
        throw new OperationCanceledException("The flow invocation was cancelled.");
    var tagged = AddTag(target, text);
    await context.PublishLogAsync("info", tagged);
    return tagged;
}
```

把 `ExampleModuleRegistration.Register` 的签名和创建实例的那一行分别改为：

```csharp
public static void Register(ScriptEngine engine, SereinFlowScriptModuleContext? context = null)
```

```csharp
var text = new ExampleTextModule("batch", context);
```

原型注册和导出表保持原样。接入统一注册入口时，使用下面这行替换之前添加的调用，将当前调用上下文传进来：

```csharp
ExampleModuleRegistration.Register(engine, context);
```

重新构建并更新 Worker 后，画布脚本可以这样调用：

```sereinlang
import { text } from "example"
return text.report("A001")
```

应看到 info 日志 `batch:A001`，节点返回同样的字符串。虽然 C# 方法返回 `Task<Value>`，脚本仍按普通方法调用；生成的包装代码与虚拟机会等待这个任务完成。

`PublishLogAsync` 使用运行时事件出口和调用的取消令牌。自定义方法执行耗时 I/O 时，应额外通过构造函数传入宿主取消令牌并交给底层 API；只在调用前读取 `IsCancellationRequested` 不会中断一个已经开始的外部操作。

### 在自己的引擎中提供运行设施

直接 `new ScriptEngine()` 没有 SereinFlow 的运行日志、环境和变量表。需要自己创建运行设施并注册内置模块。下面的完整 `Program.cs` 配合刚才加入 `Report` 的版本使用：

```csharp
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptModules;
using ScriptLang;
using ScriptLang.Runtime;
using LibraryVariableService = SereinFlow.Library.LibraryVariableService;

var variables = new LibraryVariableService();
var runtime = new NodeExecutionRuntime(
    new NodeExecutionEnvironment(
        Guid.NewGuid(), "demo", Guid.NewGuid(), "main", "script-node", 0),
    eventSink: new ConsoleLogSink(), variables: variables);
var context = new SereinFlowScriptModuleContext(runtime, CancellationToken.None);
var engine = new ScriptEngine();
SereinFlowScriptModuleRegistration.Register(engine, context);
ExampleModuleRegistration.Register(engine, context);
var chunk = engine.CompileSource("""
    import { variables } from "sereinflow"
    import { text } from "example"
    variables.set("order.code", "A001")
    return text.report(variables.get("order.code"))
    """, "example.serein");
var result = await engine.CreateTask(chunk).RunAsync(CancellationToken.None);
Console.WriteLine(((StringValue)result).Value);

sealed class ConsoleLogSink : INodeExecutionEventSink
{
    public ValueTask PublishLogAsync(NodeExecutionLogEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"{entry.Level}: {entry.Message}");
        return ValueTask.CompletedTask;
    }
}
```

结果包含 `info: batch:A001` 和 `batch:A001` 两行。这里的 `ConsoleLogSink` 只是验证程序的日志接收器；真实流程由运行时接收日志并关联节点信息。

这个程序适用于示例模块还独立注册的验证阶段。若已将 `example` 接入统一入口，保留 `SereinFlowScriptModuleRegistration.Register` 即可，移除重复的 `ExampleModuleRegistration.Register` 调用。同一引擎中每个模块注册一次。

### 上下文和实例的生命周期

模块实例按脚本调用创建，`env` 反映当前调用；变量表按运行共享。共享变量应放进 `LibraryVariableService`，不要依赖某个模块实例的字段在下个节点或下次运行继续存在。

诊断编译传入的上下文没有活动运行：`Environment` 为 null，内置 `env` 属性返回默认值，真正执行变量操作时会因缺少变量表而失败。注册阶段只创建对象、声明方法，等实际调用时再获取运行资源；不要在构造函数中要求必须存在运行 ID、读取变量或发起业务请求。

宿主的 `IServiceProvider` 和 DI 服务不会自动成为模块参数。新增业务服务应在 Worker 的执行环境中明确创建或注入，并约定其生命周期。

### 方法参数和返回值怎样转换

原型方法直接接收的是脚本值。优先使用明确的 `Value` 类型，方法内部再取出 CLR 数据：

| 脚本值 | C# 方法中使用 | 创建返回值 |
| --- | --- | --- |
| `"A001"` | `StringValue`，读取 `.Value` | `StringValue.Create("A001")` |
| `true` | `BoolValue`，读取 `.Value` | `BoolValue.Create(true)` |
| `42` | `NumberValue<int>`，读取 `.Value` | `NumberValueFactory.Create(42)` |
| 对象、数组 | `ObjectValue`、`ArrayValue` | 创建对象属性表或数组元素集合。 |
| null 或多种值类型 | `Value`，自行判断具体类型 | `Value.Null` 或具体脚本值。 |

`StringValue` 参数不会把脚本数字自动变成字符串，`NumberValue<int>` 也不能代表所有数值类型；若要接受多种数值类型，应接收 `Value` 并明确处理。类库节点参数的目标 CLR 类型转换属于 [ScriptAdapter](/script/script-adapter#值转换与错误分支)，不要与原型调用的参数检查混淆。

与运行变量交换值时，使用 `SereinFlowScriptValueConverter.ToScriptValue` / `ToVariableValue`：标量拆成 CLR 值，数组转为 `object?[]`，对象转为 `Dictionary<string, object?>`，CLR 句柄保留引用。函数、方法和循环引用不能存储；最多嵌套 32 层、每个容器最多 10,000 项。审计投影用于展示与持久化摘要，不能替代变量存储转换。

## 排错：根据失败阶段定位

| 现象 | 优先检查 |
| --- | --- |
| `partial IsTarget`、接口未实现，或原型无法注册 | 本项目是否引用 Analyzer；类是否 `partial`；是否使用块作用域命名空间；源生成器是否有构建诊断。 |
| 提示找不到 `example`，或找不到同名脚本文件 | 模块是否注册到**执行该脚本的引擎**；Worker 是否加载了新程序集。控制台注册不会影响另一进程。 |
| 可以导入 `text`，但找不到 `addTag` | 是否注册原型、标记 `[PrototypeFunction]`，以及方法名是否为 `addTag`。 |
| `tag` 不是可调用函数 | 使用 `text.tag` 读取属性，方法才使用括号。 |
| 提示参数数量或 `StringValue` 类型不符 | 脚本只传业务参数，不传 `target`；检查参数值实际类型。 |
| 本地有返回值，但看不到运行日志 | 是否提供 `NodeExecutionRuntime.EventSink`；默认空事件接收器不会显示日志。 |
| 变量操作报缺少运行级变量表 | 自定义宿主是否设置 `NodeExecutionRuntime.Variables`；单独注册模块不会创建变量表。 |
| 诊断编译成功，运行仍失败 | 编译不执行导入和业务方法，继续核对注册、部署、输入及运行资源。 |
| 第一次调用正常，另一个节点的数据不见了 | 按调用创建的模块字段不共享；需要跨节点数据时使用运行变量表。 |

修改后先运行教程验证项目，再在真实画布上运行，并验证缓存执行。涉及上下文时还应检查无活动运行的诊断注册、运行取消及不同运行的变量隔离。现有适配器验证命令见[ScriptAdapter](/script/script-adapter#修改后怎样验证)。
