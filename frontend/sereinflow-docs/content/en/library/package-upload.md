# Package, upload, and troubleshoot

A C# method appears in a project's node catalog only after its library is packaged as a ZIP, uploaded, and referenced by that project.

## ZIP contents

Like the repository's `SereinFlow.TestLibrary` packaging target, publish into a `LibraryName-Version` directory and ZIP it. Keep DLLs, dependencies, `.deps.json`, and `runtimes/.../native` paths:

```text
MyFlowNodes-1.0.0.zip
└─ MyFlowNodes-1.0.0/
   ├─ MyFlowNodes.dll
   ├─ MyFlowNodes.deps.json
   ├─ other runtime dependencies...
   └─ runtimes/.../native/...
```

The ZIP name and top-level directory must both be `LibraryName-Version`; the main assembly must be `LibraryName.dll` immediately inside it. Include published runtime dependencies, not just `.cs` source. Avoid stale files from earlier builds.

## Use it in the console

1. Publish and create the ZIP.
2. Upload it in the console, or use `POST /api/libraries/upload` with a form file field named `file`.
3. Add the uploaded library to the target project. Uploading and referencing are separate; one library can serve multiple projects.
4. Add its node on the canvas, fill inputs, save, and run.

See [Libraries REST API](/en/api/libraries) for automation.

## If a node is missing

| Symptom | Check |
| --- | --- |
| Upload succeeded, node absent from project | Project reference; `[FlowLibrary]` class and `[FlowNode]` method in the main assembly. |
| Node appears, runtime dependency error | Published dependencies and native directory layout. |
| Old flow no longer reads parameters | Stable node/parameter IDs; use `NodeParam.Aliases` for an intentional rename. |
| Service cannot be injected | Same-assembly `[FlowService]` registration or a Worker-provided service. |

For a library update, inspect the project's [upgrade preview](/en/api/libraries) before applying it.
