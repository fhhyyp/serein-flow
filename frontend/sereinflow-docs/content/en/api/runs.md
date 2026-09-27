# Runs, events, and workpieces API

One run is one execution. Save the returned `runId` to inspect status, node output, and generated files.

## Current routes

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/projects/{projectId}/flows/{flowId}/runs` | Submit a run; returns `202` |
| `GET` | `/api/runs` | List runs by `status`, `projectId`, and `take` |
| `GET` | `/api/runs/overview` | Queue and recent-run overview |
| `GET` | `/api/runs/{runId}` | Run status |
| `GET` | `/api/runs/{runId}/debug-session` | Associated debug session |
| `GET` | `/api/runs/{runId}/snapshot` | Flow snapshot used for the run |
| `GET` | `/api/runs/{runId}/outputs` | Public node outputs |
| `GET` | `/api/runs/{runId}/events` | Events; `afterSequence` fetches newer ones |
| `GET` | `/api/runs/{runId}/events/stream` | Continuous SSE event stream |
| `POST` | `/api/runs/{runId}/cancel` | Cancel an unfinished run |
| `POST` | `/api/runs/{runId}/interrupt` | Mark an unmanaged run interrupted |
| `POST` | `/api/runs/{runId}/messages/{topic}` | Publish a message to an active run |
| `GET` | `/api/runs/{runId}/workpieces` | List generated files |
| `GET` | `/api/runs/{runId}/workpieces/{workpieceId}` | Read a file; `download=true` downloads it |

## Start request

The body can include `expectedFlowVersion`, `projectInputs`, `timeoutSeconds`, `maxSteps`, and `maxNodeVisits`:

```json
{
  "expectedFlowVersion": null,
  "projectInputs": {},
  "timeoutSeconds": 60,
  "maxSteps": null
}
```

`projectInputs` keys and values follow your flow's input contract. Start with a flow requiring no external input if you are learning. See [Run a flow with the API](/en/guide/run-with-api) for a PowerShell example.

## Completion

After `202`, poll `GET /api/runs/{runId}` until the state leaves `pending` or `running`. `succeeded` means success; inspect `errorSummary` and events after failure.

`/events/stream` stays connected and emits SSE events. It is not listed in OpenAPI. After disconnecting, send the last `Last-Event-ID` to resume.

## Messages and files

For messages, `{topic}` names a topic in the active run. The body contains `payload` and may include `messageId`, `contractId`, and `channelKind`. The flow must allow external ingress. Reuse an `Idempotency-Key` header for safe retries.

List workpieces, then use the returned `workpieceId` to read or download a file. See [Runs and results](/en/modules/runs). `/interrupt` is only for a record that has lost Worker management but still appears running; use `/cancel` for normal cancellation.
