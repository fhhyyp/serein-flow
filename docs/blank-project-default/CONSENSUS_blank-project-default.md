# Consensus: Blank Project Default

## Requirement

A fresh Vue project contains a single empty `Main` canvas. Users add all nodes and connections themselves from the editor's existing tools.

## Acceptance criteria

- `createInitialCanvases()` returns one canvas: `id: main`, `lifecycle: main`.
- The canvas has empty `nodes` and `edges`, with no selected node or edge.
- A new workspace serializes as one empty canvas with `entryNodeId: ""`.
- API structural validation accepts and persists that editor draft.
- An execution request for that draft raises `DomainValidationException` with `flow.unknown_entry_node` at `entryNodeId`.
- A non-empty, user-created multi-canvas graph still round-trips without losing execution or data links.

## Technical solution

- Replace the browser fixture factory with a literal empty `Main` canvas.
- Normalize a null or blank entry ID to an empty string in `FlowDefinition.Create`.
- Do not issue unknown-entry diagnostics during editor validation for an empty entry ID.
- Add the unknown-entry diagnostic in `ExecutionPlanBuilder.Build` before any runner accesses the plan's node dictionary.

## Constraints

- `net10.0`, Vue 3 + TypeScript, ASP.NET Core, SQLite/SqlSugar remain unchanged.
- The API does not load scripts, plugins, runtime assemblies, or external DLLs.
- The application retains its existing restrained Minimalism / Swiss Style canvas: neutral white work area, no sample-card onboarding, and no decorative empty-state content.

