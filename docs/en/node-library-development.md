# SereinFlow Node Library Development Guide

This guide describes how to build, package, upload, execute, and upgrade a
SereinFlow node library. It is based on these projects in this repository:

- [`src/SereinFlow.Library/SereinFlow.Library.csproj`](../../src/SereinFlow.Library/SereinFlow.Library.csproj), the standalone node-library SDK;
- [`tests/SereinFlow.TestLibrary/SereinFlow.TestLibrary.csproj`](../../tests/SereinFlow.TestLibrary/SereinFlow.TestLibrary.csproj), an example/test library covering ordinary nodes, async triggers, input conversion, branches, enums, messaging, and constructor injection;
- [`src/SereinFlow.OpenCvLibrary/SereinFlow.OpenCvLibrary.csproj`](../../src/SereinFlow.OpenCvLibrary/SereinFlow.OpenCvLibrary.csproj), an image-processing library covering OpenCvSharp, native runtime assets, non-JSON result conversion, and run workpieces.

## 1. The model

A node library is neither an API project nor a flow-engine project. It should
depend on `SereinFlow.Library` and describe its public nodes through stable
contracts. The API reads those contracts when a package is uploaded; the Worker
loads and executes the actual assembly only when a flow runs.

```text
Library source
    │  FlowLibrary / FlowNode / NodeParam metadata
    ▼
ZIP package ──► API scans nodes, parameters, enums, and a compatibility manifest
    │
    ▼
Project library reference ──► node templates and persisted flow contracts
    │
    ▼
Worker run ──► extract, isolate-load, inject restricted SDK services, invoke methods
    │
    ├─ primitive result: retained for downstream nodes in the Worker
    └─ TransferOutputs: used by events, debug views, API, and MCP projections
```

The upload scanner reads PE metadata without executing or loading the uploaded
assembly into the API process. At execution time the Worker uses a collectible
`AssemblyLoadContext`. When the run ends, its assemblies, native loader, message
broker, and run-scoped workpiece service are released.

## 2. Project setup and dependencies

A production library can reference the published SDK package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SereinFlow.Library" Version="1.1.0" />
  </ItemGroup>
</Project>
```

The repository's `TestLibrary` and `OpenCvLibrary` use a project reference for
local integration. Use these SDK namespaces:

```csharp
using SereinFlow.Core.Api;             // FlowLibrary, FlowNode, NodeParam, NodeResult, ...
using SereinFlow.Library;              // messaging, workpieces, native loading, lifetimes
using SereinFlow.Runtime.Abstractions; // IFlowContext (defined by the SDK assembly)
```

`SereinFlow.Contracts` remains a type-forwarding compatibility facade for old
binaries. New libraries should reference `SereinFlow.Library` directly and
should not copy legacy `DynamicFlow`, `NodeAction`, or same-named attributes.

## 3. Library containers, node metadata, and stable identity

### 3.1 `FlowLibraryAttribute`

Mark a class as a node container:

```csharp
[FlowLibrary("Production line quality library")]
public sealed class ProductionNodes
{
    // [FlowNode] methods
}
```

- The attribute applies to classes and accepts an explicit library display name.
- With no name, the CLR class name is used, such as `UnnamedLibraryNodes`.
- The library name participates in derived node contract IDs.
- A single assembly may contain multiple `[FlowLibrary]` classes.

### 3.2 `FlowNodeAttribute`

```csharp
[FlowNode(
    Id = "quality.pass-rate",
    NodeType = NodeType.Action,
    AnotherName = "Calculate pass rate",
    Desc = "Calculates the batch pass rate.")]
public decimal Calculate(int passCount, int totalCount) => ...;
```

| Property | Purpose |
| --- | --- |
| `Id` | Stable node contract ID. Set it explicitly for public nodes; it must be unique within the package. |
| `NodeType` | `Action` or `Flipflop`; the default is `Action`. These are the library-node types supported by the SDK. |
| `AnotherName` | User-facing name used by the catalog, editor, and node template. |
| `Desc` | User-facing description. |

When `Id` is omitted, the scanner derives a contract ID from the library display
name and CLR method name, for example:

```text
Production line quality library.Calculate
```

Derived IDs are useful for examples, but a published library should use explicit
IDs that do not change when a class or display name is renamed. Duplicate node
contract IDs reject the upload. Avoid marking same-name overloads as nodes;
distinct method names are easier to resolve and upgrade.

### 3.3 `NodeParamAttribute`

```csharp
public string BuildCommand(
    [NodeParam(Id = "device-id", Name = "Device ID")] string deviceId,
    [NodeParam(Name = "Decimal places")] int digits = 2,
    [NodeParam(Name = "Unit")] string unit = "mm") => ...;
```

| Property | Purpose |
| --- | --- |
| `Id` | Stable parameter contract ID; defaults to the CLR parameter name. |
| `Aliases` | Previous IDs accepted after an intentional parameter rename. |
| `Name` | User-facing parameter name. |
| `IsExplicit` | Whether the input must be explicitly supplied; the default is `true`. |

Whether a parameter can actually be omitted also depends on its CLR default value
or nullability. Usually pair `IsExplicit = false` with a CLR default:

```csharp
[NodeParam(Name = "Image width", IsExplicit = false)] int width = 640
```

The scanner also reads CLR default metadata for numeric, Boolean, string,
`decimal`, `DateTime`, and enum parameters.

## 4. Supported method signatures and execution

### 4.1 Inputs

Ordinary parameters become node inputs. `IFlowContext` is special: it is a hidden
parameter, omitted from the catalog, editor, and input audit, and injected by the
Worker:

```csharp
[FlowNode]
public string Describe(
    [NodeParam(Name = "Device state")] string state,
    IFlowContext context)
{
    return state;
}
```

`IFlowContext` exposes restricted node context, not arbitrary flow data,
environment variables, secrets, API repositories, or `IServiceProvider`.

### 4.2 Return values

- A synchronous return value becomes a `result` data output; `void` produces no data output.
- `Task` and `Task<T>` are awaited by the Worker; `T` becomes the result.
- A `Flipflop` must return `Task` or `Task<T>`; upload scanning rejects other return types.
- The current library executor recognizes `Task`/`Task<T>`; do not use `ValueTask` as a library-node awaitable contract.
- When a node has one output, the engine also writes a generic `data-out` value for convenient downstream connections.

`TestLibrary` demonstrates `Task<T>` with `等待设备触发` and `监听设备配置变更`,
and a no-output `void` node with `记录工序结果`.

### 4.3 `Action` and `Flipflop`

An `Action` runs once when reached. A `Flipflop` is an asynchronous wait/trigger
node:

- with an execution input, it waits once when reached and then follows the result branch;
- without an execution input, the engine treats it as a global listener, schedules downstream work after each completion, and waits again;
- waits, subscriptions, and polling should use `IFlowContext.CancellationToken` so run cancellation and Worker shutdown can finish promptly.

### 4.4 Static and instance methods

A dependency-free node may be static. Use an instance container when the node
needs constructor injection, `IDisposable`, or encapsulated state. The Worker
creates a fresh node-container instance for each invocation and disposes
`IDisposable`/`IAsyncDisposable` instances after invocation; do not use node fields
as a cross-invocation cache.

## 5. Input conversion, variadic parameters, and enums

Flow inputs can come from literals, project inputs, expressions, or previous-node
data. The Worker converts them to the CLR type declared by the method. Common
supported forms include:

- `string`, Boolean, integral and floating-point types, `decimal`, `Guid`, and `char`;
- nullable value types;
- ordinary enums and `[Flags]` enums;
- arrays;
- types with a suitable `TypeConverter`;
- DTOs or records that can be deserialized by `System.Text.Json`;
- objects already of the target type inside the same Worker, such as an OpenCV `Mat`.

Conversion failure produces `node.input_invalid`; a missing non-nullable value
type without a default produces `node.input_missing`. The node should still
validate domain ranges, as `TestLibrary` does for a non-zero total count.

### 5.1 `params` inputs

```csharp
[FlowNode(AnotherName = "Sum inspection values")]
public int Sum([NodeParam(Name = "Value")] params int[] values)
    => values.Sum();
```

The catalog records `params int[]` as a variadic input with element type
`System.Int32`. The runtime supports two editor/input shapes:

1. `expanded`: multiple named `Value` inputs; empty optional placeholders are skipped;
2. `collection`: one array/collection input such as `[2, 3, 5]`.

This behavior is built into the runtime and needs no extra SDK attribute.

### 5.2 Ordinary and flags enums

Use an enum directly as a parameter:

```csharp
public string SetMode(
    [NodeParam(Name = "Mode")] DeviceMode mode = DeviceMode.Automatic) => ...;

[Flags]
public enum DevicePermission : ulong
{
    None = 0,
    ReadStatus = 1,
    WriteParameter = 2,
    Diagnose = 4,
    All = 7
}
```

The scanner reads enum member names, underlying type, numeric values, and the
`[Flags]` marker from PE metadata without loading user code. The editor can
therefore create an ordinary enum dropdown or a flags multi-select. Unsigned
values are stored as strings to avoid JSON/JavaScript precision loss.
`TestLibrary` covers `设备运行模式`, `设备操作权限`, `生成设备配置摘要`, and
`监听设备配置变更`.

## 6. Branch control with `IFlowContext`

Nodes take the `Success` branch by default. Select an execution branch for a
business decision through the restricted context:

```csharp
[FlowNode(AnotherName = "Choose by device state")]
public string Choose([NodeParam(Name = "Device state")] string state, IFlowContext context)
{
    if (state == "faulted")
    {
        context.SelectError("device.faulted", "The device is faulted.");
        return state;
    }

    if (state != "ready")
        context.SelectFailure("device.not_ready", "The device is not ready.");

    return state;
}
```

Available operations:

- `SelectSuccess()` selects success and clears branch code/message;
- `SelectFailure(code, message)` selects failure; the default code is `node.branch_failure`;
- `SelectError(code, message)` selects error; the default code is `node.branch_error`.

Branch selection changes which execution connection the flow engine follows; it
does not throw. An unhandled exception normally produces an error result. Use an
explicit `Failure` for expected business outcomes and provide a stable error code.

## 7. Non-JSON results with `NodeResult`

Camera handles, OpenCV `Mat` values, serial-port handles, or HTTP objects should
not be serialized directly into API JSON. Declare a result converter:

```csharp
public sealed class MatSummaryConverter : INodeResultConverter<Mat, object>
{
    public object Transfer(Mat value) => new
    {
        type = "opencv-mat",
        width = value.Width,
        height = value.Height,
        channels = value.Channels()
    };
}

[FlowNode(AnotherName = "Read image")]
[NodeResult<MatSummaryConverter>]
public Mat ReadImage(byte[] imageBytes) => ...;
```

The non-generic form is also available:

```csharp
[NodeResultAttribute(typeof(MatSummaryConverter))]
```

Execution rules:

- `INodeResultConverter<TPrimitive, TTransfer>` must be a concrete, non-generic class in the library assembly and implement exactly one closed converter contract;
- the Worker discovers it and registers it as transient;
- `TPrimitive` must match the node's actual return value;
- `TTransfer` should be JSON-safe;
- the original `TPrimitive` remains available to downstream nodes in the current Worker, while events, debug output, API, and MCP use `TTransfer`;
- use a workpiece for binary data instead of putting it in `TTransfer`.

The OpenCV library's `MatConverter` emits dimensions, channels, depth, and the
empty flag; it never serializes the Mat buffer into an event.

## 8. Images and files with `IFlowWorkpiece`

`IFlowWorkpiece` stores non-JSON data for the current run:

```csharp
public sealed class ImageNodes(IFlowWorkpiece workpiece)
{
    public FlowWorkpieceInfo Save(byte[] png)
        => workpiece.UploadImage("inspection.png", png, FlowWorkpieceContentTypes.Png);

    public FlowWorkpieceInfo SaveReport(Stream report)
        => workpiece.UploadFile("report.json", report, FlowWorkpieceContentTypes.Json);

    public FlowWorkpieceInfo SaveStep(IFlowContext context, byte[] content)
        => workpiece.UploadNodeOutput(
            context, "result.json", content, FlowWorkpieceContentTypes.Json);
}
```

The SDK provides `FlowWorkpieceContentTypes.Png` and
`FlowWorkpieceContentTypes.Json` as compile-time constants, so node libraries
do not need to repeat the `"image/png"` and `"application/json"` literals.

Available operations:

- `UploadImage`: byte array, stream, or Base64/data URI;
- `UploadFile`: byte array or stream;
- `UploadNodeOutput`: byte array or stream, with the node ID and execution-step ID
  from `IFlowContext` stored in metadata;
- `FlowWorkpieceInfo`: stable ID, kind (`Image`/`File`), name, MIME type, length,
  creation time, node ID, and execution-step ID.

Workpieces belong to the current flow run and can be listed/downloaded through
the API and MCP. `UploadNodeOutput` lets the debugger select the first artifact
from the exact chosen execution step while retaining every workpiece from the run.
The caller retains ownership of supplied streams; the SDK does not close them.
Pass a single file name, not a path.

Each OpenCV node encodes its output as PNG, calls `UploadNodeOutput(context,
...)`, and returns the original `Mat`, providing both a visual artifact and an
in-process data connection. The workpiece records the execution step ID automatically.

## 9. Constructor injection and service lifetimes

### 9.1 SDK services available to nodes

The Worker pre-registers these services in the restricted container for each
uploaded library:

- `IMessageService`, the run-level queue/event-bus service;
- `IFlowWorkpiece`, the run-level workpiece service;
- `IFlowNativeLibraryLoader`, the loader for the current library package.

```csharp
[FlowLibrary("Device library")]
public sealed class DeviceNodes(
    IMessageService messages,
    IFlowWorkpiece workpiece)
{
    [FlowNode(Id = "device.read")]
    public string Read() => "ok";
}
```

### 9.2 Library-owned services with `FlowService`

Only concrete, closed classes from the same uploaded assembly can be registered:

```csharp
public interface IDeviceChannel
{
    string Read();
}

[FlowService<IDeviceChannel>]
public sealed class DeviceChannel : IDeviceChannel
{
    public string Read() => "ok";
}

[FlowLibrary("Device library")]
public sealed class DeviceNodes(IDeviceChannel channel)
{
    [FlowNode(Id = "device.read")]
    public string Read() => channel.Read();
}
```

Open generic types, abstract classes, duplicate contracts, and conflicting
registrations are rejected. A service implementation must have exactly one
public constructor, and its implementation must satisfy its exposed interface or
base-class contract. Older C# versions can use
`[FlowService(typeof(IDeviceChannel))]`.

| Lifetime | Semantics |
| --- | --- |
| `Run` (default) | One instance is shared by the assembly throughout one Worker run. |
| `Invocation` | One instance is shared within one node invocation; the next invocation gets a new instance. |
| `Transient` | A new instance is created for each resolution. |

When a service exposes an interface, its concrete type and interface resolve to
the same instance for `Run` and `Invocation`. Service containers are isolated per
uploaded assembly, so one library cannot resolve another library's `FlowService`.
The node container itself is still created per invocation.

The following are explicitly forbidden as constructor dependencies:
`IServiceProvider`, `IServiceScopeFactory`, `IServiceProviderIsService`,
`IKeyedServiceProvider`, `IFlowContext`, API repositories, configuration,
secrets, and Worker protocol objects. Put `IFlowContext` on the node method
instead.

`TestLibrary/ConstructorInjectionNodes.cs` verifies `Run`, `Invocation`, and
`Transient` behavior. `ForbiddenProviderNode` verifies that injecting
`IServiceProvider` fails with `library.service_dependency_forbidden`.

## 10. Run-local messaging: queues, event bus, and external ingress

Create message views through the injected `IMessageService`:

```csharp
public sealed class MessageNodes(IMessageService messages)
{
    [FlowNode(
        Id = "device.message.receive",
        NodeType = NodeType.Flipflop,
        AnotherName = "Receive device message")]
    public async Task<string> Receive(IFlowContext context)
    {
        var queue = messages.CreateMessageQueue(new MessageChannelOptions
        {
            SerializationMode = MessageSerializationMode.Json,
            Capacity = 8,
            ExternalIngress = true,
            ContractId = "device.text.v1"
        });

        return await queue.ReceiveAsync<string>(
            "device.inbox", context.CancellationToken);
    }
}
```

The two models are:

- `IMessageQueue`: bounded topic-based FIFO competing-consumer queue; each message is delivered to one consumer;
- `IEventBus`: broadcast model with an independent buffer per subscriber; subscribers present at publish time each receive one copy, while late subscribers receive no history.

`MessageChannelOptions` supports:

- `SerializationMode`: `Json` (default, recommended for cross-library work) or `DirectObject` (compatible CLR objects inside one Worker);
- `Capacity`: per-topic/per-subscription buffer capacity;
- `OverflowStrategy`: `Reject`, `DropOldest`, or `DropNewest`;
- `MessageTtl`: message lifetime;
- `MaxPayloadBytes`: JSON payload limit;
- `ExternalIngress`: whether Supervisor/API delivery is allowed, disabled by default;
- `ContractId`: a server-controlled contract ID required for external ingress.

Every view of the same topic must use compatible options. An externally exposed
endpoint must explicitly set `ExternalIngress = true` and use JSON. External
HTTP/MCP delivery accepts JSON only and cannot select an assembly-qualified type.
Pass cancellation tokens to every send, receive, and subscription wait, and use
`await using` for event subscriptions.

`TestLibrary/MessageNodes` demonstrates an external JSON queue endpoint,
an external JSON EventBus endpoint, and a successor Action that consumes the
Flipflop's `data-out` value.

The message service exists only in the current Worker run. It does not provide a
cross-run broker, persistence/replay, consumer acknowledgements, or recovery
after a run ends. HTTP `202 Accepted` means only that the Worker Broker accepted
the message; observe run events, outputs, and final state to determine whether
downstream work completed. See [Worker Message Service](worker-message-service.md)
for the HTTP/MCP delivery contract.

## 11. Native dependencies and the OpenCV library

### 11.1 Declaring a native directory

Fixed dependencies can be declared at assembly or node-class level:

```csharp
using SereinFlow.Core.Api;

[assembly: NativeLibraryDirectory(
    "runtimes/{rid}/native",
    Recursive = false,
    Required = true)]
```

`{rid}` is replaced by the current runtime identifier, such as `win-x64`.
`Recursive` and `Required` default to `true`. If a required directory is
missing, empty, or contains a load failure, the Worker reports a
`FlowNativeLibraryException` with a machine-readable code and relative path.

For conditional loading, inject `IFlowNativeLibraryLoader`:

```csharp
public DeviceNodes(IFlowNativeLibraryLoader native)
{
    native.LoadNativeLibraryDirectory(
        "vendor/native/{rid}", recursive: true, required: true);
}
```

`LoadNativeLibrary("vendor/native/{rid}/device.dll")` is also available. Paths
must be relative to the package and cannot contain `..`, drive segments, or an
escape from the package root. Loading is run-scoped and idempotent. Do not load
multiple architecture directories together.

### 11.2 OpenCV example

`SereinFlow.OpenCvLibrary` declares:

```xml
<ProjectReference Include="..\SereinFlow.Library\SereinFlow.Library.csproj" />
<PackageReference Include="OpenCvSharp4" Version="4.11.0.20250507" />
<PackageReference Include="OpenCvSharp4.runtime.win"
                  Version="4.11.0.20250507"
                  PrivateAssets="all" />
```

Its `NativeLibraryManifest.cs` declares `runtimes/{rid}/native` as a required,
non-recursive directory. The Worker processes assembly-level declarations when
the assembly is loaded and class-level declarations when a node type is
resolved. The matching `OpenCvSharpExtern` shim is loaded before the first node
instance; x86 and x64 directories are not loaded together.

The current OpenCV nodes cover sample-image generation, encoded-image decoding,
grayscale conversion, thresholding, dilation, erosion, opening, closing,
Gaussian blur, Canny edge detection, resizing, and inversion. Every node returns
`Mat`, uses `MatConverter` for a JSON summary, and uploads a PNG through
`IFlowWorkpiece.UploadNodeOutput`. Downstream OpenCV nodes receive the original
`Mat`, not the summary object.

## 12. How the flow engine uses the contracts

### 12.1 Upload and catalog

The API reads the library-matching DLL, such as
`SereinFlow.TestLibrary.dll`, and scans its PE metadata for:

- `[FlowLibrary]` classes and names;
- `[FlowNode]` types, IDs, names, descriptions, and return types;
- `[NodeParam]` IDs, names, aliases, and explicit-input flags;
- `params` metadata and element types;
- CLR defaults;
- ordinary and `[Flags]` enum metadata;
- the presence of a `NodeResult` converter;
- an immutable compatibility manifest and artifact hashes.

Catalog data is used for project node templates and editors without executing
library code in the API process. Managed dependencies referenced by the library
must still be included in the package so the Worker dependency resolver can load
them.

### 12.2 Templates and saved flows

After a project references an artifact, the editor/MCP can create a node template
from a published contract. The saved runtime metadata includes the artifact ID,
version, hash, node contract ID, type, method, and parameter contracts. Data
connections can bind literals, project inputs, expressions, or previous-node
outputs to those parameters.

Only library `Action` and `Flipflop` contracts create library-node templates.
`Script` and `FlowCall` are engine node types and are not produced by
`SereinFlow.Library` attributes.

### 12.3 Worker invocation

At run time the Worker:

1. checks that each referenced artifact is allowed for the run;
2. reads `{libraryId}.zip` from the controlled package root and extracts it to a run-specific directory;
3. loads the main assembly through a collectible load context and dependency resolver;
4. initializes declared native directories, run-level messaging, run-level workpieces, and the library-isolated DI container;
5. creates an invocation scope and node instance for each call;
6. injects `IFlowContext`, converts ordinary inputs, and invokes the synchronous/asynchronous method;
7. retains the primitive output and, when declared, produces a transfer output;
8. follows `Success`, `Failure`, or `Error` connections and disposes the node instance and scope.

The main assembly, package ID, type, and method must resolve uniquely. Same-name
overloads can produce `library.method_ambiguous`; avoid publishing ambiguous
overloads as nodes.

## 13. Packaging, upload, and dependency layout

### 13.1 ZIP naming and layout

The upload filename must be:

```text
{library-name}-{major.minor.patch}[prerelease/metadata].zip
```

The package root should match the filename stem:

```text
SereinFlow.OpenCvLibrary-1.0.3/
├── SereinFlow.OpenCvLibrary.dll
├── SereinFlow.Library.dll
├── SereinFlow.OpenCvLibrary.deps.json
├── SereinFlow.OpenCvLibrary.runtimeconfig.json
└── runtimes/
    └── win-x64/native/OpenCvSharpExtern.dll
```

The main DLL must be exactly `{library-name}.dll` under the package root, and its
assembly name must match the library name. Entries must have safe, unique paths.
Packages cannot contain source code, build files, scripts, archives, or
executables. Do not flatten the publish directory: managed dependency resolution
and native paths depend on the relative layout.

`TestLibrary` itself only depends on `SereinFlow.Library`, so its publish package
has no `runtimes` native directory. The native example above is for
`OpenCvLibrary`; a library without native dependencies does not need that folder.

### 13.2 Repository build examples

`TestLibrary` uses an `AfterTargets="Publish"` target to retain the complete
publish layout and produce:

```powershell
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
```

The artifact is `artifacts\libraries\SereinFlow.TestLibrary-1.6.1.zip`.
`OpenCvLibrary` currently uses its `CreatePackage` MSBuild target after Build and
creates `SereinFlow.OpenCvLibrary-1.0.3.zip` in the project directory. It relies
on the OpenCvSharp managed assembly and `runtimes/{rid}/native` assets. Inspect
the resulting ZIP, not only the main DLL in `bin`.

## 14. Compatibility and upgrade guidance

A node contract is defined by its node ID, parameter IDs/aliases, CLR types,
return type, awaitability, and node type. To keep existing flows upgradeable:

- set an explicit stable `FlowNode.Id` for published nodes;
- set an explicit stable `NodeParam.Id` for published parameters;
- retain the old ID in `Aliases` when intentionally renaming a parameter;
- do not change an existing node's `NodeType`, synchronous/`Task` contract, return type, CLR parameter type, or `params` shape;
- add new parameters with CLR defaults when possible; a new required parameter without a default requires rewiring;
- treat removing a node or existing parameter as breaking;
- display names, descriptions, optionality, and defaults are generally compatible metadata changes, but should still be reviewed in an upgrade preview;
- avoid changing the `FlowLibrary` display name or CLR method name unless explicit IDs and the upgrade mapping have been verified.

Uploads create an immutable compatibility manifest. The engine can compare two
artifacts without executing the target assembly. Missing or ambiguous contracts
are handled conservatively and block silent rebinding. A version change alone
does not change a node contract; stable IDs and parameter aliases are the upgrade
mechanism.

## 15. Repository examples as an acceptance checklist

### `SereinFlow.TestLibrary`

Use it to verify:

- numeric, Boolean, string, nullable-string, and structured-record outputs;
- `void`, synchronous results, `Task<T>`, and a global `Flipflop`;
- ordinary inputs, CLR defaults, and `IsExplicit = false`;
- hidden `IFlowContext` injection and Success/Failure/Error branches;
- expanded and collection forms of `params int[]`;
- ordinary enums, `[Flags]` enums, `ulong` underlying values, multi-select, and defaults;
- external JSON Queue/EventBus endpoints and a successor Action;
- `Run`, `Invocation`, and `Transient` `FlowService` lifetimes;
- rejection of forbidden `IServiceProvider` injection.

### `SereinFlow.OpenCvLibrary`

Use it to verify:

- third-party managed dependencies and RID-specific native files;
- an assembly-level `NativeLibraryDirectory` declaration;
- an in-process result such as `Mat` that is not suitable for direct JSON;
- `INodeResultConverter<Mat, object>`;
- `IFlowWorkpiece.UploadNodeOutput`, MIME types, and node-artifact association;
- the original object continuing to downstream nodes while events/API expose a summary.

Recommended verification commands:

```powershell
dotnet build src\SereinFlow.OpenCvLibrary\SereinFlow.OpenCvLibrary.csproj -c Release
dotnet publish tests\SereinFlow.TestLibrary\SereinFlow.TestLibrary.csproj -c Release
dotnet test SereinFlow.sln --no-build
```

## 16. Common failures

| Symptom | Typical cause |
| --- | --- |
| No node appears after upload | The class lacks `[FlowLibrary]`, the method lacks `[FlowNode]`, or a copied same-named attribute was used. |
| Duplicate node contract | Two explicit IDs collide, or two derived IDs use the same library name and method name. |
| Input cannot bind | Parameter names were not preserved and no `NodeParam.Id` was supplied; the flow used the wrong ID/alias; or conversion failed. |
| Flipflop upload failure | The method returns a synchronous type, `ValueTask`, or another non-`Task` type. |
| `library.service_dependency_forbidden` | The constructor requests `IServiceProvider`, `IFlowContext`, or another forbidden host object. |
| `library.result_converter_invalid` | The converter is not a concrete type in the library assembly or does not implement exactly one closed converter contract. |
| Native load failure | The current RID asset was not packaged, the path contains `..`, the declaration is wrong, or multiple architectures were mixed. |
| Debugger cannot select an artifact | `UploadFile` was used instead of `UploadNodeOutput`, or the context was not passed to `UploadNodeOutput`. |
| Message endpoint does not wake | `ExternalIngress` is false, `ContractId` differs, topic/channel kind differs, or the Worker has ended. |
| Downstream receives the summary | `TransferOutputs` was treated as flow data; `NodeResult` changes the external projection, while normal data connections retain the original return value. |
