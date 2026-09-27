# Nodes and libraries

Each block in a flow is a **node**. It can run a script or call a method in an uploaded .NET library. Connections determine execution.

## Built-in and uploaded nodes

The built-in catalog currently includes Script and FlowCall nodes. Script runs SereinScript; FlowCall invokes an exposed node on another canvas.

For device connections or business logic, build a .NET node library and upload it as a ZIP. Then reference the library from a project so its nodes appear in that project's catalog.

## Uploading and referencing are separate

1. `POST /api/libraries/upload` registers the library package.
2. `PUT /api/projects/{projectId}/libraries/{libraryId}` adds it to a project.

A library can serve multiple projects while each project only sees the nodes it uses. New library versions can be previewed before an upgrade is applied.

## Reading results

A value passed between nodes may differ from the output exposed by the API. Read `outputs` for externally visible node results and `workpieces` for generated files.

To write nodes, start with [Your first node](/en/library/first-node). See [Service registration](/en/library/services) for shared objects, [Native DLL loading](/en/library/native-dll) for local dependencies, and [Libraries API](/en/api/libraries) for upload endpoints.
