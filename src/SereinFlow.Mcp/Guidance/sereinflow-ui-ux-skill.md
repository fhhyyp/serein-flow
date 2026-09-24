# SereinFlow Flow UI/UX

Use this module when editing a flow through MCP and choosing node positions or
connection routes. It complements the flow patch contract; the flow module and
the live tool schemas remain authoritative for IDs, ports, payloads and
validation.

Before laying out or relaying out a canvas, read
`sereinflow_get_flow_edit_model` and use its current bounds, node dimensions,
ports, parameters and connections. Do not use fixed pixel dimensions; adapt
the layout to the actual edit model.

## Flow organization

- Keep the main execution path left to right and align nodes that belong to
  the same execution stage.
- Put success, failure and error branches on separate rows and expand branch
  work to the right of its decision point.
- When a long linear path would become excessively wide, split it into
  multiple readable rows at semantic stage boundaries. Preserve execution
  order and make the row-to-row transition explicit.
- Keep connections short with few crossings. Route data connections through
  dedicated channels above or below nodes when necessary; do not route them
  through node bodies or parameter lists.
- Keep every node inside the canvas and avoid overlap. If existing nodes must
  move, move only the affected nodes and include those coordinate changes in
  the same flow preview.

These are visual organization preferences, not replacements for the Schema
2.0 flow contract. A layout preference must not justify changing node IDs,
ports, parameters, connections or runtime metadata.
