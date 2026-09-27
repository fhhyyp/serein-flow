# Public interfaces API

A public interface gives a published flow a stable invocation ID. It is managed among environment flow interfaces and points to a project and flow.

## Management

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/environment/interfaces` | List interfaces |
| `POST` | `/api/environment/interfaces` | Create interface |
| `PUT` | `/api/environment/interfaces/{interfaceId}` | Change name, mode, or enabled state |
| `DELETE` | `/api/environment/interfaces/{interfaceId}` | Delete interface |

Creation needs `projectId`, `flowId`, `name`, `invocationMode`, and optional `isEnabled`. Mode is `synchronous` or `asynchronous`. The flow must have a published production version.

```json
{
  "projectId": "<project ID>",
  "flowId": "<flow ID>",
  "name": "Check order status",
  "invocationMode": "asynchronous",
  "isEnabled": true
}
```

## Invoke and query

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/public/flows/{interfaceId}/invoke` | Invoke enabled interface |
| `GET` | `/api/public/tasks/{taskId}` | Read invocation result |

Invocation can include `projectInputs`, `timeoutSeconds`, `maxSteps`, and `maxNodeVisits`. Asynchronous mode returns `202` and a task ID. Synchronous mode returns `200` when complete in time, otherwise `202`. `isCompleted` indicates completion when querying; `flowData` contains exposed node data.

With REST authentication enabled, invocation requires `run.execute` and querying requires `run.read`. See [Public flow interfaces](/en/modules/public-interfaces).
