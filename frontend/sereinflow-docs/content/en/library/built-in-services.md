# Built-in services

The Worker provides services in addition to your `[FlowService]` classes. Request them in the **node class constructor**.

| Service | Purpose |
| --- | --- |
| `LibraryVariableService` | Share variables across all contexts and libraries in one run, with locked reads and writes. |
| `IMessageService` | Exchange messages during one run; optionally allow external ingress. |
| `IFlowWorkpiece` | Save images, reports, and other node output files. |
| `IFlowNativeLibraryLoader` | Load native files bundled in the library ZIP. See [Native DLLs](/en/library/native-dll). |

## Share flow variables

Each run owns one `LibraryVariableService` table shared by nodes, listener
triggers, and FlowCall frames. Constructor injection and `IFlowContext.Variables`
expose the same instance. Different runs are isolated. Original CLR objects remain
in Worker memory; a bounded final JSON projection and type metadata are saved for
historical snapshots. All SDK types use `using SereinFlow.Library;`; libraries using the
former SDK namespaces must update imports and rebuild.

```csharp
using SereinFlow.Library;

[FlowLibrary("Batch variables")]
public sealed class BatchNodes(LibraryVariableService variables)
{
    [FlowNode(AnotherName = "Save batch")]
    public void Save(IFlowContext context)
    {
        variables.Set("batch.count", 42);
        context.Variables.Set<string?>("batch.note", null);
    }

    [FlowNode(AnotherName = "Read batch")]
    public int Read() => variables.Get<int>("batch.count");
}
```

| Method | Result |
| --- | --- |
| `Contain(name)` | True when the name exists, including a stored null. |
| `Get(name)` | Original object; null when absent. |
| `Get<T>(name)` | Cast/unbox to T; `default(T)` when absent. |
| `TryGet<T>(name, out value)` | False and `default(T)` when absent; otherwise casts/unboxes and returns true. |
| `Set<T>(name, value)` | Create or replace a variable, including null. |

Names are case-sensitive and cannot be null, empty, or whitespace. Value types
are boxed and reference types retain their identity. Generic reads use CLR casts
without text parsing or numeric conversion: an `int` cannot be read as `long`.
Both generic read methods throw `InvalidCastException` on a type mismatch or
when stored null is read as a non-nullable value type. A missing `Get<int>`
returns 0; use `Contain` or `TryGet` to distinguish absence.

The lock protects each table operation. Synchronize read-modify-write sequences
and mutations of stored objects separately.

`Snapshot()` copies the current table under its lock while retaining object references. Active-run variables can also be read/written through HTTP/MCP and inspected in the **Flow variables** workspace panel. See [Runs API](/en/api/runs#flow-variables) for routes and value types.

Script nodes use the same table through `import { variables } from "sereinflow"`,
with `variables.contain(name)`, `variables.get(name)` and
`variables.set(name, value)`. See
[Script flow variables](/en/modules/nodes-and-libraries#read-and-write-flow-variables-in-scripts)
for examples and conversion rules.

## Wait for a message

`IMessageService.CreateMessageQueue()` creates a queue view. A message on a topic is consumed by one receiver. This node allows the external API to publish JSON to `device.inbox`:

```csharp
using SereinFlow.Library;

[FlowLibrary("Device messages")]
public sealed class MessageNodes(IMessageService messages)
{
    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "Wait for device message")]
    public async Task<string> Wait(IFlowContext context)
    {
        var queue = messages.CreateMessageQueue(new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "device.message.v1"
        });
        return await queue.ReceiveAsync<string>("device.inbox", context.CancellationToken);
    }
}
```

Queues use JSON by default. External publishing requires `ExternalIngress = true`; internal topics are closed by default. Use `CreateEventBus()` to deliver an event to multiple subscribers and dispose subscriptions asynchronously. Messages exist only during this Worker run, so they are not persistent storage.

## Save a generated file

`IFlowWorkpiece.UploadNodeOutput` stores a file with its producing node and execution from `IFlowContext`. It returns `FlowWorkpieceInfo` with ID, name, type, and size:

```csharp
using SereinFlow.Library;

[FlowLibrary("Report tools")]
public sealed class ReportNodes(IFlowWorkpiece workpieces)
{
    [FlowNode(AnotherName = "Save report")]
    public string Save(IFlowContext context)
    {
        var file = workpieces.UploadNodeOutput(
            context, "report.txt", "Inspection complete", "text/plain; charset=utf-8");
        return file.Id;
    }
}
```

It also accepts `byte[]` or `Stream` for binary content. List and download these files through the [Runs and workpieces API](/en/api/runs).

::: tip Parameters versus constructor services
Put `IFlowContext` on each node method; put the services above in the node class constructor. The variable table is also available as `context.Variables`.
:::
