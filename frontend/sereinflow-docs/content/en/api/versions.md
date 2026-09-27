# Flow versions API

Edits create development versions; publishing creates a production version for public interfaces. These routes list history, publish, or create a new head from older content.

Prefix: `/api/projects/{projectId}/flows/{flowId}`.

| Method | Suffix | Purpose |
| --- | --- | --- |
| `GET` | `/versions` | List versions; filter with `track` |
| `GET` | `/versions/{version}` | Read a version and its flow content |
| `POST` | `/publish` | Publish the current development version |
| `POST` | `/versions/{version}/rollback` | Create a new current version from old content |

## Publishing

`expectedDevelopmentVersion` must match the development version just read. `remark` is optional:

```json
{
  "expectedDevelopmentVersion": 3,
  "remark": "Fix order processing"
}
```

The server checks executability and referenced libraries. Publication fails if the version changed, the project is archived, or validation fails.

## Rollback

Also provide `track` (`development` or `production`) and `expectedHeadVersion`. Rollback creates a new version rather than erasing intervening history. If the head changed, reload before deciding.

See [Versions and publishing](/en/modules/versions).
