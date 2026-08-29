---
name: sereinlang
description: Author and diagnose SereinLang scripts for SereinFlow using the bundled syntax guide, flow input/output contracts, and the sereinflow_compile_sereinlang MCP tool. Use when an AI needs to write, repair, explain, or validate SereinLang without changing a flow or executing user code.
---

# SereinLang

Read [references/syntax-guide.md](references/syntax-guide.md) before writing a
non-trivial script. Treat the connected SereinFlow compiler and the selected
node contract as authoritative if the guide, examples, or intuition disagree.

## Workflow

1. Read the flow edit model and identify the target script node. Record its
   input names, type descriptions, required flags, default values, output
   contract, source name, and language version.
2. Write the smallest script that satisfies that contract. Preserve existing
   behavior when repairing a script. Use the exact input names; do not invent
   runtime modules, filesystem access, network access, environment variables,
   or assembly references.
3. Call `sereinflow_compile_sereinlang` with the full source, a stable source
   name, the configured language version, and the declared input contracts.
4. Read every structured diagnostic, including code, severity, source name,
   line, and column. Correct the source from those locations and compile
   again. Do not hide a warning or treat a missing compiler as success.
5. Report the final success flag, source SHA-256, language version, bounded
   diagnostics, and compile duration.

## SereinFlow Script Rules

- Model scripts as expressions. A block returns its final expression, and a
  script node should return the value required by its output contract.
- Use `let` for an immutable binding and `var` for a reassignable binding.
- Use double-quoted strings, `//` comments, `=` in object properties, and
  `=>` or `->` for lambdas. Do not use JavaScript object `:` syntax.
- Prefer explicit parentheses around complex conditions and function calls.
- Use `?.` for null-safe member access, `when` for pattern matching, and
  `for item in iterable { ... }` for iteration.
- Keep imported modules relative to the script and import named members with
  `import { name } from "path"`.
- Treat CLR access as host-provided behavior. Do not assume a CLR factory or
  type exists unless the node contract supplies it.

## Boundaries

Compilation is diagnostic-only. It does not execute the script, write a flow,
bind an artifact, or replace the runtime script output. A script change still
requires `sereinflow_preview_flow_patch` with `replace_script_source`, explicit
user confirmation, and `sereinflow_apply_flow_patch`.

Respect source-size, input-count, and compiler-time limits. Split a task or
simplify the script when limits are reached. Do not return full source or
sensitive literal values unless the user explicitly requests them and the
caller has the required sensitive-read permission. If the compiler is
unavailable or unhealthy, surface its structured error and stop rather than
using a local substitute.
