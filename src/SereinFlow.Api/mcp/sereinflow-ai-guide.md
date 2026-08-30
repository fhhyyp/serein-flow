# SereinFlow MCP AI Guide Index

Guide version: 3

This is a compact routing index. Load only the capability Resource needed for
the current request:

| Capability | Resource URI | Use for |
| --- | --- | --- |
| `sereinflow` | `sereinflow://ai/skills/sereinflow` | Projects, flows, nodes, runs, debugging, versions, publishing, rollback and flow mutation previews |
| `sereinlang` | `sereinflow://ai/skills/sereinlang` | SereinLang authoring, syntax and diagnostic compilation |
| `sereinflow-library-package` | `sereinflow://ai/skills/sereinflow-library-package` | Local C# publish/ZIP contract, package inspection and library attachment |

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

All write-capable operations use this gate:

```text
read current state -> preview -> inspect diagnostics and diff
-> obtain explicit user confirmation -> apply with the preview fingerprint
-> reread the affected resource and verify the result
```

The server reads this index and each capability file from its deployment at
Resource or Prompt request time. Updating a Markdown file changes the guidance
for new requests without embedding the content in MCP client code.
