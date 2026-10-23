---
name: sereinflow
description: Inspect or operate a running SereinFlow service through MCP and its live guidance. Use local tools for explicitly requested server-source edits or library builds.
---

# SereinFlow MCP

Use the configured `sereinflow` MCP server as the source of truth for
SereinFlow projects, flows, runs, debugging, versions, run-message endpoints,
SereinLang and library packages. For remote runtime state, use MCP results;
do not infer that state from a local checkout, database or uploaded binary.
Explicitly requested repository work or local library builds may use the
caller's files and toolchain. Keep those local results distinct from remote
service evidence. API-key administration is optional HTTP functionality, not
a set of MCP tools.

## Required loading order

At the start of a task:

1. Reuse the MCP client's initialized session, or let it initialize when no
   session exists. Read its server instructions; do not start a second manual
   session merely because a new task began. Direct probes use their own session.
2. Read the guide URI returned by initialize, normally
   `sereinflow://ai/guide`, with `resources/read`. This is the server's
   authoritative routing entry point; consume it through MCP rather than
   replacing it with a client-local path.
3. Follow the guide and read only the smallest capability module needed for the
   request. A request spanning capabilities may read multiple modules in task
   order.
4. Discover current tool/resource/prompt schemas only as needed and follow the
   returned schemas. Do not load every skill Resource for every task.

The capability index URIs remain valid for a second-level route, but are not a
substitute for the focused module when the guide names one. Do not assume that
this plugin contains the detailed server Skills.

## Authentication

HTTP MCP allows direct access by default. The plugin configuration therefore
contains only the endpoint URL and requires no credential variable.

If the service explicitly enables API-key authentication, add
`"bearer_token_env_var": "SEREINFLOW_MCP_API_KEY"` to the client's connection
configuration when that field is supported, or use the client's supported
credential injection. This example variable is a client setting, not a server
requirement. Host-authenticated deployments may use a different identity scheme.
Never put the key in this file, task text, tool arguments, logs, repository
files or client-local project files. Never create a second authentication
mechanism or send an API key to a SereinFlow tool.

When using the environment-variable example, the variable is read by the host
process that owns the MCP client. A PowerShell
`$env:SEREINFLOW_MCP_API_KEY` value affects that shell only; it does not update
an already-running Codex process. Shell environment policy and stdio server
`env_vars` are not replacements for the HTTP bearer-token setting. Enter the
secret through the host's supported credential/environment injection. Reconnect
after a key change, restarting the owning process if it must reread environment
variables; a new task alone does not refresh an existing process environment.

## Connection constraints

- Use the configured endpoint and the client's current initialized session.
- For every request after initialization, preserve the returned
  `MCP-Session-Id` and send the negotiated/current `MCP-Protocol-Version`.
  Do not hardcode a version that differs from the server handshake. The
  client should send this header even though the current service does not yet
  enforce it on every follow-up request.
- A successful `initialize` proves session creation only. Read the guide and
  make the required capability call before reporting the service as healthy.
- Resources, tools and prompts do not provide subscription or list-change
  updates in the current server contract. Discover catalogs when needed.
- Do not retry a stale or partial session. Establish a fresh session and retry
  a read-only request once when the server reports a missing session header, an
  expired session or a protocol mismatch. Treat the protocol header as current
  client behavior guidance rather than a server-enforced requirement on every
  request.

## Bounded diagnostics

When the MCP client fails ambiguously or the user asks whether the service is
connected, compare it with a bounded direct probe against the same endpoint
when shell/network diagnostics are available. Label observations `Codex MCP
client` or `direct probe`; a shell result proves only that shell's credentials
and network path.

For a direct probe, use this order. If API-key authentication is enabled,
keep its key in the process environment:

1. `GET /mcp` returning `405` proves the route is reachable; MCP uses `POST`.
2. `POST initialize` without credentials should return `200` in default mode.
   A `401` with `WWW-Authenticate: Bearer` means that the host or a gateway
   requires Bearer authentication; it does not identify the implementation
   or prove that a configured key is invalid.
3. In API-key mode, authenticated `POST initialize` returning `200` and a
   session ID proves authentication and session creation. Keep the ID internal.
4. Follow-up reads with the session ID and negotiated protocol
   header, followed by reading the guide, verify MCP application access. The
   client should still send the negotiated protocol header even though the
   current service does not yet reject every follow-up request that omits it.

For a direct probe, initialize with a supported client protocol version. The
following is an example for the currently supported `2025-06-18`; use the
version returned by the handshake for follow-up requests:

```json
{
  "jsonrpc": "2.0",
  "id": "diagnostic-initialize",
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-06-18",
    "capabilities": {},
    "clientInfo": { "name": "codex-sereinflow-diagnostic", "version": "1.0" }
  }
}
```

Send `Content-Type: application/json` and an appropriate `Accept` header. Do
not print authorization headers, API keys, session IDs or sensitive response
fields. Retry connection resets, timeouts and `502`/`503`/`504` only a small,
finite number of times with short backoff. Never blindly retry a mutating call;
only retry one when its contract is idempotent and supplies an idempotency key.

Use these conclusions:

- Connection refused or timeout: the host/port is unreachable from the
  probing process or the API is not listening there.
- `401` in API-key mode: the key is absent, invalid, expired or revoked.
  Host authentication or a gateway can also return `401`; inspect the host's
  challenge before assuming that the optional key package is enabled.
- `502`/`503`/`504`: gateway, connector or startup failure; do not call it an
  authentication failure without an authenticated `401`.
- Initialize succeeds but a follow-up fails: use the returned status and
  diagnostic. `401` indicates lost authentication; `403` indicates denied
  access; session errors require checking the session ID/expiry. A successful
  initialize does not prove access to every tool or project.
- If direct probing is unavailable, report the exact client error and mark
  service health unverified. Do not claim the local service is down.

Use this compact record for connection reports:

```text
Connection status: healthy | client-path-failed | auth-failed | unreachable | unknown
Endpoint: <configured endpoint, without credentials>
Transport: <status and evidence>
Authentication: <not required | host-managed | not tested | missing/invalid | accepted>
Session/protocol: <not established | established | follow-up headers verified>
Capabilities: tools=<count or ?>, resources=<count or ?>, prompts=<count or ?>
Likely cause: <one evidence-based sentence>
Next action: <one concrete action>
```

Do not report "connected" after only a client-side `200` or `initialize`.
Report which layer was verified and redact secrets, full scripts, sensitive
literals and server-local absolute paths.

## Mutation gate

For an explicit request to create a project, edit a flow, publish, rollback,
attach or import a library, or assign or upgrade a library family, use the
preview/apply contract:

```text
read current state -> preview requested scope -> inspect diagnostics/diff
-> apply the matching preview with confirmation: "APPLY", fingerprint and idempotency key
-> reread affected resources and verify persisted state
```

The user's request authorizes the stated task and dependent calls. Do not ask
for duplicate confirmation; the protocol field `confirmation: "APPLY"` is not
itself a demand for another user approval. Clarify ambiguous or expanded scope,
and stop on conflicts or unexpected destructive, release, permission or secret
effects the user has not authorized. Read-only inspection, compilation and
post-apply rereads do not need confirmation.

Run-message publication, variable writes and debug start use direct calls with
an `idempotencyKey`; debug controls use `commandSequence`. These operations
have no preview/apply phase. Follow the runtime module and current schemas.

For `sereinflow_publish_run_message`, include a non-empty `idempotencyKey` and
use the runtime skill. `accepted` only means Worker broker acceptance; verify
downstream run state or outputs. For optional HTTP API-key management, read
`sereinflow://ai/skills/sereinflow/api-keys` and use the web console or the
authorized `/api/security/api-keys` endpoints. There are no MCP key-management
tools. Do not initialize keys in default mode or repeat an ambiguous create or
rotate call without first reading current key state.

## Debug workflow

For interactive debugging, load the focused runtime Resource named by the live
guide, then use `sereinflow_list_debug_sessions` or
`sereinflow_list_runs` to discover the target, then call
`sereinflow_start_debug_session` with an `idempotencyKey`. Observe the
session through `sereinflow_get_debug_state` or
`sereinflow_wait_debug_state`. The wait tool waits for a newer paused state or
a terminal state; running node progress does not finish the wait. Use the get
tool for intermediate progress. Continue from the `stateRevision` returned by
start or control acknowledgements; a timeout returns the current state and
does not mean the run failed. A paused session accepts
`sereinflow_step_debug` or `sereinflow_continue_debug`; every command must use
the next strictly increasing `commandSequence`. Use
`sereinflow_stop_debug` to cancel an active session. Start and control require
`debug.control` when authentication is configured; discovery, state and wait
reads accept `debug.read` or `run.read` in that mode. Do not blindly retry a
control command after an ambiguous response—read the durable state first.
