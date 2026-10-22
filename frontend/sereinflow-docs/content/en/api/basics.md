# URLs, keys, and errors

Send requests to the correct address, include a key when the host requires one, and use HTTP status to interpret the result.

## Base URL

The local API defaults to `http://localhost:8188`. Business routes generally start with `/api`:

```http
GET /api/projects
```

`GET /healthz` needs no API key and confirms that the service is running. `/swagger` and `/openapi/v1.json` are default Development documentation routes; hosts can change or disable them.

## When a key is needed

The official `SereinFlow.Api` host enables the optional `SereinFlow.Security.ApiKeys` library by default. Embedded applications select `UseHostSecurity()` to reuse their identity, resource policies and project scope provider without a second SereinFlow Key check. HTTP hosts must select a security mode; missing configuration fails startup.

Programmatic callers in Key mode send:

```http
Authorization: Bearer <your key>
```

Project, flow and environment administration generally require `workspace.manage`. Run routes check `run.execute`, `run.read` or `run.message.publish`. Key management requires `security.manage`; `POST /api/security/api-keys/initial-admin` requires a direct loopback request and an empty Key table.

Browsers use a Bearer Key with `POST /api/security/session` to create an eight-hour HttpOnly, SameSite=Strict cookie session. Later requests revalidate the Key; `DELETE` signs out. REST, SignalR and SSE share credentials without long-lived keys in URLs. Anonymous `GET /api/security/capabilities` returns the mode and setup status. stdio uses a trusted local identity and needs no Key.

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
