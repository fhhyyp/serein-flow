# SereinFlow MCP Guide

Guide version: 12

This is the MCP entry point for AI guidance. After `initialize`, read this
Resource and then read only the smallest module Resource needed for the
request. Do not load every capability or copy local server files into a
client. Current tool schemas and read Resources are authoritative.

## Route by task

| Request | Resource URI |
| --- | --- |
| Project discovery, creation or inspection | `sereinflow://ai/skills/sereinflow/projects` |
| Flow editing, nodes, connections or scripts | `sereinflow://ai/skills/sereinflow/flows` |
| Flow canvas layout and UI/UX | `sereinflow://ai/skills/sereinflow/ui-ux` |
| Runs, debugging, run messages or variables | `sereinflow://ai/skills/sereinflow/runtime` |
| Run images, files or other uploaded workpieces | `sereinflow://ai/skills/sereinflow/workpieces` |
| Optional HTTP API-key management (no MCP key tools) | `sereinflow://ai/skills/sereinflow/api-keys` |
| Publish or rollback | `sereinflow://ai/skills/sereinflow/release` |
| SereinLang lexical or expression syntax | `sereinflow://ai/skills/sereinlang/syntax` |
| SereinLang imports or host APIs | `sereinflow://ai/skills/sereinlang/host` |
| SereinLang formal grammar | `sereinflow://ai/skills/sereinlang/grammar` |
| C# library build and ZIP packaging | `sereinflow://ai/skills/sereinflow-library-package/build` |
| Library ZIP contract | `sereinflow://ai/skills/sereinflow-library-package/zip` |
| Library SDK metadata | `sereinflow://ai/skills/sereinflow-library-package/metadata` |
| Library preview, import or attachment | `sereinflow://ai/skills/sereinflow-library-package/import` |
| Library family or project upgrade | `sereinflow://ai/skills/sereinflow-library-package/upgrade` |

The three capability index Resources remain available for clients that need a
second-level route:

- `sereinflow://ai/skills/sereinflow`
- `sereinflow://ai/skills/sereinlang`
- `sereinflow://ai/skills/sereinflow-library-package`

A request spanning capabilities may read the smallest set of modules in task
order. For example, flow editing with canvas layout reads `flows` and `ui-ux`;
library attachment plus flow editing reads `import` and `flows`; source-to-package
work reads `build`, `zip` and `metadata`.

If workflow prompts are preferred, call `prompts/list` and then `prompts/get`.
Each Prompt has a fixed scope; it does not classify free-text intent or add
modules based on the request. `sereinflow.inspect` loads only project discovery
and inspection guidance. `sereinlang.compile` loads only standalone lexical and
expression syntax guidance; imports and host APIs require the `host` module.
`sereinflow.package-library` is source-to-package only and composes build, ZIP
and metadata guidance; preview, import and attachment require the `import`
module. `sereinflow.edit-flow` combines `flows` and `ui-ux`;
`sereinflow.debug-run` combines `runtime` and `workpieces`. Other Prompts load
their declared module sets. Do not request unrelated prompts or skills.

## Access and mutation contracts

HTTP and MCP allow direct access by default. An application developer can
enable API-key or host authentication; permission names in these skills
describe those configured policies. Direct access does not remove validation,
preview confirmation, idempotency or runtime command ordering requirements.

For project creation, flow edits, releases, library import, attachment, family
assignment and upgrades, use the preview/apply contract:

```text
read current state -> preview requested scope -> inspect diagnostics/diff
-> apply the matching preview with confirmation, fingerprint and idempotency key
-> reread affected resources and verify persisted state
```

Run-message publication, variable writes and debug start are direct calls
with an `idempotencyKey`. Debug controls use `commandSequence`. They have no
preview/apply phase. API-key management belongs to the optional HTTP package;
MCP exposes guidance for it, not key-management tools.

The user's request authorizes its stated task and dependent calls. Do not ask
for duplicate confirmation. Clarify scope when it is ambiguous or exceeds the
request, and stop on a conflict or unexpected destructive, release, permission
or secret change that the user has not already authorized.

The server reads configured disk overrides at Resource or Prompt request time,
falling back to embedded guidance only when no override file exists. Deploy
updated override files as well as embedded resources; a stale disk file wins
over a newer assembly. Clients must reread guidance to see updates, but do not
need a rebuilt MCP client. If initialize or resources/list returns customized
guide/module URIs, follow those returned URIs instead of assuming these defaults.
