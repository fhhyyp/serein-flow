# Internal service registration

At startup, SereinFlow registers the services needed for runs in the ASP.NET Core container. Usually one `AddSereinFlow` call is enough; you do not register each database service, executor, or controller separately.

## What `AddSereinFlow` registers

The current implementation adds storage and application services, SignalR, CORS, startup checks, and execution services. `UseWebApi()` also adds Web API, problem responses, and documentation; `UseMcp()` adds MCP services.

```text
AddSereinFlow
  ├─ Storage and application services
  ├─ Execution services and startup checks
  ├─ UseWebApi → Controllers, docs, run/workspace hubs
  └─ UseMcp    → HTTP MCP
```

Uploaded nodes run in a Worker. It only supplies public node services and objects declared with `[FlowService]` in the same library. The host's database objects, configuration, and `IServiceProvider` are not passed to node constructors.

## Where to make a change

| Need | Place |
| --- | --- |
| Add a business action to the canvas | Create a [node library](/en/library/) and mark a method `[FlowNode]`. |
| Share an object between nodes | Register it with `[FlowService]` in the library; see [Services](/en/library/services). |
| Add SereinFlow to an ASP.NET Core site | Follow [host setup](/en/integration/hosting). |
| Start flows from another .NET process | Register the [client SDK](/en/integration/client-di). |
| Change script compilation, execution, or caching | Extend [ScriptAdapter](/en/script/script-adapter) and update Worker registration. |
| Add importable script methods or properties | Declare prototypes and register modules in [ScriptModules](/en/script/script-modules). |

The source also contains `AddSereinFlowStorage`, `AddSereinFlowApplication`, and `AddSereinFlowExecution`. Normal host integration uses `AddSereinFlow` to keep those registrations aligned.
