# SereinFlow Capability Index

This Resource is a router, not the complete SereinFlow operating manual. Read
only the module that matches the request. The live MCP tool schemas and current
read resources are authoritative; treat the service as a remote black box.

| Work | Resource URI |
| --- | --- |
| Project discovery and inspection | `sereinflow://ai/skills/sereinflow/projects` |
| Flow editing, patches and layout | `sereinflow://ai/skills/sereinflow/flows` |
| Runs, debugging and verification | `sereinflow://ai/skills/sereinflow/runtime` |
| Publishing and rollback | `sereinflow://ai/skills/sereinflow/release` |

For an unknown project, list projects first, then read the selected project and
flow topology. For any flow edit, read the flow edit model before constructing
nodes, ports, parameters, coordinates or connections.

For a mutation, use the shared gate: read current state, preview the requested
logical change, inspect diagnostics and diff, apply the matching preview with
the protocol fields, then reread the affected resources. The user's explicit
request authorizes its named task and dependent calls; pause once for an
ambiguous, destructive, production, permission, secret or conflict case.
