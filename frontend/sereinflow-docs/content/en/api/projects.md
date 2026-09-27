# Projects and flows API

Find a workspace through project routes, then read its canvas through flow routes. A project list item has a `project` and `flows` summaries; those are not full canvas definitions.

## Current routes

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/projects` | List projects; `includeArchived=true` includes archived ones |
| `POST` | `/api/projects` | Create a project with a name and initial flow definition |
| `PUT` | `/api/projects/{projectId}` | Rename a project |
| `POST` | `/api/projects/{projectId}/archive` | Archive a project |
| `GET` | `/api/projects/{projectId}/flows/{flowId}` | Read a full flow definition |
| `PUT` | `/api/projects/{projectId}/flows/{flowId}` | Save a flow definition |

## Read a flow

```powershell
$items = Invoke-RestMethod 'http://localhost:8188/api/projects'
$projectId = $items[0].project.id
$flowId = $items[0].flows[0].id
$flow = Invoke-RestMethod "http://localhost:8188/api/projects/$projectId/flows/$flowId"
$flow
```

The list can be empty; create a project with [Your first flow](/en/guide/first-flow) first. IDs uniquely identify each project and flow.

## Writing

Project creation needs `{ "name": "...", "definition": { ... } }`; `definition` must contain the complete flow, not just its name. For a first project, create it in the console, then read the definition with GET.

Renaming needs `name` and `expectedVersion`. Saving needs `expectedVersion` and a complete `definition`, with `definition.id` matching the path's `flowId`. If another writer changed the version, the server returns `409`; reload and merge changes.

See [Projects and flows](/en/modules/projects-and-flows) for the model.
