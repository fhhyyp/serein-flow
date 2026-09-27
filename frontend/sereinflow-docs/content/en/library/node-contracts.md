# Node contracts

After writing a method, decide what appears on the canvas, where inputs come from, and when execution continues.

## Labels and stable identifiers

| Code | Effect |
| --- | --- |
| `[FlowLibrary("Device tools")]` | Library name in the catalog; defaults to the class name. |
| `[FlowNode(AnotherName = "Read temperature", Desc = "Read the current value")]` | Node display name and description. |
| `[NodeParam(Name = "Device ID")] string deviceId` | Input label; the parameter ID defaults to `deviceId`. |
| `FlowNode.Id` / `NodeParam.Id` | Stable IDs stored in flow definitions; usually generated. |

Published flows rely on these IDs. Changing display text is safer than changing IDs. When renaming a parameter, retain its old ID or use `Aliases` to accept it.

## Action and waiting nodes

`NodeType.Action` is the default: invoke the method when execution reaches it. The method can return a value or `Task<T>`.

`NodeType.Flipflop` waits for an external trigger, such as a queue message. Return `Task` or `Task<T>` and pass `IFlowContext.CancellationToken` to the wait so cancellation can end it promptly:

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

[FlowLibrary("Device messages")]
public sealed class DeviceNodes(IMessageService messages)
{
    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "Wait for device message")]
    public async Task<string> WaitForMessage(IFlowContext context)
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

`IFlowContext` is a **method parameter**, not a constructor dependency. It exposes `RunId`, `NodeId`, `ExecutionId`, and a cancellation token. Use `SelectSuccess()`, `SelectFailure(code, message)`, or `SelectError(code, message)` to select a branch.

## Complex return values

SereinFlow passes the C# result to downstream nodes in the current run, and also writes a public result to events and API outputs. The public result must be JSON-compatible. Large byte arrays, OpenCV `Mat`, and device connections should be handled separately:

| Destination | Value |
| --- | --- |
| Downstream data connection | Original C# object in this Worker run. |
| Events, debug result, API `outputs` | Small JSON-compatible summary. |
| Downloadable file | Save with `IFlowWorkpiece.UploadNodeOutput`. |

`[NodeResult<ConverterType>]` changes only the **public result**, not the object delivered downstream. It does not save files automatically.

### Example: pass a report downstream and expose a summary

```csharp
using System.Text;
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

public sealed record ReportFile(string FileName, byte[] Content);

public sealed class ReportSummaryConverter : INodeResultConverter<ReportFile, object>
{
    public object Transfer(ReportFile report) => new
    {
        name = report.FileName,
        bytes = report.Content.Length
    };
}

[FlowLibrary("Report tools")]
public sealed class ReportNodes(IFlowWorkpiece workpieces)
{
    [FlowNode(AnotherName = "Create report")]
    [NodeResult<ReportSummaryConverter>]
    public ReportFile Create(IFlowContext context)
    {
        var report = new ReportFile(
            "report.txt", Encoding.UTF8.GetBytes("Inspection complete"));

        workpieces.UploadNodeOutput(
            context, report.FileName, report.Content,
            FlowWorkpieceContentTypes.Text);

        return report;
    }

    [FlowNode(AnotherName = "Read report size")]
    public int GetSize([NodeParam(Name = "Report")] ReportFile report)
        => report.Content.Length;
}
```

Connect **Create report** to the **Report** input of **Read report size**. `GetSize` receives the full `ReportFile`, while `outputs.result` in events and `GET /api/runs/{runId}/outputs` holds a summary such as `{"name":"report.txt","bytes":19}`. Download the full content from [workpieces](/en/api/runs). The original object only exists during this Worker run.

### Converter checklist

- `TPrimitive` in `INodeResultConverter<TPrimitive, TTransfer>` must accept the actual node result.
- `TTransfer` should be JSON-friendly: numbers, strings, arrays, or small plain objects. Omit file bytes, handles, and cycles.
- The converter must be a concrete class in the **same uploaded assembly** and implement one definite `INodeResultConverter<,>`. The Worker registers it automatically.
- Conversion happens after the node method completes. Exceptions produce a conversion error visible in run events.

The repository's `SereinFlow.OpenCvLibrary` uses this pattern: pass the original `Mat` downstream, expose a width/height/channel summary, and upload a PNG. See [Native DLL loading](/en/library/native-dll) for its local dependencies.
