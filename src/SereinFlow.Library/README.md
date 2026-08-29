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

Leave `Id` unset for ordinary nodes and parameters. SereinFlow derives a node
ID from the library name (or declaring class name) and method name, and derives
a parameter ID from its CLR parameter name. Do not define local legacy
`DynamicFlow` or `NodeAction` attributes.
