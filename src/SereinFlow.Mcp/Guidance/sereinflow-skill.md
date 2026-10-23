# SereinFlow Capability Index

This Resource is a router, not the complete SereinFlow operating manual. Read
only the module that matches the request. The live MCP tool schemas and current
read resources are authoritative; treat the service as a remote black box.

| Work | Resource URI |
| --- | --- |
| Project discovery, creation and inspection | `sereinflow://ai/skills/sereinflow/projects` |
| Flow editing and patch contracts | `sereinflow://ai/skills/sereinflow/flows` |
| Flow canvas layout and UI/UX | `sereinflow://ai/skills/sereinflow/ui-ux` |
| Runs, debugging and verification | `sereinflow://ai/skills/sereinflow/runtime` |
| Run images, files and uploaded workpieces | `sereinflow://ai/skills/sereinflow/workpieces` |
| Run-message endpoints and variables | `sereinflow://ai/skills/sereinflow/runtime` |
| Optional HTTP API-key management (no MCP key tools) | `sereinflow://ai/skills/sereinflow/api-keys` |
| Publishing and rollback | `sereinflow://ai/skills/sereinflow/release` |

For an unknown project, list projects first, then read the selected project and
flow topology. For any flow edit, read the flow edit model before constructing
nodes, ports, parameters, coordinates or connections.

For project creation, flow edits, releases and library changes, read current
state, preview the requested logical change, inspect diagnostics and diff,
apply the matching preview with
the protocol fields, then reread the affected resources. The user's explicit
request authorizes its named task and dependent calls; clarify ambiguous or
expanded scope, and stop on conflicts or unexpected effects outside that scope.
Run messages, variables and debugging use the runtime module's direct-call
contracts rather than preview/apply. Optional API-key management uses HTTP.
