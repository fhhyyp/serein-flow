# TODO: Workbench API and Graph Fixes

## Deferred work

1. Add browser E2E coverage that performs the physical Vue Flow delete-key gesture, switches canvases, refreshes the page, and verifies server restoration. The current reducer and DTO tests cover the state transitions, while manual HTTP verification covers the API path.
2. Implement the project picker and flow selector. The startup policy currently opens the first stored project and its first flow.
3. Add server-side execution endpoints and live event delivery through SignalR with SSE fallback. The current Run button remains a local visual simulation.
4. Evolve persisted UI metadata into explicit versioned editor metadata if third-party node editors or schema migrations need independent compatibility guarantees.
5. Make project creation and initial-flow insertion a single application transaction, then add an API integration suite with a disposable SQLite database.

## No configuration required

- No credentials, API keys, login configuration, or legacy `.dnf` migration is required for this delivery.
- SQLite files, recovery artifacts, and `.ssc` caches remain excluded from Git.
