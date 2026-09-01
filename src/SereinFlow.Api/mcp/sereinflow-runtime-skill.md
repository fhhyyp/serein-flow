# SereinFlow Runtime

For execution problems, inspect the current run Resource or debug session
Resource and use the bounded diagnostics returned by the service. Only the
production head is eligible for environment execution; development and
production tracks are separate.

After any successful mutation, reread the affected resource and verify its
version, checksum, counts and state. An apply response is an acknowledgement,
not the authoritative projection. If readback does not match, report
`mcp.post_apply_verification_failed` and stop.

Keep run output, scripts, literals, keys and server-local paths bounded and
redacted. Report stable MCP error codes, diagnostic IDs and safe fields. Do not
blindly retry a rejected or non-idempotent mutation.
