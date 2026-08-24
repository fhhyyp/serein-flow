# Alignment: Workbench API and Graph Fixes

## Original request

- The web workbench has no real frontend-to-backend interaction.
- Deleting one connection must not remove unrelated connections.
- Switching canvases must render the selected canvas connections.

## Project understanding

The Vue 3 workbench currently persists its editable workspace only in browser storage. The ASP.NET Core API already owns SQLite initialization and project persistence, but exposes only a health endpoint. The existing database schema already contains `Projects`, `FlowDefinitions`, and `FlowDefinitionVersions`.

Vue Flow receives the active canvas through computed arrays. Its internal synchronization may emit changes while the active canvas changes, so applying every edge change back to the computed source can replace the persisted graph with an empty array.

## Scope

1. Add project and flow-definition read/create/update endpoints backed by SQLite and SqlSugar.
2. Persist a complete `FlowDefinitionDto` JSON document with optimistic version control and immutable version history.
3. Load a server workspace at startup, create a server project only when none exists, and save from the command bar.
4. Keep browser storage only as a recovery draft when the API is unavailable.
5. Make edge deletion scoped to explicit removed edge identifiers and recreate Vue Flow when the active canvas changes.
6. Preserve node UI metadata and the `trigger` node type in API round trips.

## Out of scope

- Authentication, authorization, collaboration, execution orchestration, and SignalR/SSE live events.
- Legacy `.dnf` migration and the retired WPF workbench.
- Replacing the existing visual language; the workbench remains a light, compact Minimalism / Swiss Style editor.

## Assumptions and risks

- `PUT` is the only operation that changes a flow and carries an expected flow version.
- A conflict must not overwrite either copy; the API returns HTTP 409 and the UI asks the editor to reload.
- API validation is structural and must not load scripts, plugins, or worker assemblies.
- The user explicitly authorized autonomous execution, so the Approve phase is recorded as accepted for this bounded implementation.

## Acceptance questions resolved

- The canonical data store is SQLite through the API; `localStorage` is not canonical.
- A fresh browser instance must reopen the last saved server workflow.
- Removing one edge must retain all other edges in the same canvas.
- Switching away from and back to a canvas must retain and display its edges.
