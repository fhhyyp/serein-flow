# Alignment: Blank Project Default

## Original requirement

The Vue workbench must not contain locally preset nodes. A newly created project must be clean.

## Project understanding

On an empty database, the Vue application now stays in an unsaved blank workspace. It does not create a project or inject an order-processing example until the user explicitly saves. The local node catalog is also empty; node definitions are expected to come from the server-side catalog.

## Scope

- Create exactly one empty `Main` canvas for a new workspace.
- Do not create nodes, links, parameters, selected entities, or optional lifecycle canvases.
- Permit an empty entry node while a flow is in editor/persistence state.
- Reject the same draft at the isolated execution-plan boundary.
- Preserve existing user-created nodes, canvases, and graph connections through DTO mapping.

## Boundaries

- No legacy `.dnf` compatibility migration.
- No change to API/Worker isolation, project permissions, or run transport.
- A one-time SQLite migration removes the old `订单处理流程` seed project and its dependent flow records. Other persisted projects remain unchanged.

## Risks and assumptions

- An empty flow must not be represented with a fake or hidden trigger node.
- `FlowDefinition.Validate` is structural editor validation, while `ExecutionPlanBuilder` is the execution admission boundary.
- The user previously authorized autonomous execution, so the Approve step is recorded as accepted without an additional confirmation request.
