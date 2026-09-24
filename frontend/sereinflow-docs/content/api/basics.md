# 地址、密钥与报错

先记住三件事：把请求发到对的地址、按宿主设置携带密钥、看 HTTP 状态判断结果。

## 请求地址

本机开发时，API 默认监听 `http://localhost:8188`。业务接口一般以 `/api` 开头，例如：

```http
GET /api/projects
```

健康检查是 `GET /healthz`。它不需要 API 密钥，可用于确认服务是否启动。`/swagger` 和 `/openapi/v1.json` 是默认的开发环境文档地址；宿主可以修改这些路径，也可以关闭文档页面。

## 什么时候要带密钥

官方 `SereinFlow.Api` 宿主默认关闭 REST 鉴权，方便本机开发。将 SereinFlow 集成到其他 ASP.NET Core 宿主时，REST 默认要求鉴权。实际行为由 `SereinFlow:Api:RequireAuthentication` 决定。

开启后，请在请求头放入：

```http
Authorization: Bearer <你的密钥>
```

管理项目、流程和环境的接口通常需要管理员密钥。运行相关接口分别检查 `run.execute`、`run.read` 或 `run.message.publish` 权限。管理密钥的接口即使 REST 鉴权开关关闭，也仍要求管理员密钥；首次创建管理员密钥的 `/mcp-keys/setup` 只接受本机请求。

完整密钥只在创建或轮换时显示一次。把它放在自己的安全配置中，不要写进文章、源码或日志。

## 看懂常见状态码

| 状态 | 一般表示什么 | 先检查什么 |
| --- | --- | --- |
| `200` 或 `201` | 已完成，或已创建 | 读取返回的对象和 ID |
| `202` | 请求已接受，仍可能在执行 | 用返回的 ID 再查状态 |
| `400` | 请求内容不合要求 | 看返回的校验问题 |
| `401` | 没有可用密钥 | 看 `Authorization` 请求头 |
| `403` | 有密钥，但权限不够 | 看密钥的项目范围和权限 |
| `404` | 地址中的资源不存在 | 检查 ID、路径及所属项目 |
| `409` | 当前版本或状态不允许操作 | 重新读取最新状态再决定下一步 |

大多数错误使用 `application/problem+json`，常见字段是 `status` 和 `title`；部分会带 `code` 或 `currentVersion`。流程校验等场景可能返回别的 JSON 结构，因此请保留整个响应体帮助排查。

## 用 PowerShell 发第一个请求

```powershell
Invoke-RestMethod 'http://localhost:8188/api/projects'
```

如果你的宿主要求密钥，可以加上 `-Headers @{ Authorization = "Bearer $apiKey" }`。这里的 `$apiKey` 由你在本机安全地赋值。
