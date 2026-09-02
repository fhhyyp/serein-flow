# SereinFlow Runtime

For execution problems, discover runs with `sereinflow_list_runs` or active
debug sessions with `sereinflow_list_debug_sessions`. Start an interactive
session with `sereinflow_start_debug_session`, then use
`sereinflow_wait_debug_state` and `sereinflow_get_debug_state` to observe it.
When the state is paused, send `sereinflow_step_debug` or
`sereinflow_continue_debug` with the next strictly increasing
`commandSequence`; use `sereinflow_stop_debug` to cancel it. Only the
production head is eligible for environment execution; development and
production tracks are separate.

To inject an external event into an active ordinary or debug run, first confirm
the run and its exposed endpoint from current run state, then call
`sereinflow_publish_run_message` with the `runId`, topic, arbitrary JSON
`payload`, `channelKind` (`queue` or `eventBus`) and a non-empty
`idempotencyKey`. An optional `contractId` documents the endpoint contract and
an optional `messageId` controls Worker-level deduplication. The tool requires
`run.message.publish` and project scope. `accepted` means only that the run's
Worker broker accepted the message; reread run inspection and downstream
outputs or events to verify Flipflop processing. Do not treat acceptance as
business completion or blindly retry with a new idempotency key.

After any successful mutation, reread the affected resource and verify its
version, checksum, counts and state. An apply response is an acknowledgement,
not the authoritative projection. If readback does not match, report
`mcp.post_apply_verification_failed` and stop.

Keep run output, scripts, literals, keys and server-local paths bounded and
redacted. Report stable MCP error codes, diagnostic IDs and safe fields. Do not
blindly retry a rejected or non-idempotent mutation. Discovery, state and wait
reads accept `debug.read` or `run.read`; start and control require
`debug.control`.

Debug start is idempotent when the same `idempotencyKey` is replayed with the
same request. Debug control commands use `commandSequence` rather than an
idempotency key; after an ambiguous response, read the session state before
choosing the next sequence.
