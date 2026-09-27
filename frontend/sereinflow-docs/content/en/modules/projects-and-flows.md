# Projects and flows

A **project** is a workspace for flows, selected node libraries, and runs. A **flow** stores the canvas, nodes, connections, and entry point that define its execution.

## Why a new project is empty

The console creates a blank **Main flow** canvas with each project. This draft can be saved but cannot run until it has an executable node and an entry point.

## What a flow contains

- **Canvas:** A space for nodes. The main flow is the default; you can add others.
- **Node:** An action such as a script or a call to another canvas.
- **Flow connection:** Sets which node executes next.
- **Data connection:** Passes an upstream value to a downstream parameter.
- **Entry node:** Where a normal flow starts.

Saving the canvas creates a flow definition. `GET /api/projects/{projectId}/flows/{flowId}` returns that definition.

## Which endpoint to use

Use `GET /api/projects` to find project and flow IDs, then `GET /api/projects/{projectId}/flows/{flowId}` for the full definition. The console sends the current version when saving. If someone else saved first, the server returns `409`; reload before merging changes.

Building a definition by hand also means handling nodes, ports, connections, and validation. Start by drawing in the console, then read and run the flow through the API. See [Projects and flows API](/en/api/projects).
