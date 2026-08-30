---
name: sereinflow
description: Primary SereinFlow AI entry point and capability router. Safely inspect, diagnose, modify, publish, rollback, and import SereinFlow projects through MCP tools, and route SereinLang authoring or C# library ZIP packaging to the sibling skills. Use when a request mentions SereinFlow, flows, debugging, runs, versions, publishing, libraries, scripts, or is ambiguous and needs a safe next-step recommendation.
---

# SereinFlow

## Entry Point And Routing

Load this file first when a host only knows the repository Skill entry point.
Use the sibling Skill files only when the request enters their capability:

| Request intent | Skill to load | First action |
| --- | --- | --- |
| Projects, flows, nodes, runs, debugging, versions, publishing, rollback, MCP resources or tools | `$sereinflow` (this file) | Read state through the connected MCP server |
| Write, explain, repair, or compile a SereinLang script | `$sereinlang` at `../sereinlang/SKILL.md` | Read the target script node contract and its syntax guide |
| Build a C# library and create the upload ZIP | `$sereinflow-library-package` at `../sereinflow-library-package/SKILL.md` | Discover the `.csproj` and evaluate its MSBuild metadata |

For Codex, invoke the named Skill with `$sereinflow`, `$sereinlang`, or
`$sereinflow-library-package`. For OpenCode or another host without Skill
aliases, resolve the paths relative to this file under `skills/` and load only
the selected sibling `SKILL.md`; then load its directly linked references or
scripts when the task requires them. Do not load every Skill for a request
that belongs to one capability.

### Resolve Ambiguous Requests

When the user does not identify a project, flow, node, version, or operation,
do not guess and do not perform a mutation. Classify the request from its
strongest terms, perform only bounded read-only discovery, and offer the
smallest useful next choices:

- “流程”“节点”“运行”“调试” -> inspect projects, flows, or run/debug state;
- “改流程”“增加节点”“修改脚本” -> read the flow edit model, then propose a
  structured patch without applying it;
- “发布”“上线”“生产”“回滚” -> read development/production heads and
  version history, then offer preview publish or preview rollback;
- “类库”“DLL”“ZIP”“打包” -> load `$sereinflow-library-package` and inspect
  the local project or existing artifact; never ask for a server-side build;
- “SereinLang”“脚本”“语法”“编译” -> load `$sereinlang` and inspect the
  script node input/output contract;
- mixed or unclear terms -> show the detected intent, state the missing
  identifier or choice, and recommend one read-only inspection command.

Use this compact recommendation shape when clarification is needed:

```text
我将其识别为：<能力类别>。
建议下一步：<只读检查或加载的 Skill>。
还需要：<项目/流程/节点/轨道/目标版本中的必要缺口>。
执行边界：<不会写入、发布、回滚或导入，直到预览并获得明确确认>。
```

If the request contains both script and flow-change intent, load `$sereinlang`
for syntax and compiler diagnostics first, then return to this Skill for
`replace_script_source` preview and apply. If it contains both DLL and flow
intent, load `$sereinflow-library-package` to produce and validate the local
ZIP first; attaching the artifact and changing a flow remain separate
confirmed operations.

### Mutation Gate

Keep the following state machine for every write-capable request:

```text
ambiguous request
    -> bounded read-only discovery
    -> selected capability Skill
    -> preview and diagnostics
    -> show diff/impact and request explicit confirmation
    -> apply with preview fingerprint and fresh idempotency key
    -> reread the affected resource and report the new state
```

Never interpret “帮我处理”“修一下”“上线一下” or a successful compile as
confirmation to write, publish, roll back, attach, or import. A compiler result
is diagnostic-only; a local DLL/ZIP result is not an import authorization.

Use the SereinFlow MCP server as the source of truth for project, flow, run,
debug, library, version, and preview state. Keep every response bounded and
avoid requesting sensitive source or values unless the task and permission
explicitly require them.

### Production Black-Box Boundary

Treat the connected SereinFlow service as a remote production black box. The
client may be running in Codex, OpenCode, Claude Code, or another host that
does not have the SereinFlow repository, solution, source tree, database, or
server-local package directory. Never search for `SereinFlow.sln`, browse
server source code, inspect server-local files, or decompile uploaded binaries
to diagnose an MCP or production-run error.
The presence of a similarly named local checkout, a fixed development path, or
an available `cwd` does not establish that it is the service being diagnosed.

When an MCP operation fails, use only the returned MCP error code, stable
machine-readable error data, bounded diagnostics, and the affected resource's
public read model. Do not invent a source-level cause from a generic message.
For an insufficient diagnostic, report the exact tool, error code, message,
and safe diagnostic fields, then request the server operator's correlated
server log or a user-provided correction. Do not substitute a local source
search for a missing production diagnostic.

Local source and project inspection is permitted only for an explicitly local
authoring or packaging task, and only for the user-selected project path. It
must not be used to explain a remote service error. A fixed development path
such as `D:\Project\dotnet\SereinFlow` is never a production prerequisite.

Preview IDs are transaction-scoped handles, not proof that a project, flow, or
library already exists. Do not pass a project or library preview ID into a
later attach or read operation; apply the preview first, reread the real
resource, and use the returned persisted ID. The preview resource itself is
readable for diagnostics but remains `isPreviewOnly: true`.

## Create A Project

When no suitable project exists, use `sereinflow_preview_create_project` with
the requested project name and optional main-flow name. The preview generates
the project and flow IDs and creates a Draft project with an empty `main`
canvas. Show those IDs and the validation result, then apply only after the
user explicitly confirms with `APPLY` through
`sereinflow_apply_create_project`. Project creation is administrator-only and
requires `project.write`; a project-scoped key cannot create a project outside
its own scope. After creation, reread the project, attach libraries through
their separate preview/apply pair, and use the flow edit model before drawing
the flow.

## Operating Rules

- Authenticate with the configured MCP API key and work only within the
  projects granted to that key.
- Read the project and flow before proposing a change. Do not infer IDs,
  ports, parameter names, library versions, or entry nodes from prose.
- Treat development and production as separate tracks. A production head is
  the only version eligible for environment execution.
- Use the exact resource and tool names exposed by the connected server. Do
  not manufacture an HTTP endpoint or write the database directly.
- Treat every mutation as a two-phase operation: preview, show the result and
  obtain explicit user confirmation, then apply with the same preview
  fingerprint and a fresh idempotency key.
- Never retry an apply with a new payload under an existing idempotency key.
  Reuse the original request to obtain an idempotent replay, or use a new key
  after creating a new preview.
- Never expose an API key secret in logs, prompts, audit summaries, or flow
  content. The secret is returned only when a key is created or rotated.

## Inspect Before Acting

Start with `sereinflow_list_projects` or the `sereinflow://projects` resource.
Read the selected project, its flow summaries, and the selected flow's
topology. Read `sereinflow://libraries` and the referenced
`sereinflow://libraries/{libraryId}` resources when a node contract is
involved. Use `sereinflow_get_flow_edit_model` for the complete development
editing model, including canvases, positions, node ports, parameters, script
contracts, entry node, run policy, and library versions.

For execution analysis, use `sereinflow_get_run_inspection` or
`sereinflow://runs/{runId}` and `sereinflow_get_debug_state` or
`sereinflow://debug-sessions/{sessionId}`. Inspect the bounded timeline,
structured node state, inputs, outputs, diagnostics, and state revision. Use
`sereinflow_wait_debug_state` when waiting for a later revision or terminal
state. Do not claim a node completed from a stale snapshot.

## Change A Flow

For every new request to `sereinflow_preview_flow_patch`, use the v2 envelope:
`schemaVersion: "2.0"`, a camelCase `op` discriminator, and the named typed
payload belonging to that operation. New requests must never use the legacy
`operation` / `value` envelope. Version 1 (an omitted or `"1.0"`
`schemaVersion`) is compatibility input only; preserve a returned v2
`normalizedOperations` array as-is and do not reconstruct DTOs from a legacy
payload.

The allowed v2 `op` values are:

- `addCanvas`, `updateCanvas`, `removeCanvas`
- `addNode`, `replaceNode`, `removeNode`
- `setNodeParameter`
- `addConnection`, `replaceConnection`, `removeConnection`
- `setEntryNode`, `setRunPolicy`, `replaceScriptSource`

For example, `setRunPolicy` carries a `runPolicy` object, while `addNode`
carries a complete `node` object; do not put either value inside a generic
wrapper:

```json
{
  "projectId": "<project-id>",
  "flowId": "<flow-id>",
  "expectedDevelopmentVersion": 12,
  "schemaVersion": "2.0",
  "operations": [
    {
      "op": "setRunPolicy",
      "runPolicy": { "concurrencyMode": "exclusiveReject" }
    }
  ]
}
```

All enum values in v2 are canonical camelCase strings. Treat
`schemaVersion: "2.0"`, `enumEncoding: "camelCase"`,
`normalizedOperations`, and `normalizationWarnings` in the preview response
as the authoritative client contract. A field, discriminator, enum, ID, or
reference diagnostic is a hard stop: report its stable `mcp.flow_patch.*`
code with `diagnosticId`, `fieldPath`, `expected`, and remediation; do not
retry with guessed casing or a reconstructed payload.

All newly added canvases, nodes, and connections must have explicit stable
IDs. Remove a node only after explicitly removing its connections. Handle all
contents before removing a canvas. Change only known fields in the structured
contract; never submit arbitrary JSON paths or an untyped document merge.
Keep the expected development version from the latest read model in the
preview request.

### Node Layout Constraints

Before drawing or placing nodes, call `sereinflow_get_flow_edit_model` and use
the returned canvas bounds, node positions, node dimensions, ports, and
connections as the layout baseline. Never guess coordinates from node names or
place several nodes at the same default position.

Every node must have a non-overlapping bounding box. Keep a clear safety gap
between each new node, every existing node, the canvas boundary, and other new
nodes. Calculate horizontal steps from the actual node width; for the usual
approximately `260 px` node width, use roughly `340-420 px` between columns,
and increase the step for nodes with many parameters or wider UI content. As a
baseline, keep at least `80 px` of horizontal clearance between adjacent nodes
and at least `64 px` between branch rows. Enlarge the canvas when required;
do not shrink nodes to make a crowded layout fit.

Lay out the main execution path from left to right, keeping nodes in the same
execution stage on a common `y` coordinate. Put success, failure, and error
branches on separate rows, expanding them to the right of their parent. When
multiple outputs share a parent, order their rows deterministically and leave
enough right-side space for the parent's fan-out. Reserve left-side space for
high-merge nodes and group large flows into execution-stage columns or lanes.

Keep execution connections short, directional, and with as few crossings as
possible. Route data connections so they do not pass through node bodies or
port lists; when a cross-stage route is unavoidable, use a dedicated channel
above or below the nodes. Do not solve connection collisions by moving nodes
closer together.

When changing an existing flow, do not rearrange all existing nodes without a
clear reason. Move only the affected nodes when a new node would overlap an
existing node or make a connection unreadable, and include those coordinate
changes in the same preview. Before presenting the preview, check every node
bounding box for overlap and canvas overflow, then check connection crossings
and routes through node bodies. A layout is not ready when nodes are merely
valid; it must remain readable at the returned node dimensions.

Present the preview's validation diagnostics and structured diff before
applying it. Check the candidate checksum, affected node and library counts,
script hash changes, and any sensitive-path redaction. Apply only when the
user explicitly confirms with `APPLY`, passing `previewId`,
`previewFingerprint`, and a unique `idempotencyKey`. A stale version,
changed fingerprint, changed caller, expired preview, or validation error is a
hard stop; reread state and create a new preview.

Do not create a version for an empty or normalized no-op patch. After a
successful apply, reread the development version and topology and report the
new version and checksum.

Treat an apply response as an acknowledgement, not the authoritative resource
projection. The server may redact literal values and script source in that
response. Completion requires a successful reread of the affected resource and
matching version/checksum/counts; if the reread does not match, report
`mcp.post_apply_verification_failed` and stop.

## Publish Or Roll Back

Use `sereinflow_preview_publish_flow` and
`sereinflow_apply_publish_flow` to publish the current validated development
head. Show the target production version, current production head, execution
readiness, library impact, and environment impact before confirmation.

Use `sereinflow_preview_rollback_flow` and
`sereinflow_apply_rollback_flow` for an explicit historical target. Always
provide `track` as `development` or `production`, the target source version,
and the current expected head version. A production rollback changes only the
production track; a development rollback never publishes automatically.
History is immutable and the newly created rollback or publish version keeps
the monotonic version counter. Use the version resources to verify the result:

- `sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}`
- `sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}`

Use `sereinflow_compare_flow_versions` when the user asks what changed. Do
not describe two versions as identical without comparing their checksums and
structured changes.

## Libraries

The server accepts only a completed ZIP named
`[LibraryName]-[Version].zip`, containing a matching main
`[LibraryName].dll` and the complete `dotnet publish` runtime closure. The
user or local VS/.NET toolchain must create the C# source, project, published
output, and ZIP. Preserve dependency-relative paths so Worker
`AssemblyDependencyResolver` can resolve managed and native assets. Do not ask
the SereinFlow server to run
`dotnet build`, MSBuild, a `.csproj`, `.props`, `.targets`, or any build
script, and do not submit a server-local path over remote HTTP.

New node libraries must reference the standalone `SereinFlow.Library` NuGet
SDK for `SereinFlow.Core.Api` metadata attributes and the restricted
`SereinFlow.Runtime.Abstractions.IFlowContext`. Do not define local
`DynamicFlow`/`NodeAction` compatibility attributes or copy the SereinFlow
contracts into the library. Read `$sereinflow-library-package` for the exact
project, metadata, and ZIP rules.

Use `sereinflow_preview_library_package` first. Review ZIP and main-DLL hashes,
publish dependencies and native assets,
PE scan diagnostics, FlowLibrary/FlowNode/NodeParam contracts, stable ID
confidence, compatibility analysis, duplicate IDs, Flipflop return type,
and project impact. The scan must not load or execute the uploaded assembly.
After explicit confirmation, apply with
`sereinflow_apply_library_package` and a unique idempotency key. Import does
not attach the library to a project automatically.

Attach an existing immutable artifact only through
`sereinflow_preview_project_library_attach` followed by
`sereinflow_apply_project_library_attach`. Treat this as a separate confirmed
mutation because it changes the set of contracts available to a project.

To place an Action or Flipflop class-library node after a library is attached,
first call the read-only `sereinflow_create_library_node_template` with the
persisted `projectId`, `libraryId`, `libraryNodeContractId`, and
`position: { x, y }`. It requires `project.read` and an already attached,
scanned library; it does not create a preview, flow version, or resource.
Use the returned canonical `node` unchanged as the `node` payload of a v2
`addNode` operation for a canvas already known from the flow edit model. Do
not manually assemble runtime library metadata, parameter ports, parameter
IDs, default literals, enum/variadic data, or execution ports. Record the
returned library version/package SHA-256 and `contractRevision` alongside the
proposed patch for review. Library attachment itself remains the separate
preview/apply operation above; it is never represented in a flow patch.

## Errors And Audit

For authorization, preview, version, validation, or resource errors, report
the stable MCP error code and stop. Do not retry a rejected mutation blindly,
and do not browse local or server source code to explain it. For a transient
read-only transport failure, one bounded retry is acceptable; for apply,
reuse the original idempotency key or create a fresh preview instead of
guessing whether the mutation succeeded.
Expect mutation operations to be audited with principal, project, flow,
track, version, tool, preview, idempotency digest, outcome, size, and
duration, while secrets, full scripts, sensitive literals, and full stacks
remain redacted.
