# Acceptance: Blank Project Default

## Verification matrix

| Requirement | Evidence | Status |
| --- | --- | --- |
| Fresh workspace is clean | `initialCanvases.test.ts` passed | Passed |
| User-created graphs round-trip | `flowDtoMapper.test.ts` passed | Passed |
| Empty project persists structurally | Temporary SQLite HTTP `POST` returned `201`; subsequent `GET` returned one empty `Main` canvas | Passed |
| Empty project cannot run | `ExecutionPlanTests` diagnostic assertion passed | Passed |
| Frontend compiles and bundles | `vue-tsc -b` and `vite build` passed | Passed |
| Backend solution passes | Debug and Release `dotnet test SereinFlow.sln --no-restore` passed | Passed |

## Executed checks

```text
node --test --experimental-strip-types tests/canvasGraph.test.ts tests/flowDtoMapper.test.ts tests/initialCanvases.test.ts
vue-tsc -b
vite build
dotnet test SereinFlow.sln --no-restore
dotnet test SereinFlow.sln --configuration Release --no-restore
```

The .NET suite reported 43 discovered tests passed: Domain 10, Runtime 6, ScriptAdapter 6, Infrastructure 5, Worker integration 6, and Architecture 16. The existing Application and API integration test projects contain no discovered test methods.

## HTTP persistence evidence

A fresh API instance using a unique temporary SQLite database accepted an empty definition through `POST /api/projects` with status `201`. A following `GET /api/projects/{projectId}/flows/{flowId}` returned:

```json
{
  "entryNodeId": "",
  "canvasCount": 1,
  "nodeCount": 0,
  "connectionCount": 0
}
```

The temporary API process and all temporary SQLite files were removed after validation.

