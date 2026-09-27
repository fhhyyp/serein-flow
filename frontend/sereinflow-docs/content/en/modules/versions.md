# Versions and publishing

Saving a flow is like saving a draft. **Publishing** marks a version for external use while you continue editing.

## Development and production versions

- **Development version:** Created by editing and saving.
- **Production version:** Created by publishing; used by public flow interfaces.

A flow needs a production version before you can create a public interface. Otherwise the server returns `409`.

## Why writes include a version

Concurrent editors could overwrite each other. Include the version you read as `expectedVersion` when saving. If it changed, the server returns `409`; reload, compare, and submit again.

Publishing similarly requires `expectedDevelopmentVersion`. The server validates the flow before creating a production version.

## Rollback

Rollback does not delete history. It creates a new current version using older content. You must also provide the expected current version of that track, preventing a stale rollback after someone else's publication.

See [Flow versions API](/en/api/versions) for endpoints and request fields.
