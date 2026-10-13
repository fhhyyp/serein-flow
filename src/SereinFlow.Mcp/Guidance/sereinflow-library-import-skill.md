# SereinFlow Library Import

Submit the completed ZIP as base64 with its filename to
`sereinflow_preview_library_package`. The preview returns the server-derived
package SHA-256 and DLL hash metadata; callers do not supply these hashes.
Review the complete bounded result: hashes, PE and contract diagnostics,
recognized metadata, stable-ID confidence, duplicate IDs, dependencies, native assets, Flipflop
return types and project impact. The scanner inspects metadata without loading
or executing the assembly.

When the preview matches the requested logical task, apply it with the preview
identity and fingerprint, `confirmation: "APPLY"`, and an idempotency key via
`sereinflow_apply_library_package`. The apply request uses the staged package
from the preview and does not resend package bytes or hashes. The server checks
the package size and SHA-256 against the preview before importing it. If the
package changed, create and review a new preview. Importing does not attach a
library to a project unless attachment is also requested.

For attachment, use:

```text
sereinflow_preview_project_library_attach
-> inspect the attachment diff
-> sereinflow_apply_project_library_attach
```

After attachment, call the read-only
`sereinflow_create_library_node_template` with persisted `projectId`,
`libraryId`, `libraryNodeContractId` and position. Use its canonical `node`
unchanged in a v2 flow patch. Do not manually assemble ports, parameter IDs,
defaults, enum or variadic metadata, or execution ports. Keep attachment and
flow patch previews separate, then reread both persisted results.

Pause once if a preview exceeds the requested scope, changes production,
permissions or secrets, is destructive, or reports a conflict.
