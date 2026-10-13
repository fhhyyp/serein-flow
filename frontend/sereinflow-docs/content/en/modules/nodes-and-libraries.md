# Nodes and libraries

Each block in a flow is a **node**. It can run a script or call a method in an uploaded .NET library. Connections determine execution.

## Built-in and uploaded nodes

The built-in catalog currently includes Script and FlowCall nodes. Script runs SereinScript; FlowCall invokes an exposed node on another canvas.

See [Use SereinScript](/en/script/usage) for editing, syntax, input/output behavior, and built-in modules. Start with [SereinScript usage and development](/en/script/) to extend execution or add C# modules.

For device connections or business logic, build a .NET node library and upload it as a ZIP. Then reference the library from a project so its nodes appear in that project's catalog.

## Read and write flow variables in scripts

Import `variables` to share the current run's table with C# library nodes,
listener triggers and FlowCall frames:

```sereinlang
import { variables } from "sereinflow"
variables.set("batch.count", 42)
if variables.contain("batch.count") {
    return variables.get("batch.count")
}
return null
```

`get(name)` returns null when absent or null; `contain(name)` distinguishes the
two. `set(name, value)` creates or replaces a variable. Names must be non-empty,
non-whitespace, case-sensitive strings. Here `42` is stored as an `int`, readable
in C# with `Get<int>("batch.count")`.

Arrays and objects are copied into CLR containers; call `set` to save script
mutations. CLR handles retain identity. Functions and method values cannot be
stored. Collection conversion is bounded to 32 nesting levels and 10,000 items
per container. Each table operation is locked; multiple `get`/`set` calls do not
form an atomic update. Inspect values in **Flow variables** during a run and in
its saved final snapshot afterward. See
[Built-in services](/en/library/built-in-services#share-flow-variables) and
[Runs API](/en/api/runs#flow-variables) for sharing rules and routes.

## Uploading and referencing are separate

1. `POST /api/libraries/upload` registers the library package.
2. `PUT /api/projects/{projectId}/libraries/{libraryId}` adds it to a project.

A library can serve multiple projects while each project only sees the nodes it uses. New library versions can be previewed before an upgrade is applied.

## Reading results

A value passed between nodes may differ from the output exposed by the API. Read `outputs` for externally visible node results and `workpieces` for generated files.

To write nodes, start with [Your first node](/en/library/first-node). See [Service registration](/en/library/services) for shared objects, [Native DLL loading](/en/library/native-dll) for local dependencies, and [Libraries API](/en/api/libraries) for upload endpoints.
