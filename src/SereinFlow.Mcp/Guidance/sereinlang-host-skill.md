# SereinLang Host Interoperation

Calls, indexing, `.`, and `?.` chain left to right at the highest precedence:

```sereinlang
makeAdder(1)(2)(3)
matrix[0][1]
person?.address?.city
```

Use `?.` when a missing intermediate member should produce `null` instead of a
member-access failure.

Imports use this form:

```sereinlang
import { createStore, member : alias } from "relative/path.script"
```

The path is relative to the current script. The imported file's final value is
the module object from which members are destructured. Import aliases use `:`.

## SereinFlow built-in module

```sereinlang
import { variables, log, env } from "sereinflow"
variables.set("batch.count", 42)
if variables.contain("batch.count") {
    log.info(variables.get("batch.count"))
}
return variables.get("batch.count")
```

`variables.contain(name)`, `get(name)` and `set(name, value)` use the run's
`LibraryVariableService`, shared with C# nodes, listener triggers and FlowCall
frames. Names must be non-empty, case-sensitive strings. Missing values and
stored null both read as null; `contain` distinguishes them. `set` returns null.
Scalars are native CLR values (`42` stores an `int`); arrays store `object?[]`
and objects store `Dictionary<string, object?>`. Containers are copied on
conversion, so call `set` after script mutations. CLR handles keep their original
references; do not convert them to audit summaries. JSON values written through
HTTP/MCP are readable as script objects/arrays. Functions and method values cannot
be stored. Conversion is bounded to 32 nesting levels and 10,000 items per
container. Each table operation is locked, but read-modify-write is not atomic.
Different runs are isolated. Inspect values through `sereinflow_get_run_variables`
or the Flow variables panel; final projections are saved when the run exits.

`log.info(value)` and `log.error(value)` emit run logs. `env` exposes `runId`,
`projectId`, `flowId`, `canvasId`, `nodeId`, `frameDepth` and
`isCancellationRequested` for the current invocation.

The documented runtime surface includes `print`, `typeof`, `bool`, `double`,
`range`, `keys`, `len`, `now`, `date` and `timespan`. Array members may include
`.length`, `.count`, `.add` and `.toList`; object members may include `.has` and
`.keys`; string members may include `.split`, `.join`, `.toUpper`, `.toString`
and `.last` where the host supports them.

Use only functions, CLR factories and members exposed by the target SereinFlow
host. Do not invent file-system, network, environment-variable, assembly or
other capabilities. Compilation is diagnostic-only; persistence still requires
a flow preview, apply and readback.
