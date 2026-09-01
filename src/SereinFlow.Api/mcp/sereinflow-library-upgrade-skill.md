# SereinFlow Library Upgrade

A library family groups immutable artifacts for compatibility selection. Do
not infer a family from a package name or version. Family assignment is a
separate administrator-only preview/apply operation and does not change an
existing flow binding.

For a project upgrade, preserve this sequence:

```text
sereinflow_get_project_libraries
-> sereinflow_list_library_families / sereinflow_get_library_family
-> sereinflow_preview_library_upgrade
-> inspect every per-flow blocker and acknowledgement
-> confirm the requested upgrade scope when not already authorized
-> sereinflow_apply_library_upgrade
-> sereinflow_get_library_upgrade
-> reread project libraries and affected flow topology
```

The source artifact must already be referenced by the project; source and
target must be in the same explicitly assigned family and the target must be
available. Upgrade preview requires `flow.write` and `library.manage`.
Project-scoped callers see only artifacts referenced by that project; attach a
target through the attachment workflow first when needed.

Apply is bound to the stored preview and cannot substitute project, plan,
source or target IDs. Supply only the preview fingerprint, protocol
confirmation, idempotency key and selected flow/version/acknowledgement items.
A batch is per-flow transactional and may contain both `succeeded` and
`failed`; use persisted readback before reporting completion.
