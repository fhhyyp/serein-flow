# Your first node

Build a node that calculates a pass rate from the number of passing and tested items.

## 1. Create a class library

The current `SereinFlow.Library` targets .NET 10. From the **SereinFlow repository root**:

```powershell
dotnet new classlib -n MyFlowNodes -f net10.0
Set-Location MyFlowNodes
dotnet add reference ..\src\SereinFlow.Library\SereinFlow.Library.csproj
```

Outside this repository you can reference the `SereinFlow.Library` package instead. Match the server version; this repository currently specifies `1.2.0`.

## 2. Add a method

Create `QualityNodes.cs`:

```csharp
using SereinFlow.Core.Api;

namespace MyFlowNodes;

[FlowLibrary("Quality checks")]
public sealed class QualityNodes
{
    [FlowNode(AnotherName = "Calculate pass rate", Desc = "Percentage of passing items")]
    public decimal PassRate(
        [NodeParam(Name = "Passing items")] int passed,
        [NodeParam(Name = "Total items")] int total)
    {
        if (total <= 0)
            throw new ArgumentOutOfRangeException(nameof(total), "Total must be positive");

        return Math.Round((decimal)passed / total * 100m, 2);
    }
}
```

`[FlowLibrary]` marks the class as a node library. `[FlowNode]` exposes `PassRate` in the canvas catalog. `AnotherName`, `Desc`, and `[NodeParam]` set user-visible labels. The returned `decimal` is the node result.

## 3. Publish and use it

Add `<EnableDynamicLoading>true</EnableDynamicLoading>` to the `<PropertyGroup>` in `MyFlowNodes.csproj`, so publication includes dynamic-loading dependencies. From the project directory:

```powershell
dotnet publish .\MyFlowNodes.csproj -c Release -o ..\artifacts\MyFlowNodes-1.0.0
Compress-Archive -Path ..\artifacts\MyFlowNodes-1.0.0 `
  -DestinationPath ..\artifacts\MyFlowNodes-1.0.0.zip -Force
```

The ZIP must contain a top-level `MyFlowNodes-1.0.0` directory with `MyFlowNodes.dll` inside. Upload it in the console, add the library to a project, then place **Calculate pass rate** on the canvas. Enter both numbers, mark it as the entry, save, and run. See [Package and upload](/en/library/package-upload) for structure and troubleshooting.

::: tip Stable IDs
Ordinary nodes can omit `FlowNode.Id` and `NodeParam.Id`; the current API derives them from library, method, and parameter names. If existing flows use a node, plan renames carefully. Explicit IDs and `NodeParam.Aliases` support an intentional migration.
:::
