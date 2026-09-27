# URLs, keys, and errors

Send requests to the correct address, include a key when the host requires one, and use HTTP status to interpret the result.

## Base URL

The local API defaults to `http://localhost:8188`. Business routes generally start with `/api`:

```http
GET /api/projects
```

`GET /healthz` needs no API key and confirms that the service is running. `/swagger` and `/openapi/v1.json` are default Development documentation routes; hosts can change or disable them.

## When a key is needed

The official `SereinFlow.Api` host disables REST authentication by default for local development. In another ASP.NET Core host, REST authentication is required by default. `SereinFlow:Api:RequireAuthentication` determines the behavior.

When enabled, send:

```http
Authorization: Bearer <your key>
```

Project, flow, and environment administration usually require an administrator key. Run routes check `run.execute`, `run.read`, or `run.message.publish`. Key management always requires an administrator key even when normal REST authentication is disabled. First administrator setup at `/mcp-keys/setup` accepts local requests only.

The full secret is shown once on creation or rotation. Store it securely, never in documentation, source, or logs.

## Common status codes

| Status | Meaning | Check |
| --- | --- | --- |
| `200` or `201` | Completed or created | Returned object and ID |
| `202` | Accepted, possibly still running | Query with returned ID |
| `400` | Invalid request | Validation details |
| `401` | No usable key | `Authorization` header |
| `403` | Key lacks permission | Scope and permissions |
| `404` | Resource not found | ID, path, and project |
| `409` | Current version or state forbids action | Reload latest state |

Most errors use `application/problem+json` with `status` and `title`, sometimes `code` or `currentVersion`. Validation errors can use another JSON shape, so retain the full response for troubleshooting.

## First PowerShell request

```powershell
Invoke-RestMethod 'http://localhost:8188/api/projects'
```

If the host requires a key, add `-Headers @{ Authorization = "Bearer $apiKey" }`, with `$apiKey` securely set locally.
