# Debugging

A normal run continues to completion. Debugging lets you pause before a node, inspect inputs and previous steps, then continue or step once.

## Breakpoints

Put node IDs in `breakpointNodeIds` when starting a debug session. Execution pauses before those nodes so you can inspect state.

You can also start without breakpoints and observe the session. The session ID controls **continue**, **step**, and **stop**; its associated run ID is used to read events and outputs.

## Command sequence

1. Read the debug session and check whether it is paused.
2. Choose **continue**, **step**, or **stop**.
3. Send the command with `commandSequence`.
4. Read the session again to confirm the state.

Increment `commandSequence` for each new command in a session. It prevents network retries from executing an old command twice.

Try **Debug flow** in the console first, then follow [Debug sessions API](/en/api/debug) to automate the same steps.

## Inspect flow variables

Open **Flow variables** from the panel switcher to inspect names, CLR types, and values shared by this run. Inspection works while paused, refreshes every second while open, and supports manual refresh and paging. The Worker saves a final bounded projection before normal exit; finished runs and reopened historical snapshots load that saved data. Original CLR objects are not persisted. You can also set variables through MCP before continuing execution; see [Runs API](/en/api/runs#flow-variables).
