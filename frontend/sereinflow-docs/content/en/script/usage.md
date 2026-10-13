# Use SereinScript in flows

Script nodes are useful for calculations, conditions, and data transformations. Start by passing in `41` and returning `42`, then explore logs, run variables, and common syntax.

## Edit and run in the console

1. Drag **Script** from the **Node library** onto a project canvas. To run it on its own, select **Flow entry** in the right **Node inspector**.
2. Add an input under **Script inputs**. Set its name to `amount`, value kind to `System.Int32`, mark it **Required**, choose **Literal** as its source, and enter `41`.
3. Open the script editor using the button in the script section. Replace the default `return 0` with the code below.
4. Select **Apply code** (or press `Ctrl+Enter`, `Cmd+Enter` on macOS), then **Save** the canvas. Applying code updates the current edit; saving the project is a separate action.
5. Select **Run flow**. Inspect logs in **Run output** and the node output in the run record. The result should be `42`.

```sereinlang
import { log } from "sereinflow"
let result = amount + 1
log.info(result)
return result
```

**Cancel** or `Esc` closes the editor and discards unapplied changes. The editor provides highlighting; compilation diagnostics and actual execution determine whether the script is valid.

## How inputs reach the script

An input's **name** is a variable the script can read directly. Do not declare `amount` again. Use unique identifiers starting with a letter or underscore, followed by letters, digits, or underscores. Avoid keywords such as `return` and `let`, and names that collide with imported members.

Inputs can come from literals, project inputs, expressions, or previous nodes. To consume an upstream result, connect its output to the script input and arrange execution with a flow connection. Data connections supply values; flow connections determine when nodes execute.

**Required** means the execution request must provide the input; its value can still be null. An omitted optional input is injected as `null`, so the script can supply a default:

```sereinlang
let actual = if amount == null then 0 else amount
return actual + 1
```

See [Input and output contracts](/en/script/script-adapter#input-and-output-contracts) for connector IDs, variable names, and conversion behavior.

## Return values and output connectors

`return` supplies the script result. A normal return of `false`, `null`, or `{ success = false }` still selects **Success**. The value does not automatically select **Failure**. Unhandled compilation and execution errors select **Error**.

| Output contract | Mapping |
| --- | --- |
| No outputs | The return value produces no node output. |
| One output | The connector receives the whole value, including objects or arrays. |
| Multiple outputs | Return an object; each output reads the property matching its contract `name`. Missing properties map to null. |

For two declared outputs named `total` and `accepted`:

```sereinlang
return { total = amount + 1, accepted = amount >= 0 }
```

The default node has one output. The current inspector can add and remove script inputs; configure multiple output contracts through the flow definition/API or custom development. Adding object properties in code does not add canvas connectors.

## Common syntax

| Form | Meaning |
| --- | --- |
| `let x = 1` | Declare a variable that cannot be reassigned. |
| `var x = 1`, `x = x + 1` | Declare and update a mutable variable. |
| `42`, `42L`, `3.14f`, `3.14`, `10.5m` | int, long, float, double, decimal literals. |
| `"text"`, `true`, `false`, `null` | String, Boolean, and null literals. |
| `[1, 2, 3]`, `items[0]` | Arrays and zero-based indexing. |
| `{ name = "Ada", count = 3 }`, `item.name` | Objects and property access; properties use `=`. |
| `+ - * / %`, `== != < <= > >=` | Arithmetic and comparisons. |
| `&&`, `\|\|`, `!` | Logical operations. |
| `if test { ... } else { ... }` | Conditional branches. |
| `if test then a else b` | A conditional expression that returns a value. |
| `for item in items { ... }` | Iterate a collection. |
| `let add = (a, b) => a + b` | Define a function and call it with `add(1, 2)`. |
| `import { log } from "sereinflow"` | Import a module member. |

For example, sum an array:

```sereinlang
let values = [1, 2, 3]
var total = 0
for value in values {
    total = total + value
}
if total > 0 {
    return total
} else {
    return 0
}
```

## Logs and invocation metadata

```sereinlang
import { log, env } from "sereinflow"
log.info(env.nodeId)
if env.isCancellationRequested {
    return null
}
return env.projectId
```

`log.info(value)` and `log.error(value)` publish structured run logs and return null. Calling `log.error` sets the log level to error; it does not itself fail the node. Messages retain at most 16 Ki characters, with an ellipsis appended when truncated.

`env` exposes these read-only properties:

| Property | Meaning |
| --- | --- |
| `runId`, `projectId`, `flowId` | Current run, project, and flow IDs as strings. |
| `canvasId`, `nodeId` | Current canvas and node IDs as strings. |
| `frameDepth` | Current invocation frame depth, an integer. |
| `isCancellationRequested` | Whether cancellation has been requested, a Boolean. |

## Share variables within a run

```sereinlang
import { variables, log } from "sereinflow"
variables.set("batch.count", 42)
variables.set("batch.note", null)
if variables.contain("batch.count") {
    log.info(variables.get("batch.count"))
}
return variables.get("batch.count")
```

`set(name, value)` creates or replaces a variable. `get(name)` returns null for an absent name or stored null. `contain(name)` tests presence, including stored null. Names are case-sensitive and cannot be empty or whitespace-only.

Scripts, C# library nodes, listener triggers, and FlowCall frames in one run share the table; separate runs are isolated. Arrays and objects are copied on conversion, so call `set` again to save changes. CLR handles retain their references. Functions, methods, and cycles cannot be stored. Conversion allows at most 32 nesting levels and 10,000 items per container. Individual operations are locked; a `get` followed by `set` is not an atomic update.

Inspect live values in **Flow variables** and final values in the saved run snapshot. See [Built-in services](/en/library/built-in-services#share-flow-variables) for C# access and [Runs API](/en/api/runs#flow-variables) for routes.

## Language built-in modules

The engine also supplies `system`, exporting `file`, `network`, `console`, `path`, `json`, `timer`, `crypto`, and `process`. For example, parse JSON:

```sereinlang
import { json } from "system"
let payload = json.parse("{\"count\":41}")
return payload.count + 1
```

File paths and external calls use the Worker's environment. Use `sereinflow.log` for run logs; `system.console` writes to the process console. Available system methods are defined in `src/ThirdParty/SereinScript/ScriptLang/System/` in the repository.

## Troubleshooting

| Symptom or code | Check |
| --- | --- |
| `script.input_missing` | A required input needs a data source and a value under the correct connector key. |
| `script.input_unknown` | The request may still use a deleted or changed input ID. |
| `script.compile_failed` | Check undeclared variables, object property `=`, brackets, and diagnostic line/column positions. |
| `script.output_shape_invalid` | A multiple-output contract requires an object result. |
| `script.value_conversion_failed` | Check the downstream .NET parameter type; `"42"` does not automatically become an integer. |
| `script.value_unsupported` | Check unsupported conversions; custom hosts must supply a run variable table for `variables`. |
| `script.cancelled` | The script invocation was cancelled; inspect the run's stop status. |

Developers can obtain structured diagnostics with `ISereinLangCompiler.CompileAsync`. It compiles without execution and currently accepts language version `0.1`. See [Diagnostic compilation](/en/script/script-adapter#diagnostic-compilation) for limits and an example.
