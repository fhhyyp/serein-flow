# Debug sessions API

A normal run executes to completion. A debug session can pause before a selected node, inspect state, then continue or step.

## Create and read

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/projects/{projectId}/flows/{flowId}/debug-sessions` | Start session |
| `GET` | `/api/debug-sessions/{sessionId}` | Read session state |
| `GET` | `/api/debug-sessions/{sessionId}/wait` | Wait for state change |
| `GET` | `/api/runs/{runId}/debug-session` | Find session from run ID |

Creation can include `breakpointNodeIds`, flow inputs, timeout, and expected version. Save the `sessionId` from the `202` response and read its status.

`/wait` accepts `afterRevision` and `timeoutSeconds` (0–60). It waits for a change or timeout without modifying the flow.

## Control execution

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/debug-sessions/{sessionId}/continue` | Continue |
| `POST` | `/api/debug-sessions/{sessionId}/step` | Execute one step |
| `POST` | `/api/debug-sessions/{sessionId}/stop` | Stop |

Each body needs an increasing `commandSequence` within the session to prevent a retried old command from executing again. Read the session after each state change. `409` usually means its state has changed.

Try **Debug flow** in the console first, then automate with these endpoints. See [Runs and results](/en/modules/runs).
