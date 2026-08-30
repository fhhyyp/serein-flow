---
name: sereinflow
description: MCP-first SereinFlow entry point. Load the current operating guide from the connected SereinFlow service, discover the live tools and prompts, and route local SereinLang or C# library packaging tasks to their dedicated skills.
---

# SereinFlow

## MCP-first operation

The connected `SereinFlow.Api` MCP service is the authority for common
SereinFlow operations. Do not treat this file as a copy of the service's
operating rules. After connecting, read `sereinflow://ai/guide` through
`resources/read`; the service loads that Markdown at request time, so it may
change without updating this plugin.

Use the URL in the configured MCP server entry. Do not add a database path,
library path, staging path, API key, or other server-local path to client
configuration. A client may operate normally without a SereinFlow checkout or
solution on its machine.

## Discovery order

1. Initialize the MCP session and inspect `initialize.instructions`.
2. Read `sereinflow://ai/guide` for the compact capability index.
3. Read only the capability Resource relevant to the request:
   `sereinflow://ai/skills/sereinflow`,
   `sereinflow://ai/skills/sereinlang`, or
   `sereinflow://ai/skills/sereinflow-library-package`.
4. Call `tools/list`, `resources/list`, and `prompts/list` when their live
   metadata is needed; use the returned names and schemas exactly.
5. For an ambiguous request, perform bounded read-only discovery only.
6. For a mutation, follow the server guide's preview, explicit confirmation,
   apply, and post-apply reread requirements.

The server index and capability Resources are dynamic and take precedence over
remembered tool names or rules in this entry point. Do not load all capability
Resources for an unrelated request. Do not search local source code or server
files to explain a remote MCP result.

## Codex-only routing

Use the sibling skill only when the request enters its local capability:

- SereinLang authoring, repair, explanation, or compilation: load
  `$sereinlang` from `../sereinlang/SKILL.md`.
- Local C# library build and upload ZIP packaging: load
  `$sereinflow-library-package` from `../sereinflow-library-package/SKILL.md`.

The local packaging skill operates on a user-selected local project and local
.NET toolchain. It does not authorize a remote import or flow mutation. Attach
an imported library and edit a flow only through the live MCP preview and
confirmation workflow.

## Remote boundary

Treat the connected service as a black box. For failures, report the returned
MCP error code, diagnostic ID, and bounded safe fields. Do not infer a source
level cause from a generic error, expose secrets or full scripts, or inspect a
fixed repository path. Compilation, package inspection, and previews are
diagnostic results; they are not mutation authorization.
