# SereinFlow Release

Publishing and rollback affect production state. Preview them separately,
inspect the target track, version, checksum and impact, and pause for one
explicit confirmation before applying unless the request already explicitly
authorizes that exact release operation.

After apply, reread the production-track version resources:
`sereinflow://projects/{projectId}/flows/{flowId}/versions/production` and,
when the applied version is known,
`sereinflow://projects/{projectId}/flows/{flowId}/versions/production/{version}`.
Also reread the publish or rollback preview at
`sereinflow://mcp-previews/{previewId}`. Verify the persisted track, checksum
and state. Treat a successful apply response as an acknowledgement only. Stop
on authorization, validation, stale-version, fingerprint or post-apply
verification errors; do not retry a rejected release blindly.
