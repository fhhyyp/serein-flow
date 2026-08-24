# Acceptance: Canvas Node Seat Refactor

## Results

| Check | Result |
| --- | --- |
| Seat model regression tests | 3 passed |
| Existing graph, DTO, and empty-workspace tests | 8 passed |
| `vue-tsc -b` | Passed |
| Vite production build | Passed |
| Browser smoke test | Project menu opened; blank project showed one empty Main canvas; node insertion exposed seat labels; persisted main canvas rendered 4 nodes / 6 edges; switching to Init rendered 2 nodes / 1 edge and switching back restored 4 / 6 |

## Connection rendering fix

Vue Flow now receives each canvas through one `model-value` element list. This lets
the library register nodes before validating persisted edges. The connection
validator also ignores the edge currently being hydrated when checking for
duplicates; previously every restored edge was mistaken for its own duplicate
and removed from the internal store.

## Deferred

- Browser automation currently verifies rendering and click insertion. A full pointer-drag E2E test should be added when the browser test harness exposes a stable drag locator API for HTML5 data transfer.
- The new-project entry point starts with an empty Main canvas. The visible node
  library is an add-only capability catalog; it does not inject preset nodes into
  a newly created project.
