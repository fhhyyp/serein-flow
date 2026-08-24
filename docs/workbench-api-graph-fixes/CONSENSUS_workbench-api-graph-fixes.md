# Consensus: Workbench API and Graph Fixes

## Accepted requirements

The workbench shall communicate with an ASP.NET Core API to create, load, and save one flow definition per project. SQLite is authoritative. The API must use optimistic concurrency so a stale browser cannot silently overwrite a newer flow.

The editor shall retain separate node and connection collections for each lifecycle canvas. It shall only mutate stored edges when Vue Flow explicitly reports an edge removal that belongs to the active canvas.

## Acceptance criteria

1. `GET /api/projects` lists stored projects.
2. `POST /api/projects` creates a project and its first flow version.
3. `GET /api/projects/{projectId}/flows/{flowId}` returns a complete flow document.
4. `PUT /api/projects/{projectId}/flows/{flowId}` updates the document when `expectedVersion` matches and returns `409 Conflict` otherwise.
5. Nodes, ports, parameters, UI labels, script data, execution connections, and data connections survive a JSON/database round trip.
6. A browser startup loads an existing server flow, or creates and saves the default flow once when none exists.
7. The save command writes to the API, exposes loading/error/conflict state, and only marks the workspace clean after server acknowledgement.
8. Removing one edge leaves all other edges intact; switching canvases preserves and displays the edges belonging to each canvas.

## Technical constraints

- `net10.0`, ASP.NET Core Minimal API, SQLite, and SqlSugar.
- API must not reference Runtime, ScriptAdapter, ScriptLang, or untrusted worker code.
- Vue 3, TypeScript, Vite, and `@vue-flow/core` remain in use.
- UI remains accessible, light, restrained, and uses the existing blue action color with unambiguous connection semantics.
