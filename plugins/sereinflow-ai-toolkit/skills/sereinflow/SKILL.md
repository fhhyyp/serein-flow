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

The HTTP connection obtains its bearer token only from the
`SEREINFLOW_MCP_API_KEY` environment variable. Never put an API key in the
plugin, repository, task text, tool arguments, or client-local project files.
If initialization cannot authenticate, report that the SereinFlow MCP API key
is absent, invalid, expired, or revoked; do not treat it as a missing Skill or
attempt to bypass server authentication.

## Configure the client key

The SereinFlow Web Console can generate and persist MCP keys. Open the
environment settings, use the local-only first-key setup when no key exists,
then create a project-scoped client key with the smallest required permissions.
The complete `sfk_...` Secret is returned only once after creation or rotation.

In the Codex MCP credential configuration, set the variable named
`SEREINFLOW_MCP_API_KEY` to that complete Secret. The variable name is already
declared by this plugin; the plugin never contains the Secret value. A browser
cannot change the environment of an already running Codex process, so the
Secret must be entered through the client's supported credential UI or secure
environment/secret injection mechanism. Use the server bootstrap setting only
for first-time recovery when the Web Console is unavailable, and remove it
after a dedicated client key has been created.

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

## Task-level write authorization

For an explicit request to create a project, edit a flow, or upload or import a
library, treat the initial request as authorization for that named logical task
and its dependent MCP calls. Use `read -> preview -> inspect -> apply ->
reread`, but do not ask for a separate confirmation after every preview or
between dependent writes. A single request that includes library import,
project attachment, and flow editing uses one authorization for the whole chain.

The apply tools still require `confirmation: "APPLY"`, the preview fingerprint,
and an idempotency key; fill those protocol fields without prompting again once
the preview matches the user's requested scope.

Pause for one concise confirmation only when the request is ambiguous, the
preview reveals destructive or unexpected scope, the operation publishes or
rolls back production, changes permissions or secrets, encounters a version
conflict, or would perform a materially different operation. A preview is not
authorization for work outside the requested scope. Read-only inspection,
compilation, package scanning, and post-apply rereads do not require a prompt.
