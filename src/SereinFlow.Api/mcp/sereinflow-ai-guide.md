# SereinFlow MCP AI Guide Index

Guide version: 5

This is a compact routing index. Load only the capability Resource needed for
the current request:

| Capability | Resource URI | Use for |
| --- | --- | --- |
| `sereinflow` | `sereinflow://ai/skills/sereinflow` | Projects, flows, nodes, runs, debugging, versions, publishing, rollback and flow mutation previews |
| `sereinlang` | `sereinflow://ai/skills/sereinlang` | SereinLang authoring, syntax and diagnostic compilation |
| `sereinflow-library-package` | `sereinflow://ai/skills/sereinflow-library-package` | C# library authoring with `SereinFlow.Library` from NuGet.org, local publish/ZIP contract, package inspection and library attachment |

The current MCP tool and resource schemas are authoritative. Discover them
with `tools/list`, `resources/list`, and `prompts/list`; do not copy server
source code, database paths, library directories, uploaded binaries or local
repository assumptions into a client.

Use this routing rule:

- Project, flow, runtime or release request: read the `sereinflow` Resource.
- Script syntax or compilation request: read only the `sereinlang` Resource.
- C# library, DLL, ZIP or attachment request: read only the
  `sereinflow-library-package` Resource.
- A request spanning capabilities may read the smallest set of listed
  Resources needed, in the order implied by the task.

## Task-level write authorization

For an explicit user request to create a project, edit a flow, or upload/import
a library, treat the request as authorization for that named logical task and
the dependent steps needed to complete it. Do not ask for a separate approval
after every preview or between dependent MCP calls.

Use this compact gate:

```text
read current state -> preview the logical change or dependent batch
-> inspect diagnostics and diff -> apply each matching preview
-> reread affected resources and verify versions/checksums/counts
```

The apply tools still require their protocol fields, including
`confirmation: "APPLY"`, the preview fingerprint, and an idempotency key. Once
the user's request authorizes the scoped task and the preview matches it, fill
those fields without asking the user to repeat the same confirmation. For a
single request that includes library import, project attachment, and flow
editing, treat those dependent writes as one task-level authorization.

Pause and ask one concise question only when the request is ambiguous, the
preview contains destructive or unexpected changes, the operation affects
production publication or rollback, changes permissions or secrets, encounters
a version conflict, or requires a materially different operation. A preview is
never permission to perform an operation outside the user's requested scope.

The server reads this index and each capability file from its deployment at
Resource or Prompt request time. Updating a Markdown file changes the guidance
for new requests without embedding the content in MCP client code.
