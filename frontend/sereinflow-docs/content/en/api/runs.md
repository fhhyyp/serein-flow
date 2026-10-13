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
| `GET` | `/api/runs/{runId}/variables?offset=0&take=100` | Read active-run variables |
| `GET` | `/api/runs/{runId}/variables/{name}` | Read one variable |
| `PUT` | `/api/runs/{runId}/variables/{name}` | Set a variable with run.variable.write |
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
## Flow variables

`GET /api/runs/{runId}/variables?offset=0&take=100` reads a sorted page; `GET /api/runs/{runId}/variables/{name}` reads one variable. Both require project-scoped `run.read`. `PUT /api/runs/{runId}/variables/{name}` requires `run.variable.write` and an explicit body:

```json
{ "value": 42, "valueType": "int32" }
```

Supported types are `null`, `boolean`, `string`, `int32`, `int64`, `double`, `decimal`, and `json`. An `int32` is stored as `System.Int32`, so nodes can use `Get<int>(name)`. A `json` value is stored as `JsonElement`. Strings are not implicitly parsed as numbers, and assembly-qualified type names are rejected.

List responses contain `variables`, `totalCount`, `hasMore`, and `capturedAt`; `take` defaults to 100 and accepts 1–200. Each entry has its name, CLR type, `valueAvailable`, JSON `value`, and raw `valueJson` text preserving numeric precision. Complex CLR objects expose type metadata only; inspection does not execute arbitrary getters or `ToString()`. JSON values are bounded to 16 KiB and responses to 512 KB, with unavailable markers for oversized projections.

MCP exposes `sereinflow_get_run_variables` (`runId`, optional `name` or pagination) and `sereinflow_set_run_variable` (`runId`, `name`, `value`, `valueType`, `idempotencyKey`). Successful retries with the same key replay the response without another write.

Open **Flow variables** from the editor or run snapshot panel switcher. Active runs refresh every second; finished runs load a saved final snapshot, including when reopened later. Ordinary and debug Workers capture final variables once before exit and persist them with the terminal event. `isFinal` identifies saved snapshots; `isTruncated` marks the 2,000-entry or 256 KB storage limit. Safe JSON values and type metadata are saved; original CLR objects such as `Mat` are not. Older runs, crashes, and forced termination may return `409 variable.snapshot_unavailable`. Missing variables return `404`, writes to finished runs `409`, invalid values `400`, oversized writes `413`, and acknowledgement timeouts `504`.
