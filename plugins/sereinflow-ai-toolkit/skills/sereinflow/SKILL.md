---
name: sereinflow
description: Connect to SereinFlow MCP, load its live routing guide, and operate only the requested capability.
---

# SereinFlow MCP

Use the configured `sereinflow` MCP server as the source of truth for
SereinFlow projects, flows, runs, debugging, versions, SereinLang and library
packages. Treat the service as a remote black box; do not use local source,
databases, library directories or uploaded binaries to explain a server result.

## Required loading order

At the start of a task:

1. Complete a fresh MCP `initialize` handshake and read its `instructions`.
2. Read `sereinflow://ai/guide` with `resources/read`. This is the server's
   authoritative routing entry point. In this repository its backing source is
   `D:/Project/dotnet/SereinFlow/src/SereinFlow.Api/mcp/sereinflow-ai-guide.md`;
   consume it through MCP rather than replacing it with a client-local path.
3. Follow the guide and read only the smallest capability module needed for the
   request. A request spanning capabilities may read multiple modules in task
   order.
4. Discover current tool/resource/prompt schemas only as needed and follow the
   returned schemas. Do not load every skill Resource for every task.

The capability index URIs remain valid for a second-level route, but are not a
substitute for the focused module when the guide names one. Do not assume that
this plugin contains the detailed server Skills.

## Authentication

The HTTP MCP connection receives its bearer token only from
`SEREINFLOW_MCP_API_KEY`, as declared by the plugin's MCP configuration.
Never put the key in this file, task text, tool arguments, logs, repository
files or client-local project files. Never create a second authentication
mechanism or send an API key to a SereinFlow tool.

The variable is read by the host process that owns the MCP client. A PowerShell
`$env:SEREINFLOW_MCP_API_KEY` value affects that shell only; it does not update
an already-running Codex process. Shell environment policy and stdio server
`env_vars` are not replacements for the HTTP bearer-token setting. Enter the
secret through the host's supported credential/environment injection, then
restart Codex or start a new task after creating, rotating or changing it.

## Connection constraints

- Use the configured endpoint and one fresh `initialize` session.
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

For a direct probe, use this order and keep the key in the process environment:

1. `GET /mcp` returning `405` proves the route is reachable; MCP uses `POST`.
2. Unauthenticated `POST initialize` returning `401` with
   `WWW-Authenticate: Bearer` proves bearer authentication is enforced. It
   does not by itself prove a configured key is invalid.
3. Authenticated `POST initialize` returning `200` and a session ID proves
   authentication and session creation. Keep the ID internal.
4. Authenticated follow-up reads with the session ID and negotiated protocol
   header, followed by reading the guide, verify MCP application access. The
   client should still send the negotiated protocol header even though the
   current service does not yet reject every follow-up request that omits it.

The initialize body must be valid MCP JSON-RPC. Use the current protocol
version returned by the server, for example:

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
- Authenticated `401`: the key is absent, invalid, expired or revoked; do not
  replace it with a repository or tool-argument key.
- `502`/`503`/`504`: gateway, connector or startup failure; do not call it an
  authentication failure without an authenticated `401`.
- Initialize succeeds but a follow-up fails: suspect session ID, protocol
  header, session expiry or protocol mismatch before suspecting the key.
- If direct probing is unavailable, report the exact client error and mark
  service health unverified. Do not claim the local service is down.

Use this compact record for connection reports:

```text
Connection status: healthy | client-path-failed | auth-failed | unreachable | unknown
Endpoint: <configured endpoint, without credentials>
Transport: <status and evidence>
Authentication: <not tested | missing/invalid | accepted>
Session/protocol: <not established | established | follow-up headers verified>
Capabilities: tools=<count or ?>, resources=<count or ?>, prompts=<count or ?>
Likely cause: <one evidence-based sentence>
Next action: <one concrete action>
```

Do not report "connected" after only a client-side `200` or `initialize`.
Report which layer was verified and redact secrets, full scripts, sensitive
literals and server-local absolute paths.

## Mutation gate

For an explicit request to create a project, edit a flow, publish, attach or
import a library, use one task-level authorization and this sequence:

```text
read current state -> preview requested scope -> inspect diagnostics/diff
-> apply the matching preview with confirmation: "APPLY", fingerprint and idempotency key
-> reread affected resources and verify persisted state
```

Do not ask for duplicate confirmation between dependent calls. Pause once when
the request is ambiguous, the preview is destructive or unexpected, production
state, permissions or secrets change, a version conflict occurs, or the scope
materially differs from the request. Read-only inspection, compilation and
post-apply rereads do not need confirmation.
