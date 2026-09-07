# SereinFlow MCP Service

## Status

MCP is hosted exclusively by `SereinFlow.Api`. In Web mode, `/mcp` is mounted
in the same API process and shares one DI container with the regular Web API,
Application services, database, library directories, and execution services.
Local automation can explicitly use `SereinFlow.Api --mcp-stdio`.

The MCP backend accesses business data only through Application services and
controlled DI scopes. It does not call a local REST callback API or access
SQLite, SqlSugar repositories, or Worker handles directly. Flow changes,
publishing, rollback, SereinLang compilation, and library import continue to
pass through permission checks, preview confirmation, idempotency controls, and
auditing.

## Startup

### Web API and HTTP MCP

Build and start the API:

```text
dotnet build src/SereinFlow.Api/SereinFlow.Api.csproj
dotnet run --project src/SereinFlow.Api/SereinFlow.Api.csproj
```

The development MCP endpoint is `http://127.0.0.1:8188/mcp`. Requests must
include:

```text
Authorization: Bearer <api-key>
```

The first `initialize` request establishes an `Mcp-Session-Id`; subsequent
requests must return the same session ID. The HTTP transport continues to
enforce request-body, response, concurrency, per-principal rate, and tool
timeout limits. Unauthenticated requests, invalid sessions, limit violations,
timeouts, and internal failures return stable `mcp.*` diagnostics. Loopback
addresses do not provide an unauthenticated administrator bypass.

The MCP client configuration committed to Git stores only the URL, not the API
key, database path, or library directory:

```json
{
  "mcpServers": {
    "sereinflow": {
      "type": "http",
      "url": "http://127.0.0.1:8188/mcp"
    }
  }
}
```

The API key should be supplied through the MCP client's secure credentials,
external secret injection, or a credential store. Do not write the key into the
repository, plugin configuration, logs, or flow content.

SereinFlow AI Toolkit uses `SEREINFLOW_MCP_API_KEY` as the environment variable
name for the HTTP Bearer token. The plugin declares only the variable name and
does not store or transmit the secret. Manage keys from **Environment settings**
in the Web Console: select **Generate first key**, then create a project-scoped
client key for Codex. The complete `sfk_...` Secret is shown only when it is
created or rotated; the server database stores only its hash and salt. The
initialization endpoint accepts only local loopback requests. If the server and
browser are on different machines, a server administrator must complete the
initial initialization through controlled configuration before key management
can continue in the Web Console.

Put the generated Secret into Codex's MCP credential configuration. Set the
variable name to `SEREINFLOW_MCP_API_KEY` and paste the complete Secret shown by
the Web Console as its value. For security reasons, the browser cannot directly
modify the environment of an already-running Codex process. The server can
generate and persist the key, but the client still requires this credential
binding step. Do not write the Secret into the repository, plugin source files,
logs, or flow content.

Only when the Web Console is unavailable, use PowerShell to configure the first
bootstrap key. The angle-bracket value must be the same random secret in both
lines and must not be committed or recorded:

```powershell
[Environment]::SetEnvironmentVariable('SereinFlow__Mcp__BootstrapAdminKey', '<temporary-random-secret>', 'User')
[Environment]::SetEnvironmentVariable('SEREINFLOW_MCP_API_KEY', '<temporary-random-secret>', 'User')
```

After changing user environment variables, restart the running
`SereinFlow.Api` and Codex processes so both read the new environment. An HTTP
`401` means that the endpoint and MCP route are reachable but the Bearer key is
missing, invalid, expired, or revoked; it is not a port or resource-catalog
failure.

### Local stdio

stdio is an explicit entry point of the API executable:

```text
dotnet run --project src/SereinFlow.Api/SereinFlow.Api.csproj -- --mcp-stdio
```

stdio mode registers only the MCP, Application, Storage, and stdio services it
needs. It does not start the HTTP listener, SignalR, or API-only hosted
services. `SereinFlow:Mcp:Stdio:ApiKey` must be set explicitly (the environment
variable form is `SereinFlow__Mcp__Stdio__ApiKey`). A missing, invalid, expired,
or revoked key causes the process to exit with a nonzero status. stdout outputs
only JSON-RPC; startup errors, logs, and internal diagnostics go to stderr.
Normal EOF performs cleanup and exits normally.

## JSON-RPC Protocol Error Codes

`McpProtocolException` uses the named constants in
`McpProtocolErrorCodes` instead of raw numeric literals. The JSON-RPC wire
response still exposes the protocol code as a number. Business diagnostics in
`error.data.code` remain stable string codes such as `mcp.invalid_arguments`.

| Constant | Wire code | Meaning |
| --- | ---: | --- |
| `ParseError` | `-32700` | The request body is not valid JSON. |
| `InvalidRequest` | `-32600` | The JSON-RPC envelope is invalid. |
| `MethodNotFound` | `-32601` | The requested MCP method or tool is not supported. |
| `InvalidParams` | `-32602` | Request parameters or a tool payload are invalid. |
| `InternalError` | `-32603` | The server failed unexpectedly. |
| `GenericServerError` | `-32000` | A server-side failure has no more specific mapping. |
| `Unauthenticated` | `-32001` | Authentication is required or failed. |
| `PermissionDenied` | `-32003` | The caller is authenticated but not authorized. |
| `ResourceNotFound` | `-32004` | The requested project, flow, preview, resource, or key was not found. |
| `TransientFailure` | `-32005` | The request was throttled or a dependent service timed out. |
| `Conflict` | `-32010` | The resource changed or conflicts with the requested mutation. |
| `OperationRejected` | `-32011` | The operation is understood but cannot be applied. |
| `RequestTooLarge` | `-32012` | The request exceeds the configured size limit. |
| `ResponseTooLarge` | `-32013` | The response exceeds the configured size limit. |
| `LibraryInspectionUnavailable` | `-32020` | Library package inspection is unavailable in the host. |

Clients should branch on the numeric `error.code` for JSON-RPC handling and
use `error.data.code` for SereinFlow-specific remediation. The numeric values
are protocol values; they are not interchangeable with the string business
error-code families in `SereinFlow.Contracts`.

## Data Path Configuration

The database and library directories are server-host configuration, not MCP
client parameters. The unified configuration is under `SereinFlow`:

```json
{
  "SereinFlow": {
    "DataRoot": "data",
    "DatabaseFileName": "sereinflow.db",
    "LibraryDirectoryName": "libraries",
    "ScriptArtifactDirectoryName": "script-artifacts",
    "McpStagingDirectoryName": "mcp-staging",
    "WorkpieceDirectoryName": "workpieces"
  }
}
```

The relative `DataRoot` name is resolved only by the server host relative to
`ContentRootPath`, then resolved once by the shared
`SereinFlowStorageOptions`. Do not continue configuring the old
`DatabasePath`, `LibraryDirectory`, `ScriptArtifactRoot`, or MCP staging path
keys. When `DataRoot` is not explicitly configured, detecting these keys fails
with a safe-migration diagnostic instead of silently switching databases.

## Resources

The server exposes a compact routing index at `sereinflow://ai/guide` and three
capability index Resources:

```text
sereinflow://ai/guide
sereinflow://ai/skills/sereinflow
sereinflow://ai/skills/sereinlang
sereinflow://ai/skills/sereinflow-library-package
```

The routing index is intentionally short. A client should read only the
capability Resource matching the current request, so a syntax check does not
load flow, release, and C# packaging rules. Former local Skills are represented
by server Resources; each capability also exposes smaller task modules,
including the run workpiece module for image preview and file-download rules.
These contents are not copied into the client plugin.

Flow canvas layout and visual organization guidance is available separately at
`sereinflow://ai/skills/sereinflow/ui-ux`; load it together with the flow patch
module when an MCP client is arranging nodes and connections.

Each Resource is loaded from the server deployment on every read. An operator
can therefore update one Markdown file without rebuilding or reinstalling the
client plugin. The backing files are selected only by server configuration and
are constrained to remain under the server ContentRoot. The MCP caller can
request only fixed Resource URIs and cannot select an arbitrary local file. The
default index files are `mcp/sereinflow-ai-guide.md`,
`mcp/sereinflow-skill.md`, `mcp/sereinlang-skill.md`, and
`mcp/sereinflow-library-package-skill.md`. Task modules use the
`mcp/*-skill.md` default paths listed by `sereinflow://ai/guide`. Deployments
can override the four index paths with
`SereinFlow:Mcp:AiGuidance:FilePath`,
`SereinFlow:Mcp:AiGuidance:SereinFlowFilePath`,
`SereinFlow:Mcp:AiGuidance:SereinLangFilePath`, and
`SereinFlow:Mcp:AiGuidance:LibraryPackageFilePath`. Task module paths can be
overridden under `SereinFlow:Mcp:AiGuidance:Modules:<resource-key>`. Every file
is subject to the shared `SereinFlow:Mcp:AiGuidance:MaxBytes` limit.

```text
sereinflow://projects
sereinflow://archived-projects
sereinflow://libraries
sereinflow://archived-libraries
sereinflow://projects/{projectId}
sereinflow://projects/{projectId}/flows/{flowId}/topology
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}
sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}
sereinflow://libraries/{libraryId}
sereinflow://runs
sereinflow://debug-sessions
sereinflow://runs/{runId}
sereinflow://debug-sessions/{sessionId}
sereinflow://mcp-previews/{previewId}
```

The default project and library collection Resources represent the active
working set: `sereinflow://projects` excludes archived projects and
`sereinflow://libraries` contains only available library artifacts. Their
archived counterparts return only archived records, so the default and
archived collections are mutually exclusive. Direct project and library
Resources remain readable by ID for audit and existing-reference inspection.

## Prompts

MCP clients that support the standard Prompt capability can discover these
workflow entry points through `prompts/list` and request a prepared instruction
through `prompts/get`:

```text
sereinflow.inspect
sereinflow.edit-flow
sereinflow.debug-run
sereinflow.publish-flow
sereinflow.package-library
sereinflow.upgrade-library
sereinlang.compile
```

Each Prompt returns a bounded workflow message and injects only its selected
capability Resource. `sereinflow.inspect` accepts an optional `request`
argument; the other Prompts require a non-empty `request` argument. Prompt
messages guide the client through the corresponding read-only inspection,
preview, explicit confirmation, apply, and verification steps. A Prompt does
not authorize a mutation by itself.

Clients that do not automatically read MCP resources or invoke Prompts still
receive only the compact routing and safety rules through
`initialize.instructions` and the individual tool descriptions. Detailed
capability text is never appended to initialization, which keeps unrelated
rules out of the initial context. The Resource and Prompt catalogs do not
contain API keys, server absolute paths, client-local build paths, database
names, or server-local library directories.

## Tools

The tool catalog returned by `tools/list` includes project, flow, run, debug,
library, and API-key management capabilities. The debug loop discovers targets
with `sereinflow_list_runs` or `sereinflow_list_debug_sessions`, starts one with
`sereinflow_start_debug_session`, and controls it through
`sereinflow_wait_debug_state`, `sereinflow_get_debug_state`,
`sereinflow_continue_debug`, `sereinflow_step_debug`, and
`sereinflow_stop_debug`. Starting a debug session requires `debug.control` and
an `idempotencyKey`; list, state, and wait reads accept `debug.read` or
`run.read`, while control commands require a strictly increasing
`commandSequence`.

Run queries use `development` or `production` for `track`; topology queries
default to `development` when omitted. The optional `status` filter of
`sereinflow_list_runs` accepts `pending`, `running`, `succeeded`, `failed`,
`cancelled`, `timedOut`, or `interrupted`.

Every submitted run is immutably bound to `(flowId, version, checksum)` from
the persisted definition selected at submission time. Run inspection exposes
`definitionChecksum`. Do not substitute a cached edit model, stale version
resource, or unsaved candidate. Production invocation reloads the persisted
production head; a candidate or override definition is not accepted for a
formal production run. Debug candidates are allowed only when their flow ID
and version match the current persisted development definition; the run binds
to the checksum of that exact candidate. Treat
`run.candidate_definition_not_allowed` and any flow/version/checksum mismatch
as a hard stop and reread the authoritative version resource.

Publishing a message to an active run uses the separate mutation tool
`sereinflow_publish_run_message` and is not part of `debug.control`. The tool
requires the `run.message.publish` permission and a required `idempotencyKey`.
Its `payload` accepts any JSON value, including arrays, strings, numbers,
booleans, and `null`; `channelKind` uses the stable `queue` or `eventBus`
strings and defaults to `queue` when omitted. The tool looks up the active run
by `runId` and enforces the project scope. Ordinary and debug runs therefore
share the same entry point. The tool does not accept `flowId` and does not
reload the flow in response to a message.

```json
{
  "runId": "8e0f2b1e-7e9c-4e8e-b5d0-2f4d8d6f21a8",
  "topic": "order.created",
  "payload": { "orderId": "A10001", "amount": 99.5 },
  "channelKind": "queue",
  "contractId": "order.created.v1",
  "messageId": "af46ef89-5712-4dff-a6df-bf4e57f80a7d",
  "idempotencyKey": "agent-call-20260902-001"
}
```

A successful result is a structured Worker message-receipt result;
`status: "accepted"` means only that the message entered the run-level Broker.
MCP should observe Flipflop successors and the complete flow through run
details, events, outputs, and debug state. MCP idempotency results are persisted
by principal, tool, and request content. Retrying the same `idempotencyKey`
returns the original result without another delivery; the Worker also
deduplicates messages inside the Broker by stable `messageId`. Common business
error codes include `run.not_found`, `worker.not_active`, `worker.not_found`,
`message.endpoint_not_ready`, `message.endpoint_forbidden`,
`message.contract_mismatch`, `message.channel_full`, and
`message.delivery_timeout`. A formal run that attempts to use an unsaved
candidate returns `run.candidate_definition_not_allowed`.

MCP API-key management tools are administrator-only. Use
`sereinflow_list_mcp_api_keys` to read the current state, then call create,
rotate, or revoke tools only when explicitly requested. Mutations require an
`idempotencyKey`. A project-scoped key must bind to an unarchived project; an
administrator key must not bind to a project. A Secret returned by creation or
rotation is shown only once and must not be written to logs, the repository,
Prompts, or unrelated tool arguments. If a response is ambiguous, reread the
key list before retrying; do not blindly repeat the mutation.

When creating a key, `permissions` must use these stable dotted names:
`project.read`, `project.write`, `library.read`, `run.read`, `debug.read`,
`flow.write`, `debug.control`, `flow.publish`, `flow.rollback`,
`script.compile`, `library.import`, `library.manage`, `mcp.keys.manage`,
`sensitive.read`, or `run.message.publish`. A project-scoped key provides
`projectId` and must not set `isAdministrator: true`; an administrator key sets
`isAdministrator: true` and must not provide `projectId`.

Flow changes use `sereinflow_preview_flow_patch` first, followed by the
corresponding apply tool after explicit confirmation. v2 requests use
`schemaVersion: "2.0"`, an `op` discriminator, named payload fields, and
canonical camelCase enum values. New requests must use Schema 2.0; the service
retains legacy v1 input only to read existing callers and persisted previews.
AI clients must not generate v1 requests. Responses always return v2
`normalizedOperations` and `normalizationWarnings`.

When a library node has a required input without a literal default, submitting
`addNode` alone is expected to fail with
`node.missing_required_parameter`. Add the node and all required data
connection(s) in the same ordered patch; the connection operation binds the
target parameter before final validation. Apply only when the combined preview
returns `canApply: true`.

The successful Apply response intentionally redacts parameter literals and
script source. When exact parameter values are needed, reread the authoritative
flow after Apply with `includeFlowLiteralValues: true`.
`updateCanvas` replaces the complete canvas object, so a canvas rename must
preserve its existing nodes and connections in the payload.

The v2 connection payload for `addConnection` and `replaceConnection` must
provide both `branch` and `dataSource`; either may be `null`. `kind` and
`dataSource` are separate enums: an execution connection uses
`kind: "execution"`, `branch: "success"|"failure"|"error"`, and
`dataSource: null`; a data connection uses `kind: "data"`, `branch: null`, and
`dataSource: "previousNode"`. `dataSource` must not be `"execution"`.
For a data connection, `toPortId` must be the target parameter ID rather than
the `param-*` UI port ID.

Library nodes must first use the read-only
`sereinflow_create_library_node_template`. It accepts only a real Action or
Flipflop contract that has been scanned and attached to the project. It returns
a complete canonical `NodeDto`, runtime library metadata, parameter ports,
default literals, enum/variadic metadata, package SHA-256, and
`contractRevision`. Put the returned node unchanged into v2 `addNode`.
Library attach/detach remains a separate preview/apply operation and is not part
of the flow patch.

For built-in `Script` and `FlowCall` nodes, first call the read-only
`sereinflow_create_builtin_node_template` after reading the current flow edit
model. Pass a `builtinNodeId` from the model's built-in-node catalog and a
finite canvas `position`. Put the returned `node` unchanged into an `addNode`
or `replaceNode` operation; it is a complete Schema 2.0 node with execution
ports, data-output metadata, parameters and runtime UI metadata.

FlowCall runtime metadata must preserve `returnType`, `targetFlowId`,
`targetNodeId`, `targetCanvasId`, `isPublic`, and
`flowCallParameterBindings`. A missing binding list is represented by `null`
when the target has no parameters. For library nodes,
`libraryNodeContractId` is the pure node `contractId`; do not send the legacy
`flowLibraryNodeContractId` field or combine a library ID with the node
contract ID.

The flow patch also supports `addNodeParameter` and `removeNodeParameter` for
parameter-level edits. Use `addNodeParameter` with a complete parameter
contract and a unique `ui.id`; this is the MCP operation for adding another
member to a variadic group. Remove incoming data connections before using
`removeNodeParameter`. Operations are ordered within the preview, so a new
parameter can be added before an `addConnection` targets its ID.

When `removeNode` removes the current entry node, the flow patch automatically
clears `entryNodeId` if that ID is absent from the final canvas contents. This
is intentional editor-draft behavior: removing the last node produces an empty
draft that can be saved with `entryNodeId: ""`. If the same patch creates or
retains a node with that ID, the entry reference is preserved. To choose a
different entry node in the same patch, remove the old entry's connections and
node first, then issue `setEntryNode` with the ID of another remaining node.

## Node Layout Constraints

Before drawing, read `sereinflow_get_flow_edit_model`. Keep every node within
the canvas bounds and avoid overlapping existing or new nodes. Move only
affected existing nodes and include coordinate changes in the same preview.
Detailed spacing, branch alignment and connection-routing preferences belong to
the UI editor rather than the MCP contract.

## Security and Diagnostics

Treat a connected SereinFlow service as a black box. Use only MCP error codes,
structured `data`, public Resources, and bounded diagnostics. Do not change the
server's database, library, or staging paths through client parameters or
environment variables. Do not search the service source, read its database, or
decompile uploaded assemblies to explain a remote error.

Unexpected failures return JSON-RPC `InternalError` (`-32603`) with `data.code =
"mcp.internal_error"` and a `diagnosticId`. HTTP tool timeouts return
`mcp.tool_timeout` with the same diagnostic ID. Provide the diagnostic ID to
the service operator.
