# Final: Workbench API and Graph Fixes

## Delivered

The web workbench now has a real backend persistence path.

- ASP.NET Core exposes project discovery plus flow create, load, and versioned save endpoints.
- `SqlSugarFlowDefinitionRepository` stores canonical `FlowDefinitionDto` JSON in SQLite and appends every accepted version to `FlowDefinitionVersions`.
- The flow DTO now carries `trigger` and optional UI metadata, so the existing Vue editor can save and reload its node kinds, labels, descriptions, parameters, ports, and connections without data loss.
- The frontend initializes from the first server workspace or creates the default workflow on an empty database. The command-bar save operation calls the API and handles network failures and version conflicts without claiming a false save.
- Browser storage remains a recovery draft only.
- Vue Flow is keyed by active canvas, and edge removal now filters only explicit removed IDs. This prevents an internal Vue Flow reset or one edge deletion from clearing unrelated persisted edges.
- Chinese and English save/load/conflict feedback was added while retaining the light Minimalism / Swiss workbench design.

## Design influence

The UI/UX PRO MAX design-system query was used to keep the updated status behavior restrained and operational: neutral, compact information hierarchy; high-contrast light-state text; the existing `#0369A1` action color; no decorative visual changes. Connection semantics remain distinguished by their established blue execution and purple data treatments.

## Operational setup

The API database defaults to:

```text
src/SereinFlow.Api/data/sereinflow.db
```

Override it for deployment with `SereinFlow__DatabasePath`. The frontend uses same-origin `/api` by default; for a separately hosted API, define `VITE_API_BASE_URL`. Vite development proxies `/api` to `http://127.0.0.1:5178` unless `VITE_API_PROXY_TARGET` is set.

## Risk statement

The API does not load untrusted scripts or worker code, preserving the worker/API isolation boundary. This delivery persists and edits workflows; it does not yet make the existing UI Run button execute a server flow.
