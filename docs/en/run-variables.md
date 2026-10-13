# Variables in an active flow run

Each run owns one `LibraryVariableService`, shared by node contexts, listener triggers, FlowCall frames, and uploaded libraries. Other runs have independent tables. Nodes use constructor injection or `IFlowContext.Variables`; see [Node library development](node-library-development.md#92-flow-variables-libraryvariableservice). `Snapshot()` copies the table under its lock while retaining stored object references.

## HTTP access

| Method | Route | Permission |
| --- | --- | --- |
| `GET` | `/api/runs/{runId}/variables?offset=0&take=100` | `run.read` |
| `GET` | `/api/runs/{runId}/variables/{name}` | `run.read` |
| `PUT` | `/api/runs/{runId}/variables/{name}` | `run.variable.write` |

Names are case-sensitive, non-empty, and limited to 256 characters for external requests; URL-encode path names. Lists are sorted by name. `offset` starts at 0 and `take` defaults to 100, with a range of 1–200. Responses contain `variables`, `totalCount`, `hasMore`, and `capturedAt`, and are not cached. Writes require an explicit JSON value and type:

```json
{ "value": 42, "valueType": "int32" }
```

| `valueType` | Stored CLR value |
| --- | --- |
| `null` | null; `value` must be JSON null |
| `boolean` | `System.Boolean` |
| `string` | `System.String`, including null |
| `int32` | `System.Int32` |
| `int64` | `System.Int64` |
| `double` | Finite `System.Double` |
| `decimal` | `System.Decimal` |
| `json` | `System.Text.Json.JsonElement` for objects, arrays, or any JSON value |

Read an `int32` write with `Get<int>(name)` and a `json` write with `Get<JsonElement>(name)`. Assembly-qualified type names and implicit string-to-number conversions are not supported. Successful writes return `200` with the variable's metadata and JSON projection.

## MCP access

- `sereinflow_get_run_variables` requires `runId`; provide `name` for one variable or `offset` / `take` for a page. Requires `run.read` for the run's project.
- `sereinflow_set_run_variable` requires `runId`, `name`, `value`, `valueType`, and `idempotencyKey`. Requires `run.variable.write` for the run's project. Retrying a successful request with the same key replays its response without another Worker write.

```json
{
  "runId": "GUID of the active run",
  "name": "batch.count",
  "value": 42,
  "valueType": "int32",
  "idempotencyKey": "batch-count-write-1"
}
```

Use a new key for a new write. The key management panel exposes the variable-write permission. Cross-project requests are denied before reaching the Worker.

## Live frontend inspection

After starting a run or debug session, open **Flow variables** from the workspace panel switcher. It shows names, CLR types, and values, refreshes every second while open, and supports manual refresh and 25-item pages. The run console's active-run snapshot viewer exposes the same panel. Switching runs, changing pages, and closing the panel cancel obsolete requests.

Before returning its result, the Worker captures final variables once and persists their projection with the terminal event. Both ordinary and debug runs support successful, failed, cooperatively cancelled, and cooperatively timed-out exits. The panel stops polling and loads the saved final snapshot, which remains readable and pageable when reopening a historical run.

Original CLR objects are not persisted. The final snapshot retains bounded JSON projections and type metadata, up to 2,000 entries and 256 KB. Names longer than 256 characters are omitted; oversized projections retain type metadata and a `snapshot_limit` marker. `isTruncated` reports truncation, `isFinal` distinguishes saved snapshots from live reads, and `capturedAt` records the capture time. Older runs, crashes, and forced Worker termination can have no saved snapshot; their variables cannot be reconstructed and the panel reports this explicitly.

## JSON projections and errors

Entries contain `name`, `clrType`, `valueAvailable`, `value`, raw `valueJson` text preserving numeric precision, and optional `unavailableReason`. Stored null is present and has `valueAvailable: true`. A missing variable returns `404 variable.not_found`; a missing run returns `404 run.not_found`; queued Workers and writes to finished runs return `409 worker.not_active`. Historical reads without a saved snapshot return `409 variable.snapshot_unavailable`.

Common scalars, `JsonElement`, bounded arrays, standard `List<T>`, and string-keyed `Dictionary` values can be projected to JSON. Custom objects, device handles, and `Mat` expose only their CLR type with `unsupported_type`. Inspection does not call arbitrary getters, converters, or `ToString()`. Each JSON value is limited to 16 KiB and responses to 512 KB. Larger local values remain in the Worker with `value_limit` or `snapshot_limit` markers. Collection projections are limited to 128 items and eight nesting levels.

Invalid requests or type mismatches return `400`, oversized writes return `413`, and Worker acknowledgement timeouts return `504 variable.request_timeout`. The table lock does not synchronize mutations to stored objects or multi-operation updates.
