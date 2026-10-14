# ScriptModules: use and extend script modules

Script modules provide functions that SereinScript can call directly. For example, `log.info("Started")` writes a run log, while `variables.set("order.code", "A001")` saves a variable shared within that run. Script authors import modules; developers implement the functionality in C# and register it with the engine.

| Goal | Start here |
| --- | --- |
| Use logs, invocation metadata, and variables on the canvas | [Users: use built-in modules](#users-use-built-in-modules) |
| Add your own script methods and properties | [Development tutorial: create the example module](#development-tutorial-create-the-example-module) |
| Make a locally verified module available to canvas scripts | [Make flow nodes use the module](#make-flow-nodes-use-the-module) |
| Publish run logs, inspect invocation metadata, or handle cancellation | [Runtime context and value conversion](#runtime-context-and-value-conversion) |

See [Use scripts in flows](/en/script/usage) for editing, inputs and outputs, and language syntax. This page explains how to call a module and how to build one that scripts can call.

## Users: use built-in modules

### Run your first example on the canvas

The flow executor already registers the built-in `sereinflow` module. No package installation or DLL upload is required.

1. Drag a Script node onto the canvas, select it, and mark it as the **Flow entry**.
2. Open its script editor from the inspector and paste the following code. No script inputs are needed.
3. Select **Apply code**, then **Save** and **Run flow**.
4. Look for `A001` in run logs, `orderCode` and `nodeId` in the node output, and `order.code` in **Flow variables**.

```sereinlang
import { log, env, variables } from "sereinflow"
variables.set("order.code", "A001")
let code = variables.get("order.code")
log.info(code)
return { orderCode = code, nodeId = env.nodeId }
```

The output is an object: `orderCode` is `"A001"`, and `nodeId` is the current Script node's ID. The default single output receives the entire object; it does not automatically become two connectors.

### Understand import and member calls

For `import { log } from "sereinflow"` and `log.info("A001")`:

| Form | Meaning here |
| --- | --- |
| `"sereinflow"` | A module name registered by the host. This built-in name is not a DLL path. |
| `{ log }` | The exported member to import. You can also import `env` and `variables` in the same statement. |
| `log.info("A001")` | Call a method on the log object; parentheses contain the script arguments. |
| `env.nodeId` | Read a property on the environment object; do not add `()`. |

The built-in module currently provides:

| Export | Usage | Result and effect |
| --- | --- | --- |
| `log` | `log.info(value)`, `log.error(value)` | Publish info/error run logs and return null. An error log does not itself fail the node. |
| `env` | `env.nodeId`, `env.canvasId`, `env.projectId`, `env.flowId`, `env.runId` | Read the current invocation's IDs as strings. |
| `env` | `env.frameDepth`, `env.isCancellationRequested` | Read invocation depth and cancellation state. |
| `variables` | `variables.set(name, value)` | Create or replace a variable; return null. |
| `variables` | `variables.get(name)` | Read a variable; absent names and stored null return null. |
| `variables` | `variables.contain(name)` | Test presence, including stored null. The method is named `contain`. |

Variable names are case-sensitive and cannot be empty or whitespace-only. Scripts, C# library nodes, triggers, and FlowCall frames in one run share this table; another run uses another table. Arrays and objects are copied on conversion, so call `set` again to save changes. See [Share variables within a run](/en/script/usage#share-variables-within-a-run) for the full rules.

The `example` module introduced below is created by this tutorial and is not included in a standard installation. Developers must register and deploy it before canvas users can `import { text } from "example"`.

## Before development: modules, exports, and prototypes

Suppose you want `text.addTag("A001")` to return `"batch:A001"`. Three different names are involved:

```text
import { text } from "example"
          │              └─ Module name: registered with ImportResolver
          └─ Export name: the "text" key in the module's exports
text.addTag("A001")
     └─ Method name: the prototype exposes C# AddTag as addTag
```

A **module** groups importable members. An **exported object** is the value the script receives; here it is a `ClrObjectValue` wrapping a C# instance. A **prototype** defines script methods and properties for that value.

Complete both registration steps:

| Registration | What it establishes | When omitted |
| --- | --- | --- |
| `engine.PrototypeManager.Register(instance)` | How the engine handles `addTag`, `tag`, and similar members on the object. | The object may import, but its custom prototype members cannot be found. |
| `engine.ImportResolver.RegisterBuiltinModule("example", exports)` | Where `import` gets `text`. | Runtime module resolution fails or attempts to find a file with that name. |

`src/SereinFlow.ScriptModules` supplies script capabilities. `src/SereinFlow.ScriptAdapter` handles compilation, execution, and input/output mapping for flow nodes. Use a [node library](/en/library/) to add a draggable business node; extend script modules to give existing scripts a business function. Uploading `[FlowNode]` or `[FlowService]` libraries does not automatically register script modules.

## Development tutorial: create the example module

Verify locally in a console application first, then integrate with the Worker. The finished module supports:

```sereinlang
import { text } from "example"
return text.addTag("A001")
```

### 1. Create a verification project and configure references

From the **SereinFlow repository root**, with the .NET 10 SDK installed:

```powershell
dotnet new console --name ScriptModuleDemo --output examples/ScriptModuleDemo --framework net10.0
```

Replace `examples/ScriptModuleDemo/ScriptModuleDemo.csproj` with the following. Paths are relative to that project; adjust them if you place it elsewhere.

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

The first reference supplies SereinFlow modules, runtime context, and their ScriptLang dependencies. The second runs the prototype source generator in this verification project. An `Analyzer` reference generates code at compile time; it is not a business assembly call.

When developing directly in `src/SereinFlow.ScriptModules`, its runtime and Analyzer references are already configured. For now, put the following two C# files in the verification project so you can run them independently.

### 2. Implement a method and a property

Create `examples/ScriptModuleDemo/ExampleTextModule.cs`:

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

Map each C# element to its script behavior:

| C# element | Script behavior |
| --- | --- |
| `[PrototypeExtension(...)]`, `partial` | The generator completes interfaces such as `IPrototype`, enabling prototype registration. |
| `IsTarget(...)` | Restricts this prototype to values wrapping `ExampleTextModule`. |
| `PushThis = true` | The engine supplies the C# object behind `text` as `target`; scripts do not pass this argument. |
| `NamingFormat.Js` | `AddTag` and `Tag` become `addTag` and `tag`. |
| `[PrototypeFunction] AddTag(...)` | `text.addTag("A001")`; the script supplies one string. |
| `[PrototypeProperty] Tag(...)` | `text.tag`; the script reads a property even though C# implements it with a method. |
| `StringValue text`, `text.Value` | The script string wrapper and its underlying C# `string`. |
| `StringValue.Create(...)` | Wraps a C# string as a script return value. |

The current generator locates classes through block namespaces, so keep `namespace ... { }`. The prototype class must be `partial`. The generator supplies the defining declaration of `IsTarget`; the example supplies its implementation. Generated code participates in the build, so do not manually implement the generated interfaces.

### 3. Register the prototype and export the object

Create `examples/ScriptModuleDemo/ExampleModuleRegistration.cs`:

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

`"batch"` is the instance's tag, `"example"` is the module name, and the dictionary key `"text"` is the export name. The same `text` instance is used for prototype registration and the exported value.

Prototype attributes establish these script names, argument checks, and call wrappers. The current engine also has CLR reflection fallback for `ClrObjectValue`, so public C# properties and methods may be accessible under their original names. Prototype attributes are not an access allowlist. Use dedicated wrapper objects and keep public members scoped to the intended script functionality.

### 4. Create an engine and call the module

Replace `examples/ScriptModuleDemo/Program.cs` with this complete program:

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

Run from the repository root:

```powershell
dotnet run --project examples/ScriptModuleDemo/ScriptModuleDemo.csproj
```

The result line should be `batch:A001`. Change the script's last line to `return text.tag` to get `batch`. This verifies the complete path: generate the prototype, register exports, import a member, call C#, and return a script value.

This console application registers only its own engine. The running SereinFlow Worker does not yet know about `example`.

## Make flow nodes use the module

### Update registration and rebuild

1. Move `ExampleTextModule.cs` and `ExampleModuleRegistration.cs` into `src/SereinFlow.ScriptModules/`, removing them from the verification project. Keep the `SereinFlow.ScriptModules` namespace. The verification project can then call the public registration class through its project reference.
2. Open `src/SereinFlow.ScriptModules/SereinFlowScriptModule.cs`. At the end of `SereinFlowScriptModuleRegistration.Register`, after registering the built-in `sereinflow` module, add:

```csharp
ExampleModuleRegistration.Register(engine);
```

3. Build the relevant projects from the repository root:

```powershell
dotnet build src/SereinFlow.Worker.Runner/SereinFlow.Worker.Runner.csproj
dotnet build src/SereinFlow.Mcp/SereinFlow.Mcp.csproj
```

4. Follow your host's existing build/publish process so it loads the updated Worker and module assemblies. Restart hosts using old assemblies. Editing source or restarting only the portal frontend does not update Worker modules.
5. In a canvas script, import `example` and call `text.addTag("A001")`, then apply, save, and run. The node should return `"batch:A001"`.

A separate module project follows the same mechanism, but the project containing the registration entry point must reference its assembly and include it in host and Worker publishing. This tutorial uses the existing `ScriptModules` project to make that dependency explicit.

### Why multiple entry points matter

| Entry point | Current module behavior |
| --- | --- |
| `SereinScriptNodeExecutor.ExecuteAsync` | Creates an engine per invocation, calls the shared registration entry point, and runs source or cached code. |
| `SereinLangCompiler` diagnostics | Calls the same registration entry point with no active run; it compiles without calling module methods. |
| `ScriptArtifactStore.RebuildProject` | Uses a separate engine and compiles `import` to a runtime instruction. It does not currently resolve exports or create business module instances here. |

`.ssc` contains bytecode, not C# objects. Register modules when loading cached code too. Ordinary importable modules can use the shared entry point shown here. Extensions that add compile-time globals or change bytecode conventions must also update diagnostic compilation, cache rebuilding, and runtime injection. See [ScriptAdapter](/en/script/script-adapter#bytecode-caching).

Diagnostic compilation success does not establish deployment or successful method execution. Run at least once in the actual Worker.

## Runtime context and value conversion

### Advanced: publish run logs from a custom method

`AddTag` is pure string processing and does not depend on a flow. Receive `SereinFlowScriptModuleContext` through the constructor when a method needs logs or invocation metadata.

Inside `ExampleTextModule`, retain `_tag`, `IsTarget`, `AddTag`, and `Tag`. **Replace the original constructor** and add this field and method:

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

Change the registration method's signature and the instance creation line respectively:

```csharp
public static void Register(ScriptEngine engine, SereinFlowScriptModuleContext? context = null)
```

```csharp
var text = new ExampleTextModule("batch", context);
```

Keep prototype registration and the export table unchanged. In the shared registration entry point, replace the previous call with this one to pass the current invocation context:

```csharp
ExampleModuleRegistration.Register(engine, context);
```

After rebuilding and updating the Worker, a canvas script can call:

```sereinlang
import { text } from "example"
return text.report("A001")
```

Expect an info log of `batch:A001` and the same string as the node result. Although C# returns `Task<Value>`, scripts call it as a normal method; the generated wrapper and VM await its completion.

`PublishLogAsync` uses the run event sink and invocation cancellation token. For long-running I/O, pass a host cancellation token separately through the constructor and into the underlying API. A cancellation check before a call cannot interrupt an external operation that has already started.

### Supply runtime facilities in your own engine

A bare `new ScriptEngine()` has no SereinFlow run logs, environment, or variable table. Create runtime facilities and register the built-in module yourself. This complete `Program.cs` uses the version with `Report` added above:

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

The result includes `info: batch:A001` and `batch:A001`. `ConsoleLogSink` is this verification application's log receiver; actual flows use the runtime's sink and node association.

This program is for verification while the example module is still registered separately. Once `example` is integrated into the shared entry point, keep `SereinFlowScriptModuleRegistration.Register` and remove the duplicate `ExampleModuleRegistration.Register` call. Register each module once per engine.

### Context and instance lifetimes

Module instances are created per script invocation; `env` describes that invocation. The variable table is shared per run. Put shared state in `LibraryVariableService`; do not assume instance fields survive into the next node or another run.

Diagnostic compilation receives a context without an active run. `Environment` is null, built-in `env` properties return defaults, and actual variable operations fail without a run table. Registration should create objects and declare methods. Acquire run resources during actual calls, not by requiring run IDs, reading variables, or initiating business requests in constructors.

Host `IServiceProvider` and DI services do not automatically become module arguments. Create or inject business services explicitly in the Worker execution environment and define their lifetimes.

### Convert method parameters and return values

Prototype methods receive script values directly. Prefer explicit `Value` types and unwrap CLR data inside the method:

| Script value | C# parameter handling | Create a return value |
| --- | --- | --- |
| `"A001"` | `StringValue`; read `.Value` | `StringValue.Create("A001")` |
| `true` | `BoolValue`; read `.Value` | `BoolValue.Create(true)` |
| `42` | `NumberValue<int>`; read `.Value` | `NumberValueFactory.Create(42)` |
| Object or array | `ObjectValue`, `ArrayValue` | Create a property dictionary or element collection. |
| Null or multiple value kinds | `Value`; inspect the concrete type | `Value.Null` or a specific script value. |

A `StringValue` parameter does not automatically convert numbers to strings. `NumberValue<int>` does not accept every numeric kind; use `Value` and handle each intended kind when necessary. Target CLR conversion for library node parameters belongs to [ScriptAdapter](/en/script/script-adapter#conversion-and-execution-branches), distinct from prototype argument checks.

For run variables, use `SereinFlowScriptValueConverter.ToScriptValue` / `ToVariableValue`. Scalars unwrap to CLR values, arrays become `object?[]`, objects become `Dictionary<string, object?>`, and CLR handles retain identity. Functions, methods, and cycles cannot be stored; conversion allows up to 32 nesting levels and 10,000 items per container. Audit projections are display/persistence summaries and do not replace variable conversion.

## Troubleshoot by failure stage

| Symptom | Check first |
| --- | --- |
| Unimplemented `partial IsTarget` or interfaces; prototype cannot register | This project's Analyzer reference, `partial` class, block namespace, and generator build diagnostics. |
| Cannot find `example` or a script file with that name | Register the module on the engine that actually runs the script; ensure the Worker loads updated assemblies. Console registration does not affect another process. |
| `text` imports, but `addTag` cannot be found | Prototype registration, `[PrototypeFunction]`, and the script name `addTag`. |
| `tag` is not callable | Read it as `text.tag`; parentheses are for methods. |
| Wrong argument count or `StringValue` type | Pass business arguments only, not `target`; check actual script value types. |
| Local calls return values but no run logs appear | Supply `NodeExecutionRuntime.EventSink`; the default null sink does not display logs. |
| Variable operations require a run table | Supply `NodeExecutionRuntime.Variables` in custom hosts; module registration creates no table. |
| Diagnostics succeed, but execution fails | Compilation does not execute imports or business methods. Check registration, deployment, inputs, and run resources. |
| Data is missing in another node | Per-invocation module fields are not shared; use the run variable table for cross-node state. |

Run the verification project first, then run on the actual canvas and check cached execution. For context changes, also verify registration without an active run, cancellation, and variable isolation between runs. See [ScriptAdapter](/en/script/script-adapter#validate-a-change) for the existing validation command.
