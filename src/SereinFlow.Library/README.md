# SereinFlow Library SDK

`SereinFlow.Library` is the standalone public SDK for SereinFlow node
libraries. Reference it from an SDK-style class library project:

```xml
<PackageReference Include="SereinFlow.Library" Version="1.0.0" />
```

Use `SereinFlow.Core.Api` for `FlowLibrary`, `FlowNode`, `NodeParam`, and
`NodeType`. Use `SereinFlow.Runtime.Abstractions.IFlowContext` when a node
needs to select an execution branch. The SDK has no dependency on SereinFlow
Domain or server implementation assemblies.

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
