# SereinFlow Library Capability Index

This Resource routes library-package work. Load only the modules needed for
the request. The MCP service is a remote preview/import boundary and does not
build C# source or execute caller files.

| Work | Resource URI |
| --- | --- |
| Local C# build and publish | `sereinflow://ai/skills/sereinflow-library-package/build` |
| ZIP layout and validation | `sereinflow://ai/skills/sereinflow-library-package/zip` |
| SDK metadata and node contracts | `sereinflow://ai/skills/sereinflow-library-package/metadata` |
| Preview, import and project attachment | `sereinflow://ai/skills/sereinflow-library-package/import` |
| Library families and project upgrades | `sereinflow://ai/skills/sereinflow-library-package/upgrade` |

Typical routing:

- Source-to-package work reads `build`, `zip` and `metadata`.
- Upload or attachment reads `import` and the modules needed to produce the
  package.
- A family assignment or project upgrade reads only `upgrade`.

Do not infer compatibility from a local ZIP alone. The package preview and
current MCP schemas are authoritative.
