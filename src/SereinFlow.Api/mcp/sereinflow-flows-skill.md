# SereinFlow Flows

Before editing, call `sereinflow_get_flow_edit_model`. Use its canvas bounds,
node positions, dimensions, ports, parameters, connections and current version
as the edit contract.

Flow changes start with `sereinflow_preview_flow_patch` using schema version
`2.0`, camelCase enum values and typed operations. Allowed operations are
`addCanvas`, `updateCanvas`, `removeCanvas`, `addNode`, `replaceNode`,
`removeNode`, `setNodeParameter`, `addConnection`, `replaceConnection`,
`removeConnection`, `setEntryNode`, `setRunPolicy` and `replaceScriptSource`.
Do not use the legacy `operation` / `value` envelope. Remove connections before
removing nodes and handle canvas contents before removing a canvas.

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
