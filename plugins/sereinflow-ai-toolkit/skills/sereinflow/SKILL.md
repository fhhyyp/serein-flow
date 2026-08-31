---
name: sereinflow
description: Connect to the SereinFlow MCP service, load its current operating guide and live capabilities, and diagnose transport, authentication, and session failures with evidence.
---

# SereinFlow MCP

Use the configured `sereinflow` MCP server as the only source of truth for
SereinFlow projects, flows, runs, debugging, versions, SereinLang compilation,
and library package workflows.

## MCP capability contract

When the connection is authenticated, the SereinFlow MCP server declares and
implements these protocol capabilities:

- `tools`: discover with `tools/list` and invoke with `tools/call`.
- `resources`: discover fixed resources with `resources/list`, URI templates
  with `resources/templates/list`, and content with `resources/read`.
- `prompts`: discover workflow prompts with `prompts/list` and retrieve one
  with `prompts/get`.

The server declares `resources.subscribe: false` and `listChanged: false` for
resources, tools, and prompts. Do not wait for subscription updates or
list-change notifications; discover the current catalog when needed. The
plugin manifest's `Interactive` and `Write` values are UI metadata, not MCP
protocol capability declarations.

The HTTP connection obtains its bearer token only from the
`SEREINFLOW_MCP_API_KEY` environment variable. Never put an API key in the
plugin, repository, task text, tool arguments, or client-local project files.
If initialization cannot authenticate, report that the SereinFlow MCP API key
is absent, invalid, expired, or revoked; do not treat it as a missing Skill or
attempt to bypass server authentication.

`bearer_token_env_var` is read by the Codex host that owns the MCP client. A
PowerShell `$env:SEREINFLOW_MCP_API_KEY` value proves only that the current
PowerShell process has the variable; it does not update an already-running
Codex desktop process. `shell_environment_policy` controls Shell subprocesses,
and `mcp_servers.<id>.env_vars` is for stdio servers; neither is a replacement
for the HTTP bearer-token setting. Use the Codex credential/environment
injection supported by the host, then restart Codex or start a new task.

## Connection lifecycle and diagnostics

Treat connection health as four separate layers. Do not collapse a transport
error from the Codex MCP client into a conclusion about the SereinFlow service:

1. **Transport**: can the configured host and route be reached?
2. **Authentication**: does the endpoint accept the bearer token?
3. **MCP session**: did `initialize` succeed, and are the returned session and
   protocol headers used on follow-up requests?
4. **Capabilities**: can the authenticated session read the guide and list the
   current tools, resources, resource templates, and prompts?

The normal connection sequence is:

1. Use the configured `sereinflow` MCP server. Do not send an API key in tool
   arguments or try to create a second authentication mechanism.
2. Complete one fresh `initialize` handshake and record only non-secret
   evidence: status, error body category, and whether `MCP-Session-Id` was
   returned. Do not print the bearer token, full authorization header, or any
   secret-bearing response fields.
3. For every request after `initialize`, preserve the returned
   `MCP-Session-Id` and send `MCP-Protocol-Version: 2025-03-26` (header names
   are case-insensitive). If a session is stale or the server reports a
   missing protocol/session header, create a fresh session and retry the
   read-only request once.
4. Read `initialize.instructions`, then `sereinflow://ai/guide`, and only the
   relevant capability resource. A successful `initialize` alone proves only
   that a session was created; it is not a full health check.
5. Verify the session with the current capability catalogs: `tools/list`,
   `resources/list`, `resources/templates/list`, and `prompts/list`. Record
   successful result counts when available. An empty catalog is meaningful only
   after a successful authenticated `initialize` and a successful catalog
   response.

When the MCP client reports an ambiguous failure, or when a user asks whether
the service is connected, perform a bounded direct HTTP comparison against the
same configured endpoint if shell/network diagnostics are available. Label
every observation as either `Codex MCP client` or `direct probe`; a successful
PowerShell probe proves the service and key work for that PowerShell process,
not that an already-running Codex process has loaded the same environment.
Keep the API key in the process environment and report only presence/absence
and status classes.

For a direct probe, use the configured URL and this sequence:

1. `GET /mcp`: `405 Method Not Allowed` is expected and proves that the
   listener and route are reachable; it is not an MCP failure. MCP requests
   use `POST`.
2. Unauthenticated `POST initialize`: `401` with
   `WWW-Authenticate: Bearer` proves that the route is reachable and bearer
   authentication is enforced. Never use this result alone to claim the key is
   invalid.
3. Authenticated `POST initialize`: `200` plus a session ID proves that the
   endpoint accepted the key and created an MCP session. Capture the session ID
   internally, but do not expose it as a credential or log it in full.
4. Authenticated follow-up catalog requests with both the session ID and
   `MCP-Protocol-Version: 2025-03-26`: successful `200` responses prove that
   the session can use the MCP API. Read the guide resource as a final
   application-level check.

The diagnostic initialize payload must be a valid MCP JSON-RPC request, for
example:

```json
{
  "jsonrpc": "2.0",
  "id": "diagnostic-initialize",
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-03-26",
    "capabilities": {},
    "clientInfo": {
      "name": "codex-sereinflow-diagnostic",
      "version": "1.0"
    }
  }
}
```

For direct HTTP diagnostics, send `Content-Type: application/json` and an
appropriate `Accept` header. Send `Authorization: Bearer <value>` only from
the process environment; never put the value in this file, command output,
task text, or a tool argument. Use the response session header on all
subsequent requests. Handle either JSON or event-stream response framing when
the transport allows both.

Apply these bounded retry rules:

- Retry connection resets, timeouts, and `502`/`503`/`504` only a small,
  finite number of times with short backoff while the service may be starting.
- Retry `initialize`, catalog reads, and resource reads safely. Do not blindly
  retry a mutating `tools/call`; retry it only when the server contract says
  the operation is idempotent and supplies an idempotency key.
- After a failed initialization, do not reuse a partial or stale session. After
  a client restart or server restart, establish a new session before listing
  capabilities.
- Do not make repeated probes indefinitely. Report the attempts and the last
  observed status when the bounded check still fails.

Use these evidence-based conclusions:

- `POST initialize` returning `401` with `WWW-Authenticate: Bearer` means the
  listener and route are reachable, but the key is missing, invalid, expired,
  or revoked.
- Connection refused or a timeout means the configured host/port is not
  reachable from the process performing the probe, or the API is not running
  there.
- `502` or `503` from a client transport is a gateway, connector, or startup
  failure. It is not proof that a local port has no listener; report the
  transport status separately.
- If the direct probe reaches `GET /mcp` with `405`, gets `401` without a key,
  gets `200` with the configured key, and completes the follow-up catalog
  requests, the SereinFlow service and key are healthy. If the Codex MCP client
  still reports `502`, the likely fault is the Codex client/connector state or
  stale credential injection. Restart Codex or start a new task, then rerun the
  MCP handshake.
- If the direct authenticated initialize returns `401`, distinguish an absent
  key from an invalid, expired, or revoked key without exposing the value. Do
  not replace it with a key in the repository, task text, or tool arguments.
- If direct requests return `502`/`503`/`504`, the failure is in the service
  startup, gateway, or connector path. Do not state that authentication failed
  unless an authenticated request actually returned `401`.
- If `initialize` succeeds but follow-up requests fail, suspect a missing or
  stale `MCP-Session-Id`, a missing `MCP-Protocol-Version`, an expired session,
  or a protocol mismatch before suspecting the API key. Establish one fresh
  session and retest a read-only catalog call.
- If direct probing is unavailable, report the MCP client's exact observed
  status/error and mark service health as unverified. Do not claim the local
  service is down or the key is invalid based only on an unverified client
  error.

When reporting a connection test or failure, use this compact diagnostic
record so the user can distinguish the failing layer:

```text
Connection status: healthy | client-path-failed | auth-failed | unreachable | unknown
Endpoint: <configured endpoint, without credentials>
Transport: <status and evidence>
Authentication: <not tested | missing/invalid | accepted>
Session/protocol: <not established | session established | follow-up headers verified>
Capabilities: tools=<count or ?>, resources=<count or ?>,
              templates=<count or ?>, prompts=<count or ?>
Likely cause: <one evidence-based sentence>
Next action: <one concrete action>
```

Never report "connected" after only seeing a client-side `200` or an
`initialize` response. Report the layer that was verified and the layer that
still failed. Preserve status codes and relevant safe response headers in the
diagnostic notes, but redact authorization values, API keys, session IDs, and
other secrets.

After creating, rotating, or changing `SEREINFLOW_MCP_API_KEY`, restart the
Codex process or start a new task so the MCP client reloads its credentials.
The Web Console cannot mutate the environment of an already running Codex
process. Once connected, repeat `initialize`, then discover `tools/list`,
`resources/list`, `resources/templates/list`, and `prompts/list` before
reporting actual server capabilities.

## Configure the client key

The SereinFlow Web Console can generate and persist MCP keys. Open the
environment settings, use the local-only first-key setup when no key exists,
then create a project-scoped client key with the smallest required permissions.
The complete `sfk_...` Secret is returned only once after creation or rotation.

In the Codex MCP credential configuration, set the variable named
`SEREINFLOW_MCP_API_KEY` to that complete Secret. The variable name is already
declared by this plugin; the plugin never contains the Secret value. A browser
cannot change the environment of an already running Codex process, so the
Secret must be entered through the client's supported credential UI or secure
environment/secret injection mechanism. Use the server bootstrap setting only
for first-time recovery when the Web Console is unavailable, and remove it
after a dedicated client key has been created.

At the beginning of a task:

1. Read `initialize.instructions` from the MCP server.
2. Read the `sereinflow://ai/guide` resource.
3. Read only the relevant capability Resource:
   `sereinflow://ai/skills/sereinflow`,
   `sereinflow://ai/skills/sereinlang`, or
   `sereinflow://ai/skills/sereinflow-library-package`.
4. Discover `tools/list`, `resources/list`, and `prompts/list` as needed.
5. Follow the current server-provided guide and the schemas returned by the
   server. Do not rely on copied local SereinFlow source, database paths,
   library directories, or hardcoded workflow rules.

The server index is dynamic and only routes the request to the relevant
capability Resource. Read only the selected capability Resource; do not load
all SereinFlow, SereinLang, and library rules for every task. The corresponding
MCP tools are the live implementation of those capabilities. Never assume
that this plugin contains the server source code or a local copy of the
detailed Skills.

## Task-level write authorization

For an explicit request to create a project, edit a flow, or upload or import a
library, treat the initial request as authorization for that named logical task
and its dependent MCP calls. Use `read -> preview -> inspect -> apply ->
reread`, but do not ask for a separate confirmation after every preview or
between dependent writes. A single request that includes library import,
project attachment, and flow editing uses one authorization for the whole chain.

The apply tools still require `confirmation: "APPLY"`, the preview fingerprint,
and an idempotency key; fill those protocol fields without prompting again once
the preview matches the user's requested scope.

Pause for one concise confirmation only when the request is ambiguous, the
preview reveals destructive or unexpected scope, the operation publishes or
rolls back production, changes permissions or secrets, encounters a version
conflict, or would perform a materially different operation. A preview is not
authorization for work outside the requested scope. Read-only inspection,
compilation, package scanning, and post-apply rereads do not require a prompt.

## Library family and upgrade workflow

For library-family assignment or a project library version upgrade, read the
`sereinflow://ai/skills/sereinflow-library-package` capability Resource and
discover the current tool schemas. A family assignment is administrator-only
and uses its own preview/apply pair. It changes catalog membership only; it
never changes the immutable ZIP or an existing flow binding.

For upgrades, follow this stateful sequence:

```text
read project library references
-> read visible family/artifact candidates
-> preview library upgrade
-> inspect blockers and acknowledgements
-> explicit user confirmation when the scoped request has not already authorized the apply
-> apply with confirmation, fingerprint, and idempotency key
-> reread persisted upgrade plan, project references, and flow
```

The source artifact must be referenced by the project; the source and target
must be assigned to the same family; the target must be available. Project
scoped callers can read only their project's referenced family artifacts. The
apply contract is bound to the stored preview and does not permit replacing its
project, source artifact, target artifact, or upgrade plan. Batch applies are
per-flow transactional and may return both successful and failed entries; use
the persisted readback before presenting a completed result.
