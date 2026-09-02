# SereinFlow MCP Guide

Guide version: 7

This is the MCP entry point for AI guidance. After `initialize`, read this
Resource and then read only the smallest module Resource needed for the
request. Do not load every capability or copy local server files into a
client. Current tool schemas and read Resources are authoritative.

## Route by task

| Request | Resource URI |
| --- | --- |
| Project discovery or inspection | `sereinflow://ai/skills/sereinflow/projects` |
| Flow editing, nodes, connections or scripts | `sereinflow://ai/skills/sereinflow/flows` |
| Runs, execution or debugging | `sereinflow://ai/skills/sereinflow/runtime` |
| Publish or rollback | `sereinflow://ai/skills/sereinflow/release` |
| SereinLang lexical or expression syntax | `sereinflow://ai/skills/sereinlang/syntax` |
| SereinLang imports or host APIs | `sereinflow://ai/skills/sereinlang/host` |
| SereinLang formal grammar | `sereinflow://ai/skills/sereinlang/grammar` |
| C# library build and publish | `sereinflow://ai/skills/sereinflow-library-package/build` |
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
order. For example, library attachment plus flow editing reads `import` and
`flows`; source-to-package work reads `build`, `zip` and `metadata`.

If workflow prompts are preferred, call `prompts/list` and then `prompts/get`.
Each Prompt has a fixed scope; it does not classify free-text intent or add
modules based on the request. `sereinflow.inspect` loads only project discovery
and inspection guidance. `sereinlang.compile` loads only standalone lexical and
expression syntax guidance; imports and host APIs require the `host` module.
`sereinflow.package-library` is source-to-package only and composes build, ZIP
and metadata guidance; preview, import and attachment require the `import`
module. The other Prompts map to their corresponding single focused module. Do
not request unrelated prompts or skills.

## Shared mutation gate

For an explicitly requested mutation, use one task-level authorization:

```text
read current state -> preview requested scope -> inspect diagnostics/diff
-> apply the matching preview with confirmation, fingerprint and idempotency key
-> reread affected resources and verify persisted state
```

Do not ask for duplicate confirmation between dependent calls. Pause once when
the request is ambiguous, the preview is destructive or unexpected, production
state, permissions or secrets change, or a version conflict occurs.

The server reads these files at Resource or Prompt request time. Updating a
module changes guidance for new requests without rebuilding the MCP client.
