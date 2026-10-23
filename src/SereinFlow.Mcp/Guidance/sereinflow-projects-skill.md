# SereinFlow Projects

Use SereinFlow MCP as the source of truth for project identity, flow topology,
library references and current state. Do not inspect server source, databases,
library directories or uploaded binaries to explain a remote result.

If the project is unknown, call `sereinflow_list_projects`. Read the selected
project and its flow topology before proposing work. Use project and flow
Resources for current readback; do not claim completion from a stale snapshot.

Keep identifiers supplied by the service unchanged. Resolve project scope and
permissions from current tool responses rather than guessing from names or
local repository paths. For project library details, route to the library
upgrade module.

For an explicitly requested new project, use
`sereinflow_preview_create_project` with `name` and optional `flowName`.
Inspect the generated project/flow IDs and diagnostics, then call
`sereinflow_apply_create_project` with the preview ID, fingerprint,
`confirmation: "APPLY"` and an `idempotencyKey`. The server creates an empty
development flow; reread the project and its edit model before adding nodes.
When authentication is configured, creation requires `project.write` and
`workspace.manage`; a project-scoped key cannot create another project.
