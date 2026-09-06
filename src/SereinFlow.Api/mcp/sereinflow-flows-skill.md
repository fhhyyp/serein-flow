# SereinFlow Flows

Before editing, call `sereinflow_get_flow_edit_model`. Use its canvas bounds,
node positions, dimensions, ports, parameters, connections and current version
as the edit contract.

Flow changes start with `sereinflow_preview_flow_patch` using schema version
`2.0`, camelCase enum values and typed operations. Allowed operations are
`addCanvas`, `updateCanvas`, `removeCanvas`, `addNode`, `replaceNode`,
`removeNode`, `setNodeParameter`, `addNodeParameter`, `removeNodeParameter`,
`addConnection`, `replaceConnection`, `removeConnection`, `setEntryNode`,
`setRunPolicy` and `replaceScriptSource`.
Do not use the legacy `operation` / `value` envelope. Remove connections before
removing parameters or nodes, and handle canvas contents before removing a
canvas. `addNodeParameter` appends one complete parameter contract; use it for
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

Place every node in a non-overlapping bounding box inside the edit-model
canvas, with clearance from existing nodes and boundaries. Keep the main path
left to right, branch rows separate and connections away from node bodies.
Move only affected existing nodes when layout changes are required.

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
