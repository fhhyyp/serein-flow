# SereinFlow Release

Publishing changes production state; rollback changes its selected development
or production track. Preview them separately and inspect the track, version,
checksum and impact. The user's explicit request can authorize that exact
release operation; ask only when the release or affected scope was not authorized.

The `track` argument for version comparison and rollback is the canonical
string `development` or `production`. Publishing always promotes the current
development version; rollback requires an explicit selected track and the
matching `expectedHeadVersion`.

After publishing, reread the production-track version resources. After rollback,
reread the track selected in the stored preview (`development` or `production`):
`sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}` and,
when the applied version is known,
`sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}`.
Also reread the publish or rollback preview at
`sereinflow://mcp-previews/{previewId}`. Verify the persisted track, checksum
and state. Treat a successful apply response as an acknowledgement only. Stop
on authorization, validation, stale-version, fingerprint or post-apply
verification errors; do not retry a rejected release blindly.
