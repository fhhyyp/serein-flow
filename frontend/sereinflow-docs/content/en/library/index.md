# Node libraries

Canvas script nodes are useful for experiments. For reusable logic, device calls, or existing .NET code, write a **node library**: a regular C# class library whose public methods become canvas nodes.

```text
Write C# library → Publish and ZIP → Upload → Add to project → Use on canvas
```

## Where to start

1. [Your first node](/en/library/first-node): build, upload, and reference a calculation method.
2. [Node contracts](/en/library/node-contracts): labels, parameters, and execution behavior.
3. [Register services](/en/library/services): share an object among nodes.
4. [Built-in services](/en/library/built-in-services): messages, files, and run context.
5. [Load native DLLs](/en/library/native-dll): use platform-specific dependencies.
6. [Package and upload](/en/library/package-upload): ZIP layout and troubleshooting.

## Three places to keep distinct

| Place | Responsibility |
| --- | --- |
| Your node library | `[FlowLibrary]`, `[FlowNode]`, `[FlowService]`, and bundled dependencies. |
| SereinFlow host | Stores projects, exposes HTTP endpoints, and starts Workers. See [Integration](/en/integration/). |
| Running Worker | Creates node instances and provides messages, file output, and native loading for a run. |

A node constructor can receive Worker-provided services and services registered with `[FlowService]` in the **same uploaded assembly**. Host services are not automatically available inside nodes.
