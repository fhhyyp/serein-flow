# SereinFlow MCP API Keys

MCP API-key administration is an administrator-only capability. Use
`sereinflow_list_mcp_api_keys` to inspect current keys before changing them.
Project-scoped keys must name an existing non-archived project; administrator
keys cannot be project-scoped.

`permissions` is a required array of stable dotted names. Valid names are
`project.read`, `project.write`, `library.read`, `run.read`, `debug.read`,
`flow.write`, `debug.control`, `flow.publish`, `flow.rollback`,
`script.compile`, `library.import`, `library.manage`, `mcp.keys.manage`,
`sensitive.read` and `run.message.publish`. A project-scoped key supplies
`projectId` with `isAdministrator: false` (or omits that optional flag); an
administrator key supplies `isAdministrator: true` and omits `projectId`.

Use `sereinflow_create_mcp_api_key`, `sereinflow_rotate_mcp_api_key`, or
`sereinflow_revoke_mcp_api_key` only for the explicitly requested key and
confirm the target, permissions and expiration before applying the mutation.
All mutating calls require a non-empty `idempotencyKey`. After an ambiguous
response, reread the key list before retrying; never blindly repeat create,
rotate or revoke.

The secret returned by create or rotate is available only once. Do not place it
in prompts, tool arguments unrelated to the operation, logs, source files,
repository files or screenshots. Give it to the operator through the approved
secret-management path and verify only the safe key metadata afterward.

Treat revoked or expired keys as unusable. Do not use an API-key management
tool to pass credentials to another SereinFlow tool; MCP authentication is
provided by the client connection itself.
