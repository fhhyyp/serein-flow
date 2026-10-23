# Optional HTTP API-key administration

Key administration belongs to the optional `SereinFlow.Security.ApiKeys` package.
HTTP and MCP allow direct access by default. Key services are registered only
when the application developer explicitly enables API-key authentication.
MCP has no tools for creating, listing, rotating or revoking keys. Use the web
console or the authorized HTTP `/api/security/api-keys` endpoints instead.
`/api/security/capabilities` indicates whether the host offers key management.

Project keys reference an existing non-archived project; administrator keys
are not project scoped. Operations use dotted names such as `project.read`,
`flow.write`, `run.execute` and `sensitive.read`. Key administration requires
`security.manage`; workspace administration uses `workspace.manage`.

Secrets are returned only at creation or rotation. Save them through the
operator's secret-management path; never include them in prompts, repository
files, screenshots or unrelated tool arguments. Reread metadata after an
ambiguous management response before retrying. Revoked or expired keys cannot
establish new requests or subscriptions. Local stdio requires no key. External
HTTP hosts may use their existing identity provider instead of API keys.
