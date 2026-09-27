# Register your own services

When two nodes use the same device, put the device access in a C# class marked with `[FlowService]`. The Worker supplies it to node constructors.

## Minimal example

```csharp
using SereinFlow.Core.Api;

namespace MyFlowNodes;

public interface IDeviceReader
{
    string ReadStatus(string deviceId);
}

[FlowService<IDeviceReader>]
public sealed class DeviceReader : IDeviceReader
{
    public string ReadStatus(string deviceId) => $"{deviceId}: ready";
}

[FlowLibrary("Device tools")]
public sealed class DeviceNodes(IDeviceReader reader)
{
    [FlowNode(AnotherName = "Read device status")]
    public string Read([NodeParam(Name = "Device ID")] string deviceId)
        => reader.ReadStatus(deviceId);
}
```

`[FlowService<IDeviceReader>]` registers the implementation for interface injection. Use `[FlowService]` for concrete-type injection, or `[FlowService(typeof(IDeviceReader))]` as the non-generic form.

## Lifetime

| `Lifetime` | Behavior | Use |
| --- | --- | --- |
| `Run` (default) | Shared by a library within one Worker run. | Per-run connections or state. |
| `Invocation` | Shared within one node invocation. | Invocation-specific work objects. |
| `Transient` | Created each time requested. | Lightweight stateless objects. |

For example, `[FlowService<IDeviceReader>(Lifetime = FlowServiceLifetime.Invocation)]` creates a service per invocation. Resolving an interface or concrete type gives the same object under `Run` and `Invocation`. There is no singleton across runs.

## Injectable dependencies

Node constructors can receive services registered with `[FlowService]` in the same uploaded assembly, plus Worker-provided `IMessageService`, `IFlowWorkpiece`, and `IFlowNativeLibraryLoader`. The implementation must be a concrete, non-open-generic class in that assembly.

Put `IFlowContext` on the **node method**. Host `IServiceProvider`, `IServiceScopeFactory`, configuration, API repositories, keys, and Worker internals are not node constructor services.

::: tip Separate from ASP.NET Core services
`[FlowService]` registers services only for an uploaded library in the Worker. To host SereinFlow in your ASP.NET Core app, see [Host integration](/en/integration/hosting).
:::
