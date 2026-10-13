# SereinFlow Library SDK

`SereinFlow.Library` is the standalone public SDK for SereinFlow node
libraries. Reference it from an SDK-style class library project:

```xml
<PackageReference Include="SereinFlow.Library" Version="1.0.0" />
```

All public SDK types use the `SereinFlow.Library` namespace, including metadata
attributes, `IFlowContext`, and built-in services:

```csharp
using SereinFlow.Library;
```

Libraries using the former `SereinFlow.Core.Api` or
`SereinFlow.Runtime.Abstractions.IFlowContext` names must update their imports
and rebuild. Type forwarding does not preserve a renamed type's old identity.
The SDK has no dependency on SereinFlow Domain or server implementation assemblies.

## Run-local variables

`LibraryVariableService` is a variable table shared by every node context,
listener trigger, FlowCall frame, and uploaded library in one flow run.
Request it in the node constructor or use `IFlowContext.Variables`; both expose
the same instance. Different runs have separate tables. Original CLR objects live only in the
Worker; a bounded final JSON projection and type metadata are saved for historical inspection.

```csharp
[FlowLibrary("Variables")]
public sealed class VariableNodes(LibraryVariableService variables)
{
    [FlowNode]
    public void Save(IFlowContext context)
    {
        variables.Set("batch.count", 42);
        context.Variables.Set<string?>("batch.note", null);
    }

    [FlowNode]
    public int Read() => variables.Get<int>("batch.count");
}
```

| Operation | Behavior |
| --- | --- |
| `bool Contain(string name)` | True when the name exists, including a stored null. |
| `object? Get(string name)` | Original object; null when absent. |
| `T? Get<T>(string name)` | Cast/unbox to `T`; `default(T)` when absent. |
| `bool TryGet<T>(string name, out T? value)` | False and `default(T)` when absent; otherwise casts/unboxes and returns true. |
| `void Set<T>(string name, T value)` | Create or replace a value, including null. |

Each operation is protected by a lock. Names are case-sensitive and cannot be
null, empty, or whitespace. Values are stored as `object`, boxing value types
and retaining original references. Generic reads use CLR casts/unboxing:
an `int` cannot be read as `long`, and strings are not parsed. An incompatible
type throws `InvalidCastException` from both `Get<T>` and `TryGet<T>`.
Stored null can be read as a reference or nullable type; reading it as a
non-nullable value type also throws `InvalidCastException`. For `Get<int>`, a
missing variable returns `0`; use `Contain` or `TryGet` to distinguish absence.
The lock protects table access; synchronize changes to stored objects and
read-modify-write sequences separately.

`Snapshot()` copies the current variable table under its lock. Active Workers
also expose variable reads and writes over HTTP/MCP, with project-scoped
`run.read` and `run.variable.write` permissions. Writes require an explicit
built-in value type; arbitrary CLR objects remain in the Worker. The frontend's
Flow variables panel refreshes every second while open and retains its last
fetched snapshot after the Worker stops.

## Run-local messaging

Node libraries can use the same `IMessageService` instance during one Worker
run. Queues are competing-consumer FIFO channels; event-bus subscriptions are
independent broadcast cursors. JSON is the default representation and is
recommended when more than one library participates:

```csharp
public sealed class DeviceNodes
{
    private readonly IMessageService _messages;

    public DeviceNodes(IMessageService messages) => _messages = messages;

    public async Task<string> WaitForDevice(IFlowContext context)
    {
        var queue = _messages.CreateMessageQueue(new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "device.trigger.v1"
        });
        return await queue.ReceiveAsync<string>(
            "device.trigger",
            context.CancellationToken);
    }
}
```

`ExternalIngress` must be set explicitly; internal topics are not exposed to
the API. `DirectObject` is limited to compatible CLR types in the same Worker
process. Every wait and subscription should use the supplied cancellation
token, and subscriptions should be disposed with `await using`.

## Non-JSON node results

Use a result converter when a node returns an in-process value such as a
`Bitmap`, `Mat`, serial-port handle, or HTTP object. The node keeps the
original value for downstream nodes; the converter only supplies the value
written to events and exposed to JSON clients:

```csharp
public sealed class MatConverter : INodeResultConverter<Mat, object>
{
    public object Transfer(Mat primitive) => new
    {
        type = "mat",
        width = primitive.Width,
        height = primitive.Height
    };
}

[NodeResult<MatConverter>]
public Mat CaptureFrame() => ...;
```

The non-generic form is available for older C# versions:
`[NodeResultAttribute(typeof(MatConverter))]`. Converters are discovered and
registered automatically from the uploaded library. `TTransfer` should be a
JSON-safe value; use a workpiece for the binary image or file itself.

## Images and files (workpieces)

Inject `IFlowWorkpiece` into a node constructor to store binary data once per
run without embedding it in JSON. The returned `FlowWorkpieceInfo` contains a
stable ID and metadata; the API and MCP expose the corresponding listing and
download URL:

```csharp
public sealed class ImageNodes
{
    private readonly IFlowWorkpiece _flowWorkpiece;

    public ImageNodes(IFlowWorkpiece flowWorkpiece)
        => _flowWorkpiece = flowWorkpiece;

    public FlowWorkpieceInfo SaveImage(byte[] png)
        => _flowWorkpiece.UploadImage("inspection.png", png, FlowWorkpieceContentTypes.Png);

    public FlowWorkpieceInfo SaveReport(Stream report)
        => _flowWorkpiece.UploadFile("report.txt", report, "text/plain");

    public FlowWorkpieceInfo SaveNodeOutput(IFlowContext context, byte[] json)
        => _flowWorkpiece.UploadNodeOutput(context, "result.json", json, FlowWorkpieceContentTypes.Json);
}
```

`FlowWorkpieceContentTypes.Png` and `FlowWorkpieceContentTypes.Json` are
provided as compile-time constants for the common MIME types, so node
libraries do not need to repeat the string literals.

`UploadImage` accepts a byte array, stream, or base64/data URI. `UploadFile`
and `UploadNodeOutput` accept a byte array or stream. `UploadNodeOutput` stores
the node ID and execution-step ID from `IFlowContext` in the workpiece metadata
so the debugger can select the first artifact from the exact execution while
keeping all run workpieces visible. File names are restricted to a single file
name, streams remain owned by the caller, and each workpiece is stored under the
current run's server-managed workpiece directory.

## Native library dependencies

Native dependencies can be loaded explicitly from a path relative to the
uploaded library assembly. The loader is run-scoped and idempotent, so it is
safe to request the same dependency from more than one node constructor:

```csharp
public ImageNodes(IFlowWorkpiece workpiece, IFlowNativeLibraryLoader native)
{
    _workpiece = workpiece;
    native.LoadNativeLibraryDirectory("vendor/native/{rid}");
}
```

For fixed dependencies, declare the directory once at assembly or node-class
level. `{rid}` selects the current runtime, such as `win-x64`:

```csharp
[assembly: NativeLibraryDirectory("runtimes/{rid}/native", Recursive = false)]
```

Directories and files must remain inside the uploaded package. Use the
correct runtime-specific directory instead of loading both x86 and x64
variants. `FlowNativeLibraryException` reports a machine-readable failure
code when a required native dependency cannot be loaded.

## Constructor injection

Node libraries can declare services with `FlowService`. Services are isolated
to the uploaded library assembly in one Worker run, and node classes receive
them through normal constructors:

```csharp
[FlowService]
public sealed class DeviceClient
{
}

[FlowService<IDeviceChannel>]
public sealed class DeviceChannel : IDeviceChannel
{
}

[FlowLibrary("Device library")]
public sealed class DeviceNodes
{
    public DeviceNodes(DeviceClient client, IDeviceChannel channel)
    {
    }
}
```

For C# versions that do not support generic attributes, declare the same
contract with `[FlowService(typeof(IDeviceChannel))]` instead.

`FlowServiceLifetime.Run` is the default and shares one service instance for
the library assembly throughout a Worker run. `Invocation` shares an instance
only within one node call. `Transient` creates a new instance for every
resolution. When a service exposes an interface, its concrete type and that
interface resolve to the same instance for `Run` and `Invocation` lifetimes.

The Worker does not expose `IServiceProvider`, `IServiceScopeFactory`,
`IFlowContext`, API repositories, configuration, secrets, or Worker protocol
objects through constructor injection. Register only concrete, closed classes
from the same uploaded library assembly.

Leave `Id` unset for ordinary nodes and parameters. SereinFlow derives a node
ID from the library name (or declaring class name) and method name, and derives
a parameter ID from its CLR parameter name. Do not define local legacy
`DynamicFlow` or `NodeAction` attributes.
