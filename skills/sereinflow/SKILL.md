---
name: sereinflow
description: Safely inspect, diagnose, modify, publish, rollback, and import SereinFlow projects through the SereinFlow MCP tools. Use when an AI needs to read flow topology or run state, create a structured flow patch, preview and apply a mutation, manage development or production versions, attach a library, or submit a prebuilt C# library ZIP.
---

# SereinFlow

Use the SereinFlow MCP server as the source of truth for project, flow, run,
debug, library, version, and preview state. Keep every response bounded and
avoid requesting sensitive source or values unless the task and permission
explicitly require them.

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

Represent changes only with the typed operations accepted by
`sereinflow_preview_flow_patch`:

- `add_canvas`, `update_canvas`, `remove_canvas`
- `add_node`, `replace_node`, `remove_node`
- `set_node_parameter`
- `add_connection`, `replace_connection`, `remove_connection`
- `set_entry_node`, `set_run_policy`, `replace_script_source`

All newly added canvases, nodes, and connections must have explicit stable
IDs. Remove a node only after explicitly removing its connections. Handle all
contents before removing a canvas. Change only known fields in the structured
contract; never submit arbitrary JSON paths or an untyped document merge.
Keep the expected development version from the latest read model in the
preview request.

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
`[LibraryName]-[Version].zip`, containing a matching
`[LibraryName].dll`. The user or local VS/.NET toolchain must create the C#
source, project, DLL, and ZIP. Do not ask the SereinFlow server to run
`dotnet build`, MSBuild, a `.csproj`, `.props`, `.targets`, or any build
script, and do not submit a server-local path over remote HTTP.

Use `sereinflow_preview_library_package` first. Review ZIP and DLL hashes,
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

## Errors And Audit

For authorization, preview, version, validation, or resource errors, report
the stable MCP error code and stop. Do not retry a rejected mutation blindly.
Expect mutation operations to be audited with principal, project, flow,
track, version, tool, preview, idempotency digest, outcome, size, and
duration, while secrets, full scripts, sensitive literals, and full stacks
remain redacted.
