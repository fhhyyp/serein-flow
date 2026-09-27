# Built-in services

The Worker provides services in addition to your `[FlowService]` classes. Request them in the **node class constructor**.

| Service | Purpose |
| --- | --- |
| `IMessageService` | Exchange messages during one run; optionally allow external ingress. |
| `IFlowWorkpiece` | Save images, reports, and other node output files. |
| `IFlowNativeLibraryLoader` | Load native files bundled in the library ZIP. See [Native DLLs](/en/library/native-dll). |

## Wait for a message

`IMessageService.CreateMessageQueue()` creates a queue view. A message on a topic is consumed by one receiver. This node allows the external API to publish JSON to `device.inbox`:

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

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
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

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
Put `IFlowContext` on each node method; put the three services above in the node class constructor.
:::
