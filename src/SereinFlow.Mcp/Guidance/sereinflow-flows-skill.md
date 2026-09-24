# SereinFlow Flows

Before editing, call `sereinflow_get_flow_edit_model`. Use its canvas bounds,
node positions, dimensions, ports, parameters, connections and current version
as the edit contract.

Flow changes start with `sereinflow_preview_flow_patch` using schema version
`2.0`, camelCase enum values and typed operations. New requests must use the
Schema 2.0 `op`/named-payload form. The service retains legacy v1 input only
to read existing callers and persisted previews; AI clients must not generate
v1 requests. Allowed operations are
`addCanvas`, `updateCanvas`, `removeCanvas`, `addNode`, `replaceNode`,
`removeNode`, `setNodeParameter`, `addNodeParameter`, `removeNodeParameter`,
`addConnection`, `replaceConnection`, `removeConnection`, `setEntryNode`,
`setRunPolicy` and `replaceScriptSource`.
Remove connections before removing parameters or nodes, and handle canvas
contents before removing a canvas. `addNodeParameter` appends one complete
parameter contract; use it for
an additional variadic member with a unique `ui.id` in the existing
`variadicGroupId`. `removeNodeParameter` requires its incoming data connections
to be removed first. The operation is applied through the same preview/apply
flow-version workflow and can be combined with `addConnection` in one ordered
patch after the parameter has been added.

When `removeNode` removes the current entry node, the patch service clears
`entryNodeId` if that ID is absent from the final canvas contents. This is
intentional editor-draft behavior: removing the last node produces an empty
draft that can be saved with `entryNodeId: ""`. If the same patch creates or
retains a node with that ID, the entry reference is preserved. To choose a
different entry node in the same patch, remove its connections and node first,
then issue `setEntryNode` with the ID of another remaining node.

### Connection payload contract

For schema `2.0`, `addConnection` and `replaceConnection` require every field
in the connection object, including `branch` and `dataSource`; both fields are
nullable. `kind` and `dataSource` are different enums: `execution` is a
`kind`, never a `dataSource`.

| Connection | `kind` | `branch` | `dataSource` | `toPortId` |
| --- | --- | --- | --- | --- |
| Execution | `execution` | `success`, `failure` or `error` | `null` | An execution port, normally `exec-in` |
| Data | `data` | `null` | `previousNode` | The target parameter ID, not its `param-*` UI port ID |

Example execution connection:

```json
{
  "id": "exec-1",
  "fromNodeId": "source-node",
  "fromPortId": "exec-success",
  "toNodeId": "target-node",
  "toPortId": "exec-in",
  "kind": "execution",
  "branch": "success",
  "dataSource": null,
  "priority": 0
}
```

Example data connection:

```json
{
  "id": "data-1",
  "fromNodeId": "source-node",
  "fromPortId": "data-out",
  "toNodeId": "target-node",
  "toPortId": "image",
  "kind": "data",
  "branch": null,
  "dataSource": "previousNode",
  "priority": 0
}
```

Data connections are persisted as `previousNode` bindings. Adding or replacing
a data connection synchronizes the target parameter's `source`,
`sourceNodeId`, and `sourcePortId`; removing that connection restores the
parameter's literal fallback when it still points at that connection. The
connection payload should still declare `dataSource: "previousNode"` for clear
readback and diagnostics.

New nodes, canvases and connections need stable IDs. For an attached library,
use the canonical node template returned by
`sereinflow_create_library_node_template`; do not rebuild its ports, parameter
IDs or runtime metadata.

For built-in `Script` and `FlowCall` nodes, use the read-only
`sereinflow_create_builtin_node_template` after reading the current edit model.
Pass the selected `builtinNodeId` from the model's built-in-node catalog and a
finite canvas `position`. Put the returned `node` unchanged into an `addNode`
or `replaceNode` operation; it is a complete Schema 2.0 node and already
contains execution ports, data-output metadata, parameters and runtime UI
metadata. Do not reconstruct a FlowCall node from its display name or copy a
stale template.

FlowCall runtime metadata is part of the Schema 2.0 contract. Preserve
`returnType`, `targetFlowId`, `targetNodeId`, `targetCanvasId`, `isPublic` and
`flowCallParameterBindings` when a FlowCall node is edited. A missing binding
list is represented by `null` when the target has no parameters. For library
nodes, `libraryNodeContractId` is the pure node `contractId` returned by the
library/edit model; do not send the legacy `flowLibraryNodeContractId` field
or combine a library ID with the node contract ID.

Keep every node within the edit-model canvas and avoid overlapping existing
nodes. Move only affected existing nodes when layout changes are required.
For visual organization preferences, also read
`sereinflow://ai/skills/sereinflow/ui-ux`.

Treat `schemaVersion`, `enumEncoding`, `normalizedOperations` and
`normalizationWarnings` in the preview as the contract. A field,
discriminator, enum, ID or reference diagnostic is a hard stop. Report its
stable `mcp.flow_patch.*` code, `diagnosticId`, `fieldPath`, expected value and
remediation.

When adding a library node with a required input that has no literal default,
`addNode` by itself is expected to fail validation with
`node.missing_required_parameter`. Add the node and its required data
connection(s) in the same ordered patch; the connection operation then binds
the target parameter before final validation. Apply only after the combined
preview reports `canApply: true`.

The Apply response intentionally redacts parameter literals and script source.
After a successful Apply, reread the authoritative flow with
`includeFlowLiteralValues: true` when the exact parameter values are needed.
`updateCanvas` replaces the complete canvas object, so a canvas rename must
preserve its existing nodes and connections in the payload.
