# SereinScript usage and development

SereinScript is the engine behind Script nodes. Write a short program on the canvas to receive inputs, transform data, share variables within a run, and return values to downstream nodes. Source code and some APIs call the language **SereinLang**; the runtime project is **ScriptLang**. These names refer to the scripting capability described here.

## Start with your goal

| Goal | Read |
| --- | --- |
| Write and run scripts in the console; learn syntax and input/output behavior | [Use scripts in flows](/en/script/usage) |
| Change compilation, execution, caching, or exchange with .NET nodes | [Extend ScriptAdapter](/en/script/script-adapter) |
| Expose C# methods, properties, or importable modules to scripts | [Extend ScriptModules](/en/script/script-modules) |
| Expose a business method as its own canvas node | [Node libraries](/en/library/) |

## What each project does

```text
Script node source and input/output contracts
  → SereinFlow.ScriptAdapter: compilation, execution, input/output mapping, .ssc cache
    → ScriptLang: parsing, bytecode compilation, virtual machine execution
    → SereinFlow.ScriptModules: sereinflow module, run logs, environment, variables
```

Development involves `src/SereinFlow.ScriptAdapter` and `src/SereinFlow.ScriptModules`. The pinned engine and prototype generator live in `src/ThirdParty/SereinScript`. Normal builds need no separate SereinScript checkout or path environment variable. Both adapter projects currently target .NET 10.

The console includes a Script node, so writing a script requires no library upload. Adding a script module requires changing registration and rebuilding the relevant host and Worker. Uploading a `[FlowNode]` library does not automatically add an `import` module.

## Start with a result

```sereinlang
import { log, env } from "sereinflow"
log.info("Script started")
return { nodeId = env.nodeId, total = 42 }
```

The default Script node has one output, which receives the whole returned object. Multiple connectors require output contracts; see [Return values and output connectors](/en/script/usage#return-values-and-output-connectors).

Scripts execute in a separate Worker. Console, HTTP, and event records receive a projection for display and persistence; CLR handles and similar values may appear as type summaries. Custom modules should follow the [runtime context and conversion rules](/en/script/script-modules#runtime-context-and-value-conversion).
