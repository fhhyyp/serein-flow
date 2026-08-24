# Acceptance: Workbench API and Graph Fixes

## Result

Implementation is accepted for the scoped workflow persistence and graph-state repair.

| Criterion | Result | Evidence |
| --- | --- | --- |
| List stored projects | Passed | `GET /api/projects` returns `200` through the Vite proxy |
| Create project and flow | Passed | Real HTTP request created project and flow document |
| Load full flow document | Passed | Real HTTP request restored `trigger` node type |
| Optimistic save | Passed | Real HTTP update returned version `2` |
| Reject stale save | Passed | Reusing expected version `1` returned `409 Conflict` |
| Persist editor metadata | Passed | Infrastructure test round-tripped node UI metadata |
| Preserve non-target edges | Passed | Pure graph regression test retains the two unrelated edge IDs |
| Preserve per-canvas edges | Passed | Graph clone and DTO round-trip tests retain `Main` and `Init` edges |
| API isolation | Passed | Existing architecture suite remains green |
| API CORS / dev proxy | Passed | Vite `/api/projects` proxy returned `200`; CORS preflight returned `204` |

## Verification commands

```powershell
dotnet test SereinFlow.sln --no-restore
dotnet build src\SereinFlow.Api\SereinFlow.Api.csproj --no-restore
node --test --experimental-strip-types tests\canvasGraph.test.ts tests\flowDtoMapper.test.ts
frontend\sereinflow-web\node_modules\.bin\vue-tsc.cmd -b
frontend\sereinflow-web\node_modules\.bin\vite.cmd build
```

All commands passed. The .NET solution ran 47 discovered tests; the Application and API integration test assemblies presently have no discovered test classes. The frontend graph suite ran 3 tests.

## Runtime checks

- API health endpoint: `200 {"status":"Healthy"}`.
- Vite proxy to the API: `200 []` before the first workspace is created.
- Server API create/load/update/conflict sequence completed with expected status codes.
