# Environment and keys API

Environment settings control queue and runtime behavior. Keys control access to protected endpoints. These are usually administrator tasks.

## Runtime settings

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/environment/settings` | Read current runtime settings |
| `PUT` | `/api/environment/settings` | Save runtime settings |

Fields include queue capacity, concurrency, wait timeout, and library upload limits. GET first, retain fields you are not changing, then PUT the complete settings.

## API keys

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/security/api-keys/initial-admin` | First administrator key; local only |
| `GET` | `/api/security/api-keys` | List key metadata |
| `POST` | `/api/security/api-keys` | Create a key |
| `POST` | `/api/security/api-keys/{id}/rotate` | Rotate a key |
| `DELETE` | `/api/security/api-keys/{id}` | Revoke a key |

Except for initial setup, key management requires an administrator key even if ordinary REST authentication is off. A new key can have project scope, name, permissions, and expiry. The full secret is shown once after creation or rotation; listing will not return it again.

For a program that invokes flows, grant only the needed `run.execute` and `run.read` permissions. See [URLs, keys, and errors](/en/api/basics) for authentication behavior.
