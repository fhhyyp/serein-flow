---
name: sereinlang
description: Author and diagnose SereinLang scripts for SereinFlow using flow input and output contracts and the sereinflow_compile_sereinlang MCP tool. Use when an AI needs to write, repair, or validate a SereinLang script without changing a flow or executing user code.
---

# SereinLang

Write SereinLang against the exact contract supplied by the selected flow
node. Use the SereinFlow compiler tool for diagnostics and treat the result
as the authoritative language and module environment.

## Workflow

1. Read the flow edit model and identify the target script node. Record its
   input names, type descriptions, required flags, default values, output
   contract, source name, and language version.
2. Write the smallest script that satisfies that contract. Preserve the
   existing behavior when repairing a script and do not invent runtime
   modules, filesystem access, network access, environment variables, or
   assembly references.
3. Call `sereinflow_compile_sereinlang` with the full source, a stable source
   name, the configured language version, and the declared input contracts.
4. Read every structured diagnostic, including code, severity, source name,
   line, and column. Correct the source from those locations and compile
   again. Do not hide a warning or treat a missing compiler as a successful
   compile.
5. Report the final success flag, source SHA-256, language version, bounded
   diagnostics, and compile duration.

## Boundaries

- Compilation is diagnostic-only. It does not execute the script, write a
  flow, bind an artifact, or replace the runtime script output.
- Never use the compiler tool as permission to modify a flow. A script change
  still requires `sereinflow_preview_flow_patch` with
  `replace_script_source`, explicit user confirmation, and
  `sereinflow_apply_flow_patch`.
- Respect source-size, input-count, and compiler-time limits. Split a task or
  simplify the script when limits are reached.
- Do not return full source or sensitive literal values unless the user
  explicitly requests them and the caller has the required sensitive-read
  permission.
- If the compiler reports an unavailable or unhealthy compiler, surface that
  structured error and stop rather than compiling with a local substitute.
