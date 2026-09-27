# Environment and keys

Run capacity depends on service settings as well as the flow itself. API keys control who can call protected endpoints.

## Runtime settings

**Queue capacity** controls how many tasks can wait. **Maximum concurrent runs** controls how many execute at once. Values that are too low delay work; values that are too high can overload the machine. Read current settings and change only needed fields.

Use `GET /api/environment/settings` to read and `PUT /api/environment/settings` to save. These settings affect future runs, not saved flow definitions.

## API keys

Send a key as `Authorization: Bearer` when an endpoint requires authentication. A key can be scoped to a project and limited to permissions such as running or reading results.

The first administrator key must be created locally. An administrator can then create others through the console or key API. The full secret is shown only once; rotating it invalidates the old secret.

See [Environment and keys API](/en/api/environment).
