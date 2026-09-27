# Runs and results

A flow definition describes what to do; a **run** executes it once. Each run has its own ID, start and end times, status, events, and outputs.

## Run lifecycle

A submitted task may start as `pending`, then become `running`. Terminal states include `succeeded`, `failed`, `cancelled`, `timedOut`, and `interrupted`.

A `202` response from `POST .../runs` means the task was accepted. Query `GET /api/runs/{runId}` for its final result.

## Events, outputs, and artifacts

| Question | Endpoint |
| --- | --- |
| Which step is executing? | `/api/runs/{runId}/events` |
| What values did nodes expose? | `/api/runs/{runId}/outputs` |
| Were files generated? | `/api/runs/{runId}/workpieces` |

Events are ordered by `sequence`. Pass `afterSequence` to fetch only newer events. Use `/events/stream` for a continuous feed and `Last-Event-ID` to resume after disconnecting.

## When to debug

If a result is unexpected, start a **debug session**, set a breakpoint, and step through execution. It owns a separate debug run. See [Debug sessions API](/en/api/debug) and [Runs API](/en/api/runs).
