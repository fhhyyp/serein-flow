# Development: ScriptModules

`src/SereinFlow.ScriptModules` exposes C# methods and properties to scripts. The built-in `sereinflow` module exports `log`, `env`, and `variables`, registered for each invocation by `SereinFlowScriptModuleRegistration.Register(engine, context)`.

## Register prototypes and exports

1. **Register a prototype** with `engine.PrototypeManager.Register(instance)` to define the methods and properties available on a value.
2. **Register a module** with `engine.ImportResolver.RegisterBuiltinModule(name, exports)` to define members available through `import { member } from "name"`.

Use `ObjectValue` for exports and `ClrObjectValue` to wrap C# instances. Putting an object in the export table does not expose arbitrary C# members; declare the prototype explicitly.

## Add a method and a property

Add the following file to `SereinFlow.ScriptModules`. That project already references ScriptLang and includes `ScriptLang.Generator` as an Analyzer. A separate module project needs both references too.

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

The prototype generator completes the interfaces for the `partial` class and `partial IsTarget` method. `IsTarget` restricts the prototype to its intended object type. `PushThis = true` passes the script receiver as the first static method argument. `NamingFormat.Js` maps `AddTag` and `Tag` to `addTag` and `tag`.

Register before creating and running a task:

```csharp
using SereinFlow.ScriptModules;
using ScriptLang;
using ScriptLang.Runtime;

var engine = new ScriptEngine();
ExampleModuleRegistration.Register(engine);
var chunk = engine.CompileSource("""
    import { text } from "example"
    return text.addTag(text.tag)
    """, "example.serein");
var result = await engine.CreateTask(chunk).RunAsync(CancellationToken.None);
Console.WriteLine(((StringValue)result).Value); // batch:batch
```

Use the existing project references as a model:

```xml
<ItemGroup>
  <ProjectReference Include="..\ThirdParty\SereinScript\ScriptLang\ScriptLang.csproj" />
  <ProjectReference Include="..\ThirdParty\SereinScript\ScriptLang.Generator\ScriptLang.Generator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Adjust paths for the new project's location. The current prototype generator requires the block namespace form shown above (`namespace ... { }`). Prototypes are generated at build time. If partial methods or interfaces appear unimplemented, check the namespace form, Analyzer reference, `partial` declarations, and attributes.

## Make flow nodes use the module

The standalone example registers only its own engine. To allow canvas scripts to import `example`, call `ExampleModuleRegistration.Register(engine)` from `SereinFlowScriptModuleRegistration.Register`, then rebuild the host and Worker that use these projects.

Both `SereinScriptNodeExecutor.ExecuteAsync` and diagnostic compilation in `SereinLangCompiler` call this registration entry point. Each invocation gets a new engine. Register modules even when loading `.ssc`; bytecode does not store C# module instances.

`ScriptArtifactStore.RebuildProject` creates its own engine. Currently, `import` compiles into a runtime instruction without executing or validating module exports during rebuild. Extensions that add globals or compilation conventions must update diagnostic compilation, cache rebuild, and runtime injection together, and may require a cache compatibility version change.

Uploading `[FlowNode]` or `[FlowService]` libraries does not register script modules. Importable capabilities must enter through engine registration. Host DI services are not automatically passed to Worker script modules.

## Runtime context and value conversion

`SereinFlowScriptModuleContext` receives the invocation's `NodeExecutionRuntime` and `CancellationToken`. Logs and environment instances are per invocation; variables access the run's shared `LibraryVariableService` through this context.

| Capability | Extension convention |
| --- | --- |
| Logs | Use `PublishLogAsync` or the run event sink so the runtime associates run and node identity. |
| Environment | Read `Environment` for invocation metadata and query cancellation on demand. |
| Variables | Share through the run table; custom hosts must supply `NodeExecutionRuntime.Variables`. |
| Async calls | Prototypes can return supported task types such as `Task<Value>`; follow built-in `log` and observe cancellation in host operations. |
| Conversion | Use `SereinFlowScriptValueConverter.ToScriptValue` and `ToVariableValue` for variable values. |

Diagnostic registration has no active run: `Environment` is null, built-in environment properties return empty strings or defaults, and variable operations fail without a run table. Registration should declare capabilities; access run resources when the script calls a method.

Arrays become `object?[]`, objects become `Dictionary<string, object?>`, and containers are copied. CLR handles retain identity. Storage rejects functions, methods, and cycles, with limits of 32 nesting levels and 10,000 items per container. Do not store audit summaries as variables or attach one run's context to a static module instance.

Keep exports scoped to the intended context rather than exposing the whole flow session or host configuration. Validate actual calls, cached execution, diagnostic registration without an active run, cancellation, and variable isolation. See [ScriptAdapter](/en/script/script-adapter#validate-a-change) for the existing verification command.
