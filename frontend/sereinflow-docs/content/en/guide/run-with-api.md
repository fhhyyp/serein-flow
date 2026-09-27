# Run a flow with the API

This tutorial continues [Create your first flow](/en/guide/first-flow). You have saved a runnable flow in the console; now use PowerShell to start it through the API.

## 1. Find the project and flow IDs

`GET /api/projects` returns projects and summaries of their flows:

```powershell
$projects = Invoke-RestMethod 'http://localhost:8188/api/projects'
$projects | ConvertTo-Json -Depth 6
```

Find the target `project.id` and the flow `id` in `flows`. Both are GUIDs:

```powershell
$projectId = '<project ID>'
$flowId = '<flow ID>'
```

If API authentication is enabled, include a Bearer key in each request. See [URLs, keys, and errors](/en/api/basics).

## 2. Submit a run

```powershell
$body = @{
  expectedFlowVersion = $null
  projectInputs = @{}
  timeoutSeconds = 60
  maxSteps = $null
} | ConvertTo-Json -Depth 6

$request = @{
  Method = 'Post'
  Uri = "http://localhost:8188/api/projects/$projectId/flows/$flowId/runs"
  ContentType = 'application/json'
  Body = $body
}
$run = Invoke-RestMethod @request
$run.id
```

`202 Accepted` means the request was received, not that the flow finished. The returned `id` identifies this run; each run gets a new ID.

## 3. Check status and results

```powershell
Invoke-RestMethod "http://localhost:8188/api/runs/$($run.id)"
Invoke-RestMethod "http://localhost:8188/api/runs/$($run.id)/outputs"
```

Status progresses from `pending` and `running` to a terminal state such as `succeeded` or `failed`. Read `.../events` to inspect the steps.

## If the request fails

- `404`: Check that the project and flow IDs belong together.
- `400`: Check validation errors and make sure the flow is saved with an entry node.
- `401` or `403`: Check authentication and key permissions.
- `409`: The flow version or run state may have changed; reload the flow.

See [Runs API](/en/api/runs) for other endpoints and fields.
