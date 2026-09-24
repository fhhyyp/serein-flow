# 用 API 运行流程

这篇教程接着[创建第一个流程](/guide/first-flow)往下做。你已经在控制台保存了一个能运行的流程；现在用 PowerShell 发请求，让程序替你点击“运行流程”。

## 1. 找到项目和流程 ID

`GET /api/projects` 会返回项目，以及每个项目中的流程摘要：

```powershell
$projects = Invoke-RestMethod 'http://localhost:8188/api/projects'
$projects | ConvertTo-Json -Depth 6
```

在响应中找到目标项目的 `project.id`，以及 `flows` 中目标流程的 `id`。这两个值都是 GUID。把它们填到下面的变量里：

```powershell
$projectId = '<项目 ID>'
$flowId = '<流程 ID>'
```

如果 API 已开启鉴权，还要在每个请求中传入 Bearer 密钥；做法见[地址、密钥与报错](/api/basics)。

## 2. 提交运行

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

返回 `202 Accepted` 表示“已收到运行请求”，不表示流程已经完成。响应里的 `id` 是这次运行的 ID；每运行一次都会得到一个新的 ID。

## 3. 查状态和结果

```powershell
Invoke-RestMethod "http://localhost:8188/api/runs/$($run.id)"
Invoke-RestMethod "http://localhost:8188/api/runs/$($run.id)/outputs"
```

状态会从 `pending`、`running` 走向 `succeeded`、`failed` 等结束状态。你也可以读取 `.../events`，看它经历了哪些步骤。

## 如果请求失败

- `404`：检查项目 ID、流程 ID 是否来自同一个项目。
- `400`：查看响应里的校验问题，确认流程已经保存且有入口节点。
- `401` 或 `403`：检查是否启用了鉴权，以及密钥的权限。
- `409`：流程版本或运行状态可能已变化；重新读取流程后再试。

具体字段与其他运行接口见[运行 API](/api/runs)。
