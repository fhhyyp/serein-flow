# SereinFlow MCP Skill

## Boundary and discovery

Use SereinFlow MCP as the source of truth for projects, flows, runs, debug
sessions, versions and mutation previews. The MCP server is a remote service
boundary. Do not inspect server source code, server-local databases, library
directories or uploaded binaries to explain a service result.

If the project is unknown, call `sereinflow_list_projects`. Read the selected
project and flow topology before proposing work. For flow editing, call
`sereinflow_get_flow_edit_model` before constructing nodes, ports, parameters,
coordinates or connections. For execution problems, use run inspection or
debug state resources and bounded diagnostics. Do not claim completion from a
stale snapshot.

## Task-level mutation authorization

When the user explicitly asks to create a project, modify a flow, or upload or
import a library, treat that request as authorization for the named logical
task and its necessary dependent MCP calls. Do not ask for confirmation after
each internal preview or between dependent writes.

Use this sequence once per logical task:

```text
read current state -> preview the logical change or dependent batch
-> inspect diagnostics and diff -> apply each matching preview
-> reread affected resources and verify version/checksum/counts
```

The apply tools still require `confirmation: "APPLY"`, the preview fingerprint,
and an idempotency key. Populate those protocol fields after the user's
task-level authorization without asking the user to repeat it. A request that
combines library import, project attachment, and flow editing is one logical
task, so it uses one authorization across the dependent steps.

Pause for one concise confirmation only when the request is ambiguous, the
preview reveals destructive or unexpected scope, the operation publishes or
rolls back production, changes permissions or secrets, encounters a version
conflict, or would perform a materially different operation. A preview is not
authorization for work outside the requested scope. Read-only inspection and
compilation never require confirmation.

## Flow patches

Flow changes start with `sereinflow_preview_flow_patch` and use schema version
`2.0`, camelCase enum values and typed operations. New nodes, canvases and
connections need stable IDs. Remove connections before removing nodes, and
handle canvas contents before removing a canvas. Preserve the canonical node
template returned for an attached library; do not rebuild its ports, parameter
IDs or runtime metadata.

Allowed v2 operations are `addCanvas`, `updateCanvas`, `removeCanvas`,
`addNode`, `replaceNode`, `removeNode`, `setNodeParameter`, `addConnection`,
`replaceConnection`, `removeConnection`, `setEntryNode`, `setRunPolicy`, and
`replaceScriptSource`. The v2 envelope uses `op` and named typed payloads; do
not put new requests in the legacy `operation` / `value` envelope.

Treat `schemaVersion: "2.0"`, `enumEncoding: "camelCase"`,
`normalizedOperations`, and `normalizationWarnings` in the preview response
as the authoritative contract. A field, discriminator, enum, ID or reference
diagnostic is a hard stop. Report its stable `mcp.flow_patch.*` code,
`diagnosticId`, `fieldPath`, `expected`, and remediation.

## Layout

Before placing nodes, use the edit model's canvas bounds, node positions,
dimensions, ports and connections. Every node needs a non-overlapping bounding
box with clearance from existing nodes and canvas boundaries. Lay out the main
execution path left to right, keep branch rows separate, and route connections
without crossing node bodies. Move only affected existing nodes when needed.

## Versions, execution and verification

Development and production are separate tracks. Only the production head is
eligible for environment execution. Publish and rollback remain high-impact
operations: preview them separately and pause for one explicit confirmation
before applying. Use version resources or comparison tools to verify the
resulting track and checksum.

After a successful apply, reread the affected resource and verify its version,
checksum, counts and state. Treat an apply response as an acknowledgement,
not the authoritative projection. If rereading does not match, report
`mcp.post_apply_verification_failed` and stop.

## Security and errors

Never expose API keys, full secrets, sensitive literals, full scripts or
server-local absolute paths in prompts, logs or audit summaries. Report stable
MCP error codes, diagnostic IDs and bounded safe fields. Stop on
authorization, validation, stale-version, fingerprint or post-apply
verification errors. Do not retry a rejected mutation blindly.
