# SereinLang Capability Index

This Resource routes SereinLang work. Load only the relevant module; compiler
diagnostics and the target node contract are authoritative for a concrete
script.

| Work | Resource URI |
| --- | --- |
| Lexical rules and expressions | `sereinflow://ai/skills/sereinlang/syntax` |
| Imports and host interoperation | `sereinflow://ai/skills/sereinlang/host` |
| Formal grammar lookup | `sereinflow://ai/skills/sereinlang/grammar` |

For authoring or repair, read the target node with
`sereinflow_get_flow_edit_model`, write the smallest source change, then call
`sereinflow_compile_sereinlang` with the complete source, declared inputs and
language version. Correct every structured diagnostic at its source location.

Compilation is diagnostic-only. To persist accepted source, use
`replaceScriptSource` in the flow preview, inspect it, apply it under the flow
task authorization, and reread the affected flow.
