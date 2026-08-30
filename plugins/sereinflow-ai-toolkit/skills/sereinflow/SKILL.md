---
name: sereinflow
description: Connect to the SereinFlow MCP service and load its current operating guide and live capabilities.
---

# SereinFlow MCP

Use the configured `sereinflow` MCP server as the only source of truth for
SereinFlow projects, flows, runs, debugging, versions, SereinLang compilation,
and library package workflows.

## Declared MCP capabilities

The connected server currently declares and implements these protocol
capabilities:

- `tools`: discover with `tools/list` and invoke with `tools/call`.
- `resources`: discover fixed resources with `resources/list`, URI templates
  with `resources/templates/list`, and content with `resources/read`.
- `prompts`: discover workflow prompts with `prompts/list` and retrieve one
  with `prompts/get`.

The server declares `resources.subscribe: false` and
`listChanged: false` for resources, tools, and prompts. Do not wait for
subscription updates or list-change notifications; discover the current
catalog when needed. The plugin manifest's `Interactive` and `Write` values
are UI metadata, not MCP protocol capability declarations.

At the beginning of a task:

1. Read `initialize.instructions` from the MCP server.
2. Read the `sereinflow://ai/guide` resource.
3. Read only the relevant capability Resource:
   `sereinflow://ai/skills/sereinflow`,
   `sereinflow://ai/skills/sereinlang`, or
   `sereinflow://ai/skills/sereinflow-library-package`.
4. Discover `tools/list`, `resources/list`, and `prompts/list` as needed.
5. Follow the current server-provided guide and the schemas returned by the
   server. Do not rely on copied local SereinFlow source, database paths,
   library directories, or hardcoded workflow rules.

The server index is dynamic and only routes the request to the relevant
capability Resource. Read only the selected capability Resource; do not load
all SereinFlow, SereinLang, and library rules for every task. The corresponding
MCP tools are the live implementation of those capabilities. Never assume
that this plugin contains the server source code or a local copy of the
detailed Skills.

Treat all write-capable operations as preview -> inspect -> explicit user
confirmation -> apply -> reread. A successful read, compilation, package
scan, or preview is not permission to mutate state.
