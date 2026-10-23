# SereinFlow Library Metadata

Use the public SDK types:

```csharp
using SereinFlow.Library;
```

Use `FlowLibraryAttribute`, `FlowNodeAttribute`, `NodeParamAttribute`,
`FlowServiceAttribute`, `FlowServiceLifetime`, `NodeType` and `IFlowContext`.
Do not define or retain local copies of these types or legacy `DynamicFlow` /
`NodeAction` metadata; the current scanner ignores or rejects the legacy form.

All public SDK types use `SereinFlow.Library`. Libraries compiled against the
former SDK namespaces must update their imports and rebuild.

`LibraryVariableService` is a built-in constructor dependency shared by all
libraries and flow contexts in one run; `IFlowContext.Variables` exposes the
same instance, including listener triggers and FlowCall frames. Use `Contain`,
`Get`, `Get<T>`, `TryGet<T>`, and `Set<T>` for locked table access. Missing values
return null or `default(T)`, and `TryGet<T>` returns false only when absent.
Generic reads use CLR casts/unboxing and throw `InvalidCastException` on a type
mismatch, including stored null read as a non-nullable value type. Variable
names are case-sensitive and non-empty. Values are not persisted across runs;
mutations of stored objects and read-modify-write sequences require caller synchronization.

```csharp
[FlowLibrary("Image byte utilities")]
public sealed class ImageByteNodes
{
    [FlowNode(AnotherName = "Echo image bytes", Desc = "Return the supplied bytes unchanged.")]
    public byte[] Echo([NodeParam(Name = "Image bytes")] byte[] bytes)
        => bytes;
}
```

When no language is specified, localize display values to the user's dominant
language: library name, node name/description and parameter name. Keep labels
concise and free of control characters, markup, emoji, mojibake and unexplained
punctuation. Preserve established names and node identity during upgrades
unless a rename is explicitly requested.

Let the SDK and scanner derive ordinary node and parameter IDs from stable CLR
names. Normally omit `NodeParamAttribute.Id` and `Aliases`. Use them together
only for an intentional exposed-parameter rename, with real historical IDs;
never use display names as IDs. `IFlowContext` is injected and is not a user
input. Preserve the project's instance-node convention.

`NodeType.Flipflop` is an asynchronous trigger and must return `Task` or
`Task<T>`. It must await the next event, signal or polling result; wrapping
ordinary immediate work as a trigger is incorrect.

## Constructor injection

Register concrete, closed service classes from the same uploaded library
assembly with `FlowServiceAttribute`. A declaration without a contract exposes
the concrete class. A declaration with an interface or base-class contract
exposes both the contract and concrete class:

```csharp
[FlowService]
public sealed class DeviceClient
{
}

[FlowService(typeof(IDeviceChannel), Lifetime = FlowServiceLifetime.Invocation)]
public sealed class DeviceChannel : IDeviceChannel
{
}

// C# 11 and later:
[FlowService<IDeviceChannel>]
public sealed class GenericDeviceChannel : IDeviceChannel
{
}
```

`FlowServiceLifetime.Run` is the default and shares an instance throughout one
Worker run. `Invocation` shares an instance within one node invocation.
`Transient` creates a new instance for every resolution. For `Run` and
`Invocation`, resolving a contract and its concrete implementation returns the
same instance.

Node and service classes receive registered services through their public
constructors. Service implementations must be concrete, closed classes with
exactly one public constructor. Do not request `IServiceProvider`,
`IServiceScopeFactory`, `IFlowContext`, repositories, configuration, secrets
or Worker protocol objects through constructor injection.
