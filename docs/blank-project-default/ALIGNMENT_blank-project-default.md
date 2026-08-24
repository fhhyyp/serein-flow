# Alignment: Blank Project Default

## Original requirement

The Vue workbench must not contain locally preset nodes. A newly created project must be clean.

## Project understanding

On an empty database, the Vue application creates a default workspace and persists it through the ASP.NET Core API. The former `createInitialCanvases` function embedded a complete order-processing example in the browser. Its first node also became the `entryNodeId` sent to the API.

## Scope

- Create exactly one empty `Main` canvas for a new workspace.
- Do not create nodes, links, parameters, selected entities, or optional lifecycle canvases.
- Permit an empty entry node while a flow is in editor/persistence state.
- Reject the same draft at the isolated execution-plan boundary.
- Preserve existing user-created nodes, canvases, and graph connections through DTO mapping.

## Boundaries

- No legacy `.dnf` compatibility or data migration.
- No change to the node library, API/Worker isolation, project permissions, or run transport.
- Existing persisted projects remain unchanged; the new default applies to newly initialized workspaces.

## Risks and assumptions

- An empty flow must not be represented with a fake or hidden trigger node.
- `FlowDefinition.Validate` is structural editor validation, while `ExecutionPlanBuilder` is the execution admission boundary.
- The user previously authorized autonomous execution, so the Approve step is recorded as accepted without an additional confirmation request.

